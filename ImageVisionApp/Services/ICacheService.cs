namespace ImageVisionApp.Services
{
    /// <summary>
    /// Thin wrapper around IMemoryCache so caching logic is testable
    /// and swappable (e.g. to IDistributedCache/Redis later) without
    /// touching any calling code.
    /// </summary>
    public interface ICacheService
    {
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value);
    }
}
