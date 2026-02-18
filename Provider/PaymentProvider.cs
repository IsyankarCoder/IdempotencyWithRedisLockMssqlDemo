namespace IdempotencyWithRedisLockMssqlDemo.Provider
{
    public class FakePaymentProvider
        : IPaymentProvider
    {

        public FakePaymentProvider() { }

        public async Task<bool> ChargeAsync(decimal Amount)
        {
            await Task.Delay(3000);// Network similasyonu
            return true; //Her zman başarılı
        } 
    }
}
