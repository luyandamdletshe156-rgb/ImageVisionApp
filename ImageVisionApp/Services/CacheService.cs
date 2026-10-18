using Microsoft.Extensions.Caching.Memory;

namespace ImageVisionApp.Services
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly int _expirationMinutes;

        public CacheService(IMemoryCache cache, IConfiguration config, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
            _expirationMinutes = config.GetValue<int?>("CacheSettings:AbsoluteExpirationMinutes") ?? 60;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            var hit = _cache.TryGetValue(key, out value);
            _logger.LogInformation(hit ? "Cache HIT for {Key}" : "Cache MISS for {Key}", key);
            return hit;
        }

        public void Set<T>(string key, T value)
        {
            _cache.Set(key, value, TimeSpan.FromMinutes(_expirationMinutes));
        }
    }
}
