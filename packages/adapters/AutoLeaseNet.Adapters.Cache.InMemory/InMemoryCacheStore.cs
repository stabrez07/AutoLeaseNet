using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Cache;
using AutoLeaseNet.Application.Ports.Idempotency;

namespace AutoLeaseNet.Adapters.Cache.InMemory;

public sealed class InMemoryCacheStore(IMemoryCache cache) : ICacheStore
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
        => Task.FromResult(cache.TryGetValue(key, out var v) ? v as T : null);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken ct) where T : class
    {
        var entry = cache.CreateEntry(key);
        if (ttl.HasValue) entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Value = value;
        entry.Dispose();
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct)
    {
        cache.Remove(key);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => Task.FromResult(cache.TryGetValue(key, out _));
}

public sealed class InMemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    public Task<T?> GetAsync<T>(IdempotencyKey key, CancellationToken ct) where T : class
    {
        return Task.FromResult(cache.TryGetValue($"idem:{key.Value}", out var v) && v is string json
            ? JsonSerializer.Deserialize<T>(json)
            : null);
    }

    public Task SetAsync<T>(IdempotencyKey key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        var entry = cache.CreateEntry($"idem:{key.Value}");
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Value = JsonSerializer.Serialize(value);
        entry.Dispose();
        return Task.CompletedTask;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, InMemoryCacheStore>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }
}
