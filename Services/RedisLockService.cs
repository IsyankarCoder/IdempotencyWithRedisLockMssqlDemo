using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;

namespace IdempotencyWithRedisLockMssqlDemo.Services
{
    public class RedisLockService
        : IRedisLockService
    {
        private readonly StackExchange.Redis.IDatabase _database;
        public RedisLockService(IConnectionMultiplexer connectionMultiplexer)
        {

            _database = connectionMultiplexer.GetDatabase();
        }

        /// <summary>
        /// SET lock:abc123 1 NX PX 10000
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ttl"></param>
        /// <returns></returns>
        public async Task<bool> AcquireAsync(string key, TimeSpan ttl)
        {
            var lockKey = $"lock:{key}";
            return await _database.StringSetAsync(lockKey, "1", ttl, When.NotExists);
        }

        public async Task ReleaseAsync(string key, TimeSpan ttl = default)
        {

            await _database.KeyDeleteAsync(key);
        }
    }
}
