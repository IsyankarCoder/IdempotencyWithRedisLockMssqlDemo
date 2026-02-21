namespace IdempotencyWithRedisLockMssqlDemo.Services
{
    public interface IRedisIdempotencyStore
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value, TimeSpan ttl);


        Task<string?> AcquireAsyncLockWithLua(string key, TimeSpan ttl);
        Task ReleaseLockAsyncWithLua(string key, string lockValue);
    }
}
