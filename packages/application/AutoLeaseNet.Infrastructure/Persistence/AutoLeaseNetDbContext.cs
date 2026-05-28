using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Vehicles;
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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<RentPolicy> RentPolicies => Set<RentPolicy>();
    public DbSet<ExtendedCoverage> ExtendedCoverages => Set<ExtendedCoverage>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();
    public DbSet<InspectionDamageMarker> InspectionDamageMarkers => Set<InspectionDamageMarker>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutoLeaseNetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
