namespace AutoLeaseNet.Application.Ports.Cache;

/// <summary>
/// Generic cache port. Per doc 04 §3.1.
/// Implementations: Adapters.Cache.Redis, Adapters.Cache.InMemory.
/// </summary>
public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct) where T : class;
    Task<bool> RemoveAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
