namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Unit of Work port. Application layer uses this to commit changes across multiple repositories
/// atomically. Implementation in AutoLeaseNet.Infrastructure wraps EF Core DbContext.SaveChangesAsync.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
