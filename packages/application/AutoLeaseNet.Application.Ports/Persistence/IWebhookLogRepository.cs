using AutoLeaseNet.Domain.Webhooks;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for <see cref="WebhookLog"/>. The unique index on
/// (<c>Source</c>, <c>ExternalEventId</c>) does the dedup heavy-lifting — duplicate
/// arrivals surface as <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>;
/// receivers translate that to <c>200 OK</c> so Tajeer doesn't retry.
/// </summary>
public interface IWebhookLogRepository
{
    void Add(WebhookLog log);

    /// <summary>Cross-tenant dedup probe — used optimistically before the insert attempt.</summary>
    Task<bool> ExistsAsync(string source, string externalEventId, CancellationToken ct);
}
