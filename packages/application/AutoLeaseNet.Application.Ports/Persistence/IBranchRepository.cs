using AutoLeaseNet.Domain.Branches;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IBranchRepository
{
    void Add(Branch branch);
    Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Branch?> GetByTajeerBranchIdAsync(Guid tenantId, int tajeerBranchId, CancellationToken ct);
}
