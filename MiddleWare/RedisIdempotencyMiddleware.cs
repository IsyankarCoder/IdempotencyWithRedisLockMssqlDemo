using IdempotencyWithRedisLockMssqlDemo.Services;
using System.Text;

namespace IdempotencyWithRedisLockMssqlDemo.Middleware
{
    public class RedisIdempotencyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RedisIdempotencyMiddleware> _logger;

        private const string IdempotencyHeader = "X-Idempotency-Key";
        private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        public RedisIdempotencyMiddleware(
            RequestDelegate next,
            ILogger<RedisIdempotencyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IRedisIdempotencyStore store)
        {
            // Sadece POST
            if (!HttpMethods.IsPost(context.Request.Method) ||
                !context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) ||
                string.IsNullOrWhiteSpace(key))
            {
                await _next(context);
                return;
            }

            var idempotencyKey = key.ToString();

            try
            {
                // 1️⃣ CACHE KONTROL
                var cachedResponse = await store.GetAsync(idempotencyKey);

                if (!string.IsNullOrEmpty(cachedResponse))
                {
                    _logger.LogInformation("Idempotency cache HIT: {Key}", idempotencyKey);
                    await WriteResponseAsync(context, cachedResponse);
                    return;
                }

                // 2️⃣ LOCK AL
                var lockValue = await store.AcquireAsyncLockWithLua(idempotencyKey, LockTtl);

                if (lockValue is null)
                {
                    _logger.LogWarning("Lock conflict for key: {Key}", idempotencyKey);

                    // kısa bekleme
                    await Task.Delay(200);

                    cachedResponse = await store.GetAsync(idempotencyKey);

                    if (!string.IsNullOrEmpty(cachedResponse))
                    {
                        await WriteResponseAsync(context, cachedResponse);
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsync("Duplicate request in progress");
                    return;
                }

                // 3️⃣ RESPONSE CAPTURE
                var originalBody = context.Response.Body;

                await using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                try
                {
                    await _next(context);

                    memoryStream.Position = 0;
                    var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

                    // 4️⃣ SADECE 2XX ise cache yaz
                    if (context.Response.StatusCode >= 200 &&
                        context.Response.StatusCode < 300)
                    {
                        await store.SetAsync(idempotencyKey, responseBody, CacheTtl);
                        _logger.LogInformation("Response cached for key: {Key}", idempotencyKey);
                    }

                    // Response'u geri yaz
                    memoryStream.Position = 0;

                    context.Response.Body = originalBody;
                    context.Response.ContentLength = memoryStream.Length;

                    await memoryStream.CopyToAsync(originalBody);
                }
                finally
                {
                    // 5️⃣ LOCK BIRAK (garantili)
                    await store.ReleaseLockAsyncWithLua(idempotencyKey, lockValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Idempotency middleware error for key {Key}", idempotencyKey);
                throw; // global exception handler’a bırak
            }
        }

        private static async Task WriteResponseAsync(
            HttpContext context,
            string body)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(body);

            await context.Response.WriteAsync(body);
            await context.Response.Body.FlushAsync();
        }
    }
}