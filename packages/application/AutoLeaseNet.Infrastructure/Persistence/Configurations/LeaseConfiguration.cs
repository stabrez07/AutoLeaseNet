using AutoLeaseNet.Domain.Leases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Lease"/>. Picked up automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AutoLeaseNetDbContext"/>.
///
/// RLS policy on <c>TenantId</c> arrives in Week 2 Day 9 (Spec 01 §3 — defence in depth).
/// </summary>
public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.ToTable("Leases");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.CustomerId);
        builder.Property(l => l.TajeerContractNumber);

        builder.Property(l => l.IssuanceUrl)
            .HasMaxLength(1024);

        builder.Property(l => l.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();
        builder.Property(l => l.IssuedAtUtc);

        builder.Property(l => l.RowVersion).IsRowVersion();

        // Multi-tenant indexes — every read filters by TenantId, every Tajeer lookup
        // hits TajeerContractNumber. Unique index on (TenantId, TajeerContractNumber)
        // because the same contract number must never appear twice within a tenant.
        builder.HasIndex(l => new { l.TenantId, l.Status });
        builder.HasIndex(l => new { l.TenantId, l.TajeerContractNumber })
            .IsUnique()
            .HasFilter("[TajeerContractNumber] IS NOT NULL");

        // Domain-events collection is not persisted — it's published by the saga, not the DB.
        builder.Ignore(l => l.DomainEvents);
    }
}
