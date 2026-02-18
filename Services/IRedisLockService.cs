namespace IdempotencyWithRedisLockMssqlDemo.Services
{
    public interface IRedisLockService
    {
        Task<bool> AcquireAsync(string key, TimeSpan ttl);
        Task ReleaseAsync(string key, TimeSpan ttl);
    }
}
