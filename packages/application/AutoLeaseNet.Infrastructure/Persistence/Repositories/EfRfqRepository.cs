using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfRfqRepository(AutoLeaseNetDbContext db) : IRfqRepository
{
    public void Add(Rfq rfq)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        db.Rfqs.Add(rfq);
    }

    public Task<Rfq?> GetByIdAsync(Guid tenantId, Guid rfqId, CancellationToken ct)
    {
        return db.Rfqs
            .Include(r => r.StageHistory)
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);
    }

    public async Task<IReadOnlyList<Rfq>> GetByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct)
    {
        return await db.Rfqs
            .Where(r => r.TenantId == tenantId && r.CustomerId == customerId)
            .Include(r => r.StageHistory)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> GetNextSequenceAsync(Guid tenantId, CancellationToken ct)
    {
        var max = await db.Rfqs
            .Where(r => r.TenantId == tenantId)
            .CountAsync(ct)
            .ConfigureAwait(false);
        return max + 1;
    }
}
