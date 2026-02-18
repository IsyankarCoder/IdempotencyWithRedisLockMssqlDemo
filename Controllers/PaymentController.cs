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
        public async Task<IActionResult> Post([FromBody] decimal Amount, 
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
    
                };
            }
            catch (Exception ex)
            {

                throw;
            }


            return Ok();

        }

    }
}
