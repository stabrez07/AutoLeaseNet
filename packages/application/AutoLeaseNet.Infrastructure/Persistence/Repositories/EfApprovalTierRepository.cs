using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfApprovalTierRepository(AutoLeaseNetDbContext db) : IApprovalTierRepository
{
    public void Add(ApprovalTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        db.ApprovalTiers.Add(tier);
    }

    public Task<bool> AnyAsync(Guid tenantId, CancellationToken ct)
    {
        return db.ApprovalTiers.AnyAsync(t => t.TenantId == tenantId, ct);
    }
}
