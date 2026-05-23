using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence;

/// <summary>
/// Root EF Core DbContext for AutoLeaseNet. Aggregates are mapped via IEntityTypeConfiguration
/// implementations in Configurations/. Multi-tenancy is enforced at the database level via
/// Row-Level Security policies (see migrations/_rls).
/// </summary>
public class AutoLeaseNetDbContext(DbContextOptions<AutoLeaseNetDbContext> options) : DbContext(options)
{
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutoLeaseNetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
