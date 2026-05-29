using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Outbox;

/// <summary>
/// Transactional outbox row. Captured by <c>OutboxWriteInterceptor</c> in the same
/// EF transaction as the business state change, then asynchronously dispatched
/// by <c>OutboxDrainService</c> via MediatR. Per Spec 01 §2 principle #7 ("never
/// call external services inside a request transaction; write to OutboxEvent,
/// let a worker drain it"). At-least-once delivery; handlers must be idempotent.
///
/// <para>
/// Not RLS-protected — this is an integration table (parallel to <c>WebhookLog</c>).
/// The drain runs cross-tenant under SYSTEM and needs to see every tenant's rows.
/// </para>
/// </summary>
public sealed class OutboxEvent : Entity
{
    /// <summary>
    /// Assembly-qualified type name (without version/culture/key) of the original
    /// <see cref="IDomainEvent"/> implementation. Resolved by the drain via
    /// <c>Type.GetType(EventType, throwOnError: true)</c>.
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Serialized event payload (System.Text.Json).</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>Correlation id for tracing across handlers/aggregates. Phase 2.</summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>
    /// Earliest UTC time the drain may pick this row up. Equals <see cref="Entity.CreatedAtUtc"/>
    /// on capture; the drain advances it for backoff on handler failure.
    /// </summary>
    public DateTimeOffset AvailableAtUtc { get; private set; }

    /// <summary>Set on successful publish; the drain ignores rows with this set.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    /// <summary>Most recent failure message, truncated.</summary>
    public string? LastError { get; private set; }

    /// <summary>Total attempts made so far. Drains park rows once this hits the configured cap.</summary>
    public int Attempts { get; private set; }

    private OutboxEvent() { }

    /// <summary>
    /// Factory used by <c>OutboxWriteInterceptor</c>. <paramref name="eventType"/> is the
    /// assembly-qualified name of the original domain event;
    /// <paramref name="payloadJson"/> is its JSON serialization.
    /// </summary>
    public static OutboxEvent Capture(
        Guid tenantId,
        string eventType,
        string payloadJson,
        Guid? correlationId,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        return new OutboxEvent
        {
            TenantId = tenantId,
            EventType = eventType,
            PayloadJson = payloadJson,
            CorrelationId = correlationId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            Attempts = 0,
        };
    }

    /// <summary>Drain finished successfully. Idempotent on same-state re-entry.</summary>
    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        if (ProcessedAtUtc.HasValue) return;
        ProcessedAtUtc = nowUtc;
        LastError = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Drain handler threw. Increments <see cref="Attempts"/>, captures the message,
    /// pushes <see cref="AvailableAtUtc"/> out by the supplied backoff so the next
    /// poll cycle skips this row until the cooldown elapses.
    /// </summary>
    public void MarkFailed(string error, TimeSpan backoff, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentOutOfRangeException.ThrowIfLessThan(backoff, TimeSpan.Zero);

        // Truncate to fit the DB column without trimming useful context.
        LastError = error.Length > 2000 ? error[..2000] : error;
        Attempts += 1;
        AvailableAtUtc = nowUtc + backoff;
        UpdatedAtUtc = nowUtc;
    }
}
