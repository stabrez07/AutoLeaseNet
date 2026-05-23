using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfWebhookLogRepository(AutoLeaseNetDbContext db) : IWebhookLogRepository
{
    public void Add(WebhookLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        db.WebhookLogs.Add(log);
    }

    public Task<bool> ExistsAsync(string source, string externalEventId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);
        return db.WebhookLogs.AnyAsync(
            w => w.Source == source && w.ExternalEventId == externalEventId,
            ct);
    }
}
