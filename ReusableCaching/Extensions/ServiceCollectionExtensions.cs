using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReusableCaching.Interfaces;
using ReusableCaching.Models;
using ReusableCaching.Services;

namespace ReusableCaching.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddReusableCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var settings = configuration
                .GetSection("CacheSettings")
                .Get<CacheSettings>() ?? new CacheSettings();
            services.Configure<CacheSettings>(
                configuration.GetSection("CacheSettings"));

            switch (settings.Provider.Trim().ToLowerInvariant())
            {
                case "memory":

                    services.AddMemoryCache();

                    services.AddSingleton<
                        ICacheService,
                        MemoryCacheService>();

                    break;

                case "redis":

                    var redisConnection =
                        configuration.GetConnectionString("Redis");

                    if (string.IsNullOrWhiteSpace(redisConnection))
                    {
                        throw new InvalidOperationException(
                            "Redis bağlantı adresi bulunamadı.");
                    }

                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = redisConnection;
                        options.InstanceName = settings.InstanceName;
                    });

                    services.AddSingleton<
                        ICacheService,
                        RedisCacheService>();

                    break;

                default:

                    throw new InvalidOperationException(
                        $"Desteklenmeyen cache sağlayıcısı: {settings.Provider}");
            }

            return services;
        }
    }
}