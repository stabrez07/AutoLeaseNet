using AutoLeaseNet.Domain.Drivers;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IDriverRepository
{
    void Add(Driver driver);
    Task<Driver?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Driver?> GetByIdNumberAsync(Guid tenantId, string personIdNumber, CancellationToken ct);
    Task<(IReadOnlyList<Driver> Items, int TotalCount)> GetPagedAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct);
}
