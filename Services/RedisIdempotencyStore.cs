using StackExchange.Redis;

namespace IdempotencyWithRedisLockMssqlDemo.Services
{
    public class RedisIdempotencyStore 
        : IRedisIdempotencyStore
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        public RedisIdempotencyStore(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
        }

        public async Task<string?> AcquireAsyncLockWithLua(string key, TimeSpan ttl)
        {
            var lockValue = Guid.NewGuid().ToString();
            var acquired = await _db.StringSetAsync(key + ":lock", lockValue, ttl, When.NotExists);

            return acquired ? lockValue : null;

        }
        public async Task ReleaseLockAsyncWithLua(string key, string lockValue)
        {
            var script = @"if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";


            await _db.ScriptEvaluateAsync(script,
                                          new RedisKey[] { key + ":lock" },
                                          new RedisValue[] { lockValue });
        }

        public async Task<string> GetAsync(string key)
        {

            var value = await _db.StringGetAsync(key, CommandFlags.None);
            return value.HasValue ? value.ToString() :  null;

        }
        public async Task SetAsync(string key, string value, TimeSpan ttl)
        {
            await _db.StringSetAsync(key, value, ttl);
        }  
        
    }
}
