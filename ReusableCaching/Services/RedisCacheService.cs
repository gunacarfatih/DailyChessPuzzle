using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ReusableCaching.Interfaces;
using ReusableCaching.Models;

namespace ReusableCaching.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly CacheSettings _settings;

        public RedisCacheService(
            IDistributedCache distributedCache,
            Microsoft.Extensions.Options.IOptions<CacheSettings> options)
        {
            _distributedCache = distributedCache;
            _settings = options.Value;
        }

        public async Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
        {

            var data = await _distributedCache.GetAsync(
                key, cancellationToken);

            if (data == null)
                return default;

            return JsonSerializer.Deserialize<T>(data);
        }

        public async Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(value);

            var duration = expiration ??
                TimeSpan.FromMinutes(_settings.DurationMinutes);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };

            await _distributedCache.SetAsync(
                key,
                data,
                options,
                cancellationToken);
        }

        public async Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            await _distributedCache.RemoveAsync(
                key, cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var data = await _distributedCache.GetAsync(
                key, cancellationToken);


            return data != null;
        }
    }
}


public class A
{
    public int Name { get; set; }
}