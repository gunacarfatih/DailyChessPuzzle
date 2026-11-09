using Microsoft.Extensions.Caching.Memory;
using ReusableCaching.Interfaces;
using ReusableCaching.Models;

namespace ReusableCaching.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly CacheSettings _settings;

        public MemoryCacheService(
            IMemoryCache memoryCache,
            Microsoft.Extensions.Options.IOptions<CacheSettings> options)
        {
            _memoryCache = memoryCache;
            _settings = options.Value;
        }

        public Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
        {
            _memoryCache.TryGetValue(key, out T? value);

            return Task.FromResult(value);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            var duration = expiration ??
                TimeSpan.FromMinutes(_settings.DurationMinutes);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };

            _memoryCache.Set(key, value, options);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            _memoryCache.Remove(key);

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_memoryCache.TryGetValue(key, out _));
        }
    }
}