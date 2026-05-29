using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfOutboxRepository(AutoLeaseNetDbContext db) : IOutboxRepository
{
    public void Add(OutboxEvent outboxEvent)
    {
        ArgumentNullException.ThrowIfNull(outboxEvent);
        db.Set<OutboxEvent>().Add(outboxEvent);
    }

    public async Task<IReadOnlyList<OutboxEvent>> GetDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        int maxAttempts,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        return await db.Set<OutboxEvent>()
            .Where(o => o.ProcessedAtUtc == null
                && o.AvailableAtUtc <= nowUtc
                && o.Attempts < maxAttempts)
            .OrderBy(o => o.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
