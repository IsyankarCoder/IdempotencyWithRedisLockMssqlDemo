using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdempotencyWithRedisLockMssqlDemo.Helper
{
    public class IdempotencyHashService
    {
        private readonly byte[] _secretKey;
        private readonly IWebHostEnvironment _env;
        public IdempotencyHashService(IConfiguration config, IWebHostEnvironment env)
        {
            _secretKey = Encoding.UTF8.GetBytes(
                config["Idempotency:SecretKey"]);
            
            //Encoding.UTF8.GetString(_secretKey)
            _env = env;
        }

        public string ComputeHash<T>(
            string userId,
            string idempotencyKey,
            T dto)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            var normalized = JsonSerializer.Serialize(dto, options);

            var payload = $"{userId}:{idempotencyKey}:{normalized}";

            using var hmac = new HMACSHA256(_secretKey);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            return Convert.ToBase64String(hash);
        }
    }
}
