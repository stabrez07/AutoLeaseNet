using AutoLeaseNet.Domain.ExtendedCoverages;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IExtendedCoverageRepository
{
    void Add(ExtendedCoverage coverage);
    Task<ExtendedCoverage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ExtendedCoverage?> GetByTajeerIdAsync(Guid tenantId, int tajeerId, CancellationToken ct);
}
