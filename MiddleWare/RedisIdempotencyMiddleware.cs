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

        public async Task InvokeAsync(HttpContext context, IRedisIdempotencyStore store)
        {
            if (!HttpMethods.IsPost(context.Request.Method) ||
                !context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) ||
                string.IsNullOrWhiteSpace(key))
            {
                await _next(context);
                return;
            }

            var idempotencyKey = key.ToString();

            string? cachedResponse = null;
            string? lockValue = null;
            bool redisAlive = true;

            // 🔹 CACHE GET (FAIL SAFE)
            try
            {
                cachedResponse = await store.GetAsync(idempotencyKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GET failed. Continuing without cache.");
            }

            if (!string.IsNullOrEmpty(cachedResponse))
            {
                await WriteResponseAsync(context, cachedResponse);
                return;
            }

            // 🔹 LOCK ACQUIRE (FAIL SAFE)
            try
            {
                lockValue = await store.AcquireAsyncLockWithLua(idempotencyKey, LockTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis LOCK failed. Continuing without lock.");
                redisAlive = false;
            }

            // Eğer lock alamadıysa ama Redis çalışıyorsa
            if (redisAlive && lockValue == null)
            {
                try
                {
                    await Task.Delay(200);
                    cachedResponse = await store.GetAsync(idempotencyKey);

                    if (!string.IsNullOrEmpty(cachedResponse))
                    {
                        await WriteResponseAsync(context, cachedResponse);
                        return;
                    }
                }
                catch
                {
                    // ignore
                }

                // Redis çalışıyor ama lock başka request'te
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsync("Duplicate request in progress");
                return;
            }

            var originalBody = context.Response.Body;

            await using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            try
            {
                await _next(context);

                memoryStream.Position = 0;
                var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

                if (context.Response.StatusCode >= 200 &&
                    context.Response.StatusCode < 300)
                {
                    try
                    {
                        await store.SetAsync(idempotencyKey, responseBody, CacheTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Redis SET failed. Skipping cache write.");
                    }
                }

                memoryStream.Position = 0;
                context.Response.Body = originalBody;
                context.Response.ContentLength = memoryStream.Length;

                await memoryStream.CopyToAsync(originalBody);
            }
            finally
            {
                if (lockValue != null)
                {
                    try
                    {
                        await store.ReleaseLockAsyncWithLua(idempotencyKey, lockValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Redis RELEASE failed.");
                    }
                }
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