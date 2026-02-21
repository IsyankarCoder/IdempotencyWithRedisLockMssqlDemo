using IdempotencyWithRedisLockMssqlDemo.Services;
using System.Net;

namespace IdempotencyWithRedisLockMssqlDemo.MiddleWare
{
    public class RedisIdempotencyMiddleware
    {
        private readonly RequestDelegate _requestDelegate;
        private readonly ILogger<RedisIdempotencyMiddleware> _logger;


        public RedisIdempotencyMiddleware(RequestDelegate requestDelegate,
                                          ILogger<RedisIdempotencyMiddleware> logger)
        {
            _logger = logger;
            _requestDelegate = requestDelegate;
        }

        public async Task InvokeAsync(HttpContext httpContext,
                                      IRedisIdempotencyStore redisIdempotencyStore)
        {
            // Yalnızca POST ve X-Idempotency-Key ile çalış

            if (!HttpMethods.IsPost(httpContext.Request.Method) ||
               !httpContext.Request.Headers.TryGetValue("X-Idempotency-Key", out var key))
            {
                await _requestDelegate(httpContext);
                return;
            }

            var IdempontencyKey = key.ToString();

            // 1-> cache control
            var cacheResponse = await redisIdempotencyStore.GetAsync(IdempontencyKey);
            if (cacheResponse is not null)
            {

                _logger.LogDebug("Idempotency cache hit for key {Key}", IdempontencyKey);
                await WriteCachedResponse(httpContext, cacheResponse);
                return;
            }

            // 2-> Lock Al 10sn liğine
            var lockValue = await redisIdempotencyStore.AcquireAsyncLockWithLua(IdempontencyKey, TimeSpan.FromSeconds(10));
            if (lockValue is null)
            {
                // kısa bekleme ve tekrar cache control 

                await Task.Delay(200);

                cacheResponse = await redisIdempotencyStore.GetAsync(IdempontencyKey);
                if (cacheResponse is not null)
                {
                    _logger.LogDebug("Idempotency cache hit after lock wait for key {Key}", IdempontencyKey.ToString());
                    await WriteCachedResponse(httpContext, cacheResponse);
                    return;
                }

                httpContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
                await httpContext.Response.WriteAsync("Duplicate request in progress");
                return;
            }

            // 3--> Response Capture
            var originalBody = httpContext.Response.Body;
            
            await using var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;

            try
            {
                await _requestDelegate(httpContext);
                memoryStream.Position = 0;

                var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

                // 4 --> Başarılı response ise cache yaz
                if (httpContext.Response.StatusCode >= (int)HttpStatusCode.OK &&
                    httpContext.Response.StatusCode < 300)
                {
                    await redisIdempotencyStore.SetAsync(IdempontencyKey,
                                                         responseBody,
                                                         TimeSpan.FromHours(24));

                }

                // 5 --> Clienta geri yaz
                memoryStream.Position = 0;
                httpContext.Response.Body = originalBody;
                await memoryStream.CopyToAsync(originalBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                throw;
            }
            finally
            {
                // 6 lock bırak
                await redisIdempotencyStore.ReleaseLockAsyncWithLua(IdempontencyKey, lockValue);

            } 

        }


        private static async Task WriteCachedResponse(HttpContext httpContext, string cachedRespone)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(cachedRespone);
        }
    }
}
