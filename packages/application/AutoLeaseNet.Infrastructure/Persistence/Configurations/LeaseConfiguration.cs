using AutoLeaseNet.Domain.Leases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Lease"/>. Carries the full BI-relevant attribute set
/// expanded in Day A (see Plans/workstreams/2026-05-24-domain-deepening-production-seed).
/// FK relationships to Customer / Vehicle / Driver / Branch / RentPolicy /
/// ExtendedCoverage are configured with <see cref="DeleteBehavior.NoAction"/> — referenced
/// aggregates are never cascaded; closing a Lease must precede closing a Customer etc.
///
/// RLS policy on <c>TenantId</c> arrives in Week 2 Day 9.
/// </summary>
public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.ToTable("Leases");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired();

        // References (nullable until Day D wires domain lookups in SaveContract)
        builder.Property(l => l.CustomerId);
        builder.Property(l => l.ContractId);
        builder.Property(l => l.VehicleId);
        builder.Property(l => l.PrimaryDriverId);
        builder.Property(l => l.ExtraDriverId);
        builder.Property(l => l.AuthorizedDriverId);
        builder.Property(l => l.RentPolicyId);
        builder.Property(l => l.ExtendedCoverageId);
        builder.Property(l => l.WorkingBranchId);
        builder.Property(l => l.ReceiveBranchId);
        builder.Property(l => l.ReturnBranchId);

        // Tajeer system-of-record refs
        builder.Property(l => l.TajeerContractNumber);
        builder.Property(l => l.TajeerIssuanceToken).HasMaxLength(128);
        builder.Property(l => l.IssuanceUrl).HasMaxLength(1024);
        builder.Property(l => l.TajeerWorkingBranchId);
        builder.Property(l => l.TajeerReceiveBranchId);
        builder.Property(l => l.TajeerReturnBranchId);
        builder.Property(l => l.TajeerRentPolicyId);
        builder.Property(l => l.TajeerExtendedCoverageId);
        builder.Property(l => l.TajeerOperatorId);

        // Contract terms
        builder.Property(l => l.ContractTypeCode).IsRequired();
        builder.Property(l => l.ContractStartUtc).IsRequired();
        builder.Property(l => l.ContractEndUtc).IsRequired();
        builder.Property(l => l.ActualReturnUtc);
        builder.Property(l => l.AllowedKmPerHour);
        builder.Property(l => l.AllowedKmPerDay);
        builder.Property(l => l.UnlimitedKm);
        builder.Property(l => l.AllowedLateHours);

        // Payment snapshot
        builder.Property(l => l.RentAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.PaidAmount).HasPrecision(18, 2);
        builder.Property(l => l.RemainingAmount).HasPrecision(18, 2);
        builder.Property(l => l.VatAmount).HasPrecision(18, 2);
        builder.Property(l => l.TotalAmount).HasPrecision(18, 2);
        builder.Property(l => l.PaymentMethodCode);
        builder.Property(l => l.DiscountType);
        builder.Property(l => l.DiscountValue).HasPrecision(18, 2);

        // Issuance / return snapshots
        builder.Property(l => l.StartKm);
        builder.Property(l => l.StartFuelLevelCode);
        builder.Property(l => l.IssuanceConditionNotes).HasMaxLength(1024);
        builder.Property(l => l.EndKm);
        builder.Property(l => l.ReturnFuelLevelCode);
        builder.Property(l => l.ReturnConditionNotes).HasMaxLength(1024);
        builder.Property(l => l.DamagesObserved).HasMaxLength(2048);

        // Lifecycle
        builder.Property(l => l.Status).HasConversion<int>().IsRequired();
        builder.Property(l => l.SavedAtUtc);
        builder.Property(l => l.IssuedAtUtc);
        builder.Property(l => l.SuspendedAtUtc);
        builder.Property(l => l.ResumedAtUtc);
        builder.Property(l => l.ClosedAtUtc);
        builder.Property(l => l.CancelledAtUtc);
        builder.Property(l => l.ExpiredAtUtc);
        builder.Property(l => l.ExtensionCount);
        builder.Property(l => l.SuspensionReasonCode);
        builder.Property(l => l.ClosureMainReasonCode);
        builder.Property(l => l.ClosureSubReasonCode);
        builder.Property(l => l.CancellationReason).HasMaxLength(512);
        builder.Property(l => l.SaveFailureReason).HasMaxLength(256);
        builder.Property(l => l.PiiOptedOut);

        // Audit
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();

        // Indexes — every read filters by TenantId; webhook lookup is by Tajeer contract number.
        builder.HasIndex(l => new { l.TenantId, l.Status });
        builder.HasIndex(l => new { l.TenantId, l.TajeerContractNumber })
            .IsUnique()
            .HasFilter("[TajeerContractNumber] IS NOT NULL");
        builder.HasIndex(l => new { l.TenantId, l.CustomerId });
        builder.HasIndex(l => new { l.TenantId, l.VehicleId });
        builder.HasIndex(l => new { l.TenantId, l.ContractStartUtc });
        builder.HasIndex(l => new { l.TenantId, l.ContractEndUtc });

        builder.Ignore(l => l.DomainEvents);
    }
}
