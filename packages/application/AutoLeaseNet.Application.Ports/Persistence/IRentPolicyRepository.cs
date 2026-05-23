using AutoLeaseNet.Domain.RentPolicies;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IRentPolicyRepository
{
    void Add(RentPolicy policy);
    Task<RentPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<RentPolicy?> GetByTajeerRentPolicyIdAsync(Guid tenantId, int tajeerId, CancellationToken ct);
}
