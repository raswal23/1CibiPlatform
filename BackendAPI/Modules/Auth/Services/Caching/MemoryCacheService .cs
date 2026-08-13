using Microsoft.Extensions.Caching.Memory;

namespace Auth.Services;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached!;

        var result = await factory();

        _cache.Set(key, result, expiry ?? TimeSpan.FromMinutes(5));

        return result;
    }

    public void Remove(string key) => _cache.Remove(key);
}

