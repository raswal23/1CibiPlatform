namespace Auth.Services;

public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
    void Remove(string key);
}
