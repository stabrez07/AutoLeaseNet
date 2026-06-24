using AutoLeaseNet.Domain.Billing;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Contracts;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.Outbox;
using AutoLeaseNet.Domain.Pricing;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Domain.Notifications;
using AutoLeaseNet.Domain.Webhooks;
using AutoLeaseNet.Domain.Zatca;
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
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractLine> ContractLines => Set<ContractLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleHistoryEvent> VehicleHistoryEvents => Set<VehicleHistoryEvent>();
    public DbSet<VehicleServiceRecord> VehicleServiceRecords => Set<VehicleServiceRecord>();
    public DbSet<VehicleImage> VehicleImages => Set<VehicleImage>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<RentPolicy> RentPolicies => Set<RentPolicy>();
    public DbSet<ExtendedCoverage> ExtendedCoverages => Set<ExtendedCoverage>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();
    public DbSet<InspectionDamageMarker> InspectionDamageMarkers => Set<InspectionDamageMarker>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ZatcaChainState> ZatcaChainStates => Set<ZatcaChainState>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationApproval> QuotationApprovals => Set<QuotationApproval>();
    public DbSet<ApprovalTier> ApprovalTiers => Set<ApprovalTier>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqStageHistory> RfqStageHistories => Set<RfqStageHistory>();
    public DbSet<RfqAttachment> RfqAttachments => Set<RfqAttachment>();
    public DbSet<PricingVersion> PricingVersions => Set<PricingVersion>();
    public DbSet<PricingFormulaDefinition> PricingFormulaDefinitions => Set<PricingFormulaDefinition>();
    public DbSet<PricingDiscountPolicy> PricingDiscountPolicies => Set<PricingDiscountPolicy>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AdvancePayment> AdvancePayments => Set<AdvancePayment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<ZatcaSubmission> ZatcaSubmissions => Set<ZatcaSubmission>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();
    public DbSet<AccountActivity> AccountActivities => Set<AccountActivity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutoLeaseNetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdvancePayment>(e =>
        {
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.Property(p => p.RemainingBalance).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<PaymentAllocation>(e =>
        {
            e.Property(p => p.AllocatedAmountSar).HasColumnType("decimal(18,2)");
        });

        // Performance indexes for hot query paths
        modelBuilder.Entity<Lease>(e =>
        {
            e.HasIndex(l => new { l.TenantId, l.CustomerId });
            e.HasIndex(l => new { l.TenantId, l.VehicleId });
            e.HasIndex(l => new { l.TenantId, l.ContractId });
            e.HasIndex(l => new { l.TenantId, l.Status });
        });
        modelBuilder.Entity<Contract>(e =>
        {
            e.HasIndex(ct => new { ct.TenantId, ct.CustomerId });
        });
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => new { i.TenantId, i.LeaseId });
            e.HasIndex(i => new { i.TenantId, i.CustomerId });
        });
        modelBuilder.Entity<Vehicle>(e =>
        {
            e.HasIndex(v => new { v.TenantId, v.Make, v.Model });
        });

        // DisplayId: auto-increment identity column on all entities.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var displayIdProp = entityType.FindProperty("DisplayId");
            if (displayIdProp is not null)
            {
                displayIdProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
            }
        }

        // Azure SQL Edge workaround: disable RowVersion concurrency tokens globally.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rvProp = entityType.FindProperty("RowVersion");
            if (rvProp is not null)
            {
                rvProp.IsConcurrencyToken = false;
                rvProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                rvProp.SetColumnType("varbinary(8)");
            }
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        FixUntrackedNavigationEntries();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FixUntrackedNavigationEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void FixUntrackedNavigationEntries()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State != EntityState.Modified) continue;

            foreach (var nav in entry.Navigations.Where(n => n.Metadata.IsCollection))
            {
                if (nav.CurrentValue is not System.Collections.IEnumerable items) continue;
                foreach (var item in items)
                {
                    var childEntry = Entry(item);
                    if (childEntry.State == EntityState.Modified)
                    {
                        var keyProps = childEntry.Metadata.FindPrimaryKey()?.Properties;
                        var keyProp = keyProps is { Count: > 0 } ? keyProps[0] : null;
                        if (keyProp is null) continue;
                        var keyValue = childEntry.Property(keyProp.Name).CurrentValue;
                        if (keyValue is Guid g && g != Guid.Empty)
                        {
                            var existsInDb = childEntry.GetDatabaseValues() is null;
                            if (existsInDb)
                                childEntry.State = EntityState.Added;
                        }
                    }
                }
            }
        }
    }
}
