
using IdempotencyWithRedisLockMssqlDemo.Database;
using IdempotencyWithRedisLockMssqlDemo.Provider;
using IdempotencyWithRedisLockMssqlDemo.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Scalar;
using Scalar.AspNetCore;
using IdempotencyWithRedisLockMssqlDemo.Helper;
using IdempotencyWithRedisLockMssqlDemo.Middleware;

namespace IdempotencyWithRedisLockMssqlDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();



            builder.Services.AddDbContext<PaymentDBContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
            });

            //builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6380"));

            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse("localhost:6380");
                configuration.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(configuration);
            });

            builder.Services.AddSingleton<IRedisLockService, RedisLockService>();
            builder.Services.AddSingleton<IRedisIdempotencyStore, RedisIdempotencyStore>();
            builder.Services.AddScoped<IPaymentProvider, FakePaymentProvider>();
            builder.Services.AddScoped<IdempotencyHashService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
               
            }

            app.UseMiddleware<RedisIdempotencyMiddleware>();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            
            app.MapControllers();

            app.Run();
        }
    }
}
