using IdempotencyWithRedisLockMssqlDemo.Helper;
using IdempotencyWithRedisLockMssqlDemo.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace IdempotencyWithRedisLockMssqlDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController
        : ControllerBase
    {
        private readonly byte[] _secretKey = Encoding.UTF8.GetBytes("Bafra55");
        private string userId = "volkantolkan55";
        public TestController() { }

        /// <summary>
        /// Problemli Hash
        /// </summary>
        /// <returns></returns>
        [HttpPost("hash-problem")]
        public async Task<IActionResult> HashProblem()
        {
            //Raw body oku --> property order fark edebilir
            Request.EnableBuffering();
            string rawJson;

            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                rawJson =await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            
            var idempotencyKey = Request.Headers["X-Idempotency-Key"].ToString() ?? "no-key";

            var payload = $"{userId}:{idempotencyKey}:{rawJson}";

            var hash = HashHelper.ComputeHash(payload, secretKey: _secretKey);
            return Ok(new
            {
                RawJson = rawJson,
                hash = hash
            });

        }

        [HttpPost("hash-safe")]
        public async Task<IActionResult> HashSafe([FromBody] PaymentRequest paymentRequestDto,
                                                  [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey)
        {
            if (paymentRequestDto is null || string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest("Missing Idempotency Key or Request Body");
            }

            // Dto üzerinden normalize JSON

            var normalized = JsonHelper.NormalizeJson(paymentRequestDto);

            var payload = $"{userId}:{idempotencyKey}:{normalized}";

            var hash = HashHelper.ComputeHash(payload, _secretKey);

            return Ok(new
            {
                NormalizedJson = normalized,
                Hash = hash

            });
                

        }
    }
}
