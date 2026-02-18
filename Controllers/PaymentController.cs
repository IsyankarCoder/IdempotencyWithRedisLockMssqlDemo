using IdempotencyWithRedisLockMssqlDemo.Database;
using IdempotencyWithRedisLockMssqlDemo.Provider;
using IdempotencyWithRedisLockMssqlDemo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace IdempotencyWithRedisLockMssqlDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController 
        : ControllerBase
    {
         private readonly ILogger<PaymentController> _logger;
         private readonly IConfiguration _configuration;
         private readonly PaymentDBContext _paymentDBContext;
         private readonly IRedisLockService _redisLockService;
         private readonly IPaymentProvider _paymentProvider;

        public PaymentController(ILogger<PaymentController> logger,
        IConfiguration configuration,
        PaymentDBContext paymentDBContext,
        IRedisLockService redisLockService,
        IPaymentProvider paymentProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _paymentDBContext = paymentDBContext;
            _redisLockService = redisLockService;
            _paymentProvider = paymentProvider;
        }


        [HttpPost]
        public async Task<IActionResult> Post([FromBody] decimal amount, 
                                              [FromHeader(Name ="X-Idempotency-Key")] string Key )
        {
            var userId = "User-1";
            //var key = Request.Headers["X-Idempotency-Key"].ToString();

            if (string.IsNullOrWhiteSpace(Key)) {
                return BadRequest("Missing Idempotency Key");
            }

            var compositeKey = $"{userId}_{Key}";

            var locked = await _redisLockService.AcquireAsync(compositeKey, TimeSpan.FromSeconds(30));
            if (!locked)
                return Conflict("Request in progress");

            try
            {
                // DB control
                var existingPayment = await _paymentDBContext.Payments.FirstOrDefaultAsync(d => d.UserId == userId && d.IdempotencyKey == Key);
                if (existingPayment != null)
                {
                    return Ok(existingPayment);
                }

                //Yeni kayıt
                var payment = new Payment()
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = amount,
                    IdempotencyKey = Key,
                    Status = "Processing",
                    CreatedAt = DateTime.UtcNow

                };

                _paymentDBContext.Payments.Add(payment);
                await _paymentDBContext.SaveChangesAsync();

                // External Provider Çağır
                var success = await _paymentProvider.ChargeAsync(amount);

                payment.Status = success ? "Success" : "Failed";

                await _paymentDBContext.SaveChangesAsync();

                return Ok(payment);

                    
            }
            catch(DbUpdateConcurrencyException dbex)
            {

            }
            catch(DbUpdateException dbue)
            {
                //Unique constraint yakalandı 

                var existing = await _paymentDBContext.Payments.FirstAsync
                    (d => d.UserId == userId && d.IdempotencyKey == Key);
                
                return Ok(existing);
            }
            catch (Exception ex)
            {

                throw;
            }
            finally
            {
                await _redisLockService.ReleaseAsync(compositeKey, TimeSpan.FromSeconds(15));
            }


            return Ok();

        }

    }
}
