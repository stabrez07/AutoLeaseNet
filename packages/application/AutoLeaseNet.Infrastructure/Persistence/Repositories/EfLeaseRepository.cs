using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Leases;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ILeaseRepository"/>.</summary>
public sealed class EfLeaseRepository(AutoLeaseNetDbContext db) : ILeaseRepository
{
    public void Add(Lease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        db.Leases.Add(lease);
    }

    public Task<Lease?> GetByTajeerContractNumberAsync(
        Guid tenantId,
        long tajeerContractNumber,
        CancellationToken ct)
    {
        return db.Leases.SingleOrDefaultAsync(
            l => l.TenantId == tenantId && l.TajeerContractNumber == tajeerContractNumber,
            ct);
    }
}
