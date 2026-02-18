using Microsoft.EntityFrameworkCore;

namespace IdempotencyWithRedisLockMssqlDemo.Database
{
    public class PaymentDBContext
        :DbContext
    {

        public PaymentDBContext(DbContextOptions<PaymentDBContext> dbContextOptions)
            :base(dbContextOptions) 
        {


        }

        // public DbSet<Payment> Payments { get; set; }  --Alttaki method la aynı 
        public DbSet<Payment> Payments => Set<Payment>();
    }
}
