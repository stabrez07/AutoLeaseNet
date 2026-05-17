namespace AutoLeaseNet.Application.Ports.Idempotency;

/// <summary>
/// Stores client-supplied idempotency keys and their cached responses for replay.
/// Per doc 02 §7 and doc 03 §10 — used by BFF (request idempotency) and Tajeer adapter (call dedup).
/// Implementations: Adapters.Cache.Redis (production), Adapters.Cache.InMemory (tests/dev).
/// </summary>
public interface IIdempotencyStore
{
    Task<T?> GetAsync<T>(IdempotencyKey key, CancellationToken ct) where T : class;
    Task SetAsync<T>(IdempotencyKey key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
}

public sealed record IdempotencyKey(string Value)
{
    public static IdempotencyKey New() => new(Guid.NewGuid().ToString("N"));
    public static IdempotencyKey For(string aggregateType, Guid aggregateId, string operation)
        => new($"{aggregateType}:{aggregateId}:{operation}");
}
