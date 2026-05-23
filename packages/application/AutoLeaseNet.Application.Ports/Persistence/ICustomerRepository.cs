using AutoLeaseNet.Domain.Customers;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface ICustomerRepository
{
    void Add(Customer customer);
    Task<Customer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> AnyAsync(Guid tenantId, CancellationToken ct);
}
