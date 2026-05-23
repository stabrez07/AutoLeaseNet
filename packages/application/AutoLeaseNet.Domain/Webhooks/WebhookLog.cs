using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Webhooks;

/// <summary>
/// Audit + dedup row for every inbound vendor webhook (Tajeer / ZATCA / D365 / etc.).
/// Persisted before any business processing so we can replay, debug, and prove receipt
/// regardless of downstream outcomes (Spec 03 §12.3, CLAUDE.md §1 / §10).
///
/// <para>
/// Dedup is enforced by a unique index on (<see cref="Source"/>, <see cref="ExternalEventId"/>):
/// a second arrival with the same vendor event id is recognised as a duplicate and
/// short-circuited to <c>200 OK</c> so Tajeer doesn't retry.
/// </para>
/// </summary>
public sealed class WebhookLog : Entity
{
    /// <summary>Vendor that sent the webhook (e.g. <c>"TAJEER"</c>).</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Vendor-supplied event id (Tajeer's <c>id</c> field).</summary>
    public string ExternalEventId { get; private set; } = string.Empty;

    /// <summary>Vendor category (Tajeer: <c>contract</c> / <c>invoice</c> / <c>general</c>).</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>Vendor event type (Tajeer: e.g. <c>contract.create</c>).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Vendor reference id (Tajeer: <c>referenceId</c> — usually the contract number).</summary>
    public string? ReferenceId { get; private set; }

    /// <summary>Raw payload as received. Truncated retention policy applies (Spec 03 §12.5 — 90 days).</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Header secret/signature as sent — captured for audit even when validation passes.</summary>
    public string? Signature { get; private set; }

    /// <summary>True when the payload passed signature validation at receive time.</summary>
    public bool SignatureValid { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    /// <summary>Set when the worker / inline dispatcher finishes processing.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    /// <summary>Captured exception message if processing threw — null on success.</summary>
    public string? ProcessingError { get; private set; }

    private WebhookLog() { }

    /// <summary>
    /// Factory used by the BFF webhook endpoint immediately on receipt. <paramref name="tenantId"/>
    /// is the platform tenant the webhook belongs to (Phase 1 is single-tenant per environment;
    /// Week 2 will encode the tenant in the registered URL).
    /// </summary>
    public static WebhookLog Receive(
        Guid tenantId,
        string source,
        string externalEventId,
        string category,
        string eventType,
        string? referenceId,
        string payload,
        string? signature,
        bool signatureValid,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new WebhookLog
        {
            TenantId = tenantId,
            Source = source,
            ExternalEventId = externalEventId,
            Category = category,
            EventType = eventType,
            ReferenceId = referenceId,
            Payload = payload,
            Signature = signature,
            SignatureValid = signatureValid,
            ReceivedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        ProcessingError = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ProcessingError = error;
        UpdatedAtUtc = nowUtc;
    }
}
