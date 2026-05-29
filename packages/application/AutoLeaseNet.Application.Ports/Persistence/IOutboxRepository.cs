using AutoLeaseNet.Domain.Outbox;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for <see cref="OutboxEvent"/>. The drain background service
/// pulls rows in FIFO order constrained by <see cref="OutboxEvent.AvailableAtUtc"/>
/// (so backoff'd rows wait) and <see cref="OutboxEvent.ProcessedAtUtc"/> (so
/// already-done rows are skipped).
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Add a freshly-captured row. Called from <c>OutboxWriteInterceptor</c> via the
    /// DbContext's change tracker, so this is effectively
    /// <c>ctx.Set&lt;OutboxEvent&gt;().Add(...)</c>.
    /// </summary>
    void Add(OutboxEvent outboxEvent);

    /// <summary>
    /// Fetch up to <paramref name="batchSize"/> rows whose
    /// <see cref="OutboxEvent.AvailableAtUtc"/> &lt;= <paramref name="nowUtc"/>,
    /// <see cref="OutboxEvent.ProcessedAtUtc"/> IS NULL, and
    /// <see cref="OutboxEvent.Attempts"/> &lt; <paramref name="maxAttempts"/>. Ordered
    /// by <see cref="Domain.Shared.Entity.CreatedAtUtc"/> so older rows drain first.
    /// </summary>
    Task<IReadOnlyList<OutboxEvent>> GetDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        int maxAttempts,
        CancellationToken ct);
}
