namespace IdempotencyWithRedisLockMssqlDemo.Provider
{
    public interface IPaymentProvider
    {
        Task<bool> ChargeAsync(decimal Amount);
    }
}
