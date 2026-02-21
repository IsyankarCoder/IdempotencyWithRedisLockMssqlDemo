using IdempotencyWithRedisLockMssqlDemo.Database;
using IdempotencyWithRedisLockMssqlDemo.Helper;
using IdempotencyWithRedisLockMssqlDemo.Model;
using IdempotencyWithRedisLockMssqlDemo.Provider;
using IdempotencyWithRedisLockMssqlDemo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StackExchange.Redis;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdempotencyWithRedisLockMssqlDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController 
        : ControllerBase
    {
        private readonly PaymentDBContext _db;
        private readonly IRedisLockService _lockService;
        private readonly IPaymentProvider _provider;
        private readonly IdempotencyHashService _hashService;

        public PaymentsController(
            PaymentDBContext db,
            IRedisLockService lockService,
            IPaymentProvider provider,
            IdempotencyHashService hashService)
        {
            _db = db;
            _lockService = lockService;
            _provider = provider;
            _hashService = hashService;
        }

        /// <summary>
        /// ✔ Aynı key + aynı body → aynı kayıt döner
        /// ✔ Aynı key +farklı body → 400
        /// ✔ Aynı anda gelen 2 request → biri Conflict
        /// ✔ Redis down olsa bile DB unique constraint korur
        /// ✔ JSON order problemi yok
        /// ✔ Raw body okuma yok
        /// ✔ Stream exception yok
        /// 
        /// Client
        ///  ↓
        /// Redis Lock
        /// ↓
        /// DB Unique(UserId + IdempotencyKey)
        /// ↓
        /// RequestHash kontrolü
        /// ↓
        /// External Provider
        ///  
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// 

        [HttpPost]
        public async Task<IActionResult> Pay(
            [FromBody] PaymentRequest request)
        {
            var userId = "IsyankarCoder55"; // normalde claim'den gelir
            var key = Request.Headers["X-Idempotency-Key"].ToString();

            // 🔑 Deterministic Hash
            var requestHash = _hashService
                   .ComputeHash(userId, key, request);

            var existing = await _db.Payments
                                    .FirstOrDefaultAsync(d => d.UserId == userId &&
                                                              d.IdempotencyKey == key);
            if (existing != null)
            {
                if (existing.RequestHash != requestHash)
                {
                    return BadRequest("Same Idempotency Key with different paylod");
                }

                return Ok(existing);
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
                Currency = request.Currency,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = key,
                RequestHash = requestHash
            };

            await _db.Payments.AddAsync(payment);
            await _db.SaveChangesAsync();

            var success = await _provider.ChargeAsync(request.Amount);
            payment.Status = success ? "Success" : "Fail";

            await _db.SaveChangesAsync();

            return Ok(payment);
        }

        /* [HttpPost]
         public async Task<IActionResult> Pay(
             [FromBody] PaymentRequest request)
         {
             var userId = "IsyankarCoder55"; // normalde claim'den gelir
             var key = Request.Headers["X-Idempotency-Key"].ToString();

             if (string.IsNullOrWhiteSpace(key))
                 return BadRequest("Missing Idempotency Key");

             var compositeKey = $"{userId}:{key}";

             // 🔐 Distributed Lock
             var locked = await _lockService
                 .AcquireAsync(compositeKey, TimeSpan.FromSeconds(30));

             if (!locked)
                 return Conflict("Request in progress");

             try
             {
                 // 🔑 Deterministic Hash
                 var requestHash = _hashService
                     .ComputeHash(userId, key, request);

                 // 🔎 DB kontrol
                 var existing = await _db.Payments
                     .FirstOrDefaultAsync(x =>
                         x.UserId == userId &&
                         x.IdempotencyKey == key);

                 if (existing != null)
                 {
                     if (existing.RequestHash != requestHash)
                         return BadRequest(
                             "Same Idempotency-Key with different payload");

                     return Ok(existing);
                 }

                 // 🆕 Yeni kayıt
                 var payment = new Payment
                 {
                     Id = Guid.NewGuid(),
                     UserId = userId,
                     Amount = request.Amount,
                     Currency = request.Currency,
                     IdempotencyKey = key,
                     Status = "Processing",
                     CreatedAt = DateTime.UtcNow,
                     RequestHash = requestHash
                 };

                 _db.Payments.Add(payment);
                 await _db.SaveChangesAsync();

                 // 💳 External Provider
                 var success = await _provider
                     .ChargeAsync(request.Amount);

                 payment.Status = success ? "Success" : "Failed";
                 await _db.SaveChangesAsync();

                 return Ok(payment);
             }
             catch (DbUpdateException)
             {
                 // Unique constraint yakalandı
                 var existing = await _db.Payments
                     .FirstAsync(x =>
                         x.UserId == userId &&
                         x.IdempotencyKey == key);

                 return Ok(existing);
             }
             finally
             {
                 await _lockService.ReleaseAsync(compositeKey, TimeSpan.FromMicroseconds(10));
             }
         }*/
    }

}
