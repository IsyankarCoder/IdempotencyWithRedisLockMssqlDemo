using System.Security.Cryptography;
using System.Text;

namespace IdempotencyWithRedisLockMssqlDemo.Helper
{
    public static class HashHelper
    {
        /*
         HMAC internal olarak secret'ı güvenli şekilde kullanır
         Length-extension attack riskini ortadan kaldırır
         Finansal sistemlerde standarttır
        */
         

        public static string ComputeHash(string input) 
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);

            return Convert.ToBase64String(hash);

        }

        public static string ComputeHash(string payload, byte[] secretKey)
        {
            using var hmac = new HMACSHA256(secretKey);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }
}
