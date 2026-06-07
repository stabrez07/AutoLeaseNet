using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="Quotation"/> aggregate + its <see cref="QuotationLine"/>
/// and <see cref="QuotationApproval"/> child collections. Column list mirrors Spec 01 §5.4.
/// Money is <c>DECIMAL(18,2)</c>, percentages <c>DECIMAL(5,2)</c>. Children are owned by the
/// root and navigated via their private backing fields (<c>_lines</c> / <c>_approvals</c>).
/// RLS for all three tables (+ <see cref="ApprovalTier"/>) is added in the
/// <c>Add_Quotation_Aggregate</c> migration — internal-only in Phase 1 (Spec 01 §3.4).
/// </summary>
public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.TenantId).IsRequired();
        builder.Property(q => q.QuoteNumber).HasMaxLength(30).IsRequired();
        builder.Property(q => q.CustomerId).IsRequired();
        builder.Property(q => q.AccountManagerId).IsRequired();

        builder.Property(q => q.Status).HasConversion<int>().IsRequired();
        builder.Property(q => q.QuoteDate).IsRequired();
        builder.Property(q => q.ValidUntilDate).IsRequired();
        builder.Property(q => q.ContractType).HasConversion<int>().IsRequired();
        builder.Property(q => q.EstimatedDurationMonths).IsRequired();
        builder.Property(q => q.TermsAndConditionsMd);

        builder.Property(q => q.SubTotalSar).HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(q => q.VatSar).HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.TotalSar).HasPrecision(18, 2).IsRequired();

        builder.Property(q => q.SubmittedAtUtc);
        builder.Property(q => q.ApprovedAtUtc);
        builder.Property(q => q.SentAtUtc);
        builder.Property(q => q.AcceptedAtUtc);
        builder.Property(q => q.ClosedAtUtc);
        builder.Property(q => q.PdfBlobUri).HasMaxLength(500);
        builder.Property(q => q.AcceptedByCustomerSignature);

        builder.Property(q => q.CreatedAtUtc).IsRequired();
        builder.Property(q => q.UpdatedAtUtc).IsRequired();
        builder.Property(q => q.RowVersion).IsRowVersion();

        // QuoteNumber is unique within a tenant; pipeline + inbox views filter by status.
        builder.HasIndex(q => new { q.TenantId, q.QuoteNumber }).IsUnique();
        builder.HasIndex(q => new { q.TenantId, q.Status });
        builder.HasIndex(q => new { q.TenantId, q.CustomerId });

        builder.HasMany(q => q.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Approvals)
            .WithOne()
            .HasForeignKey(a => a.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(q => q.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(q => q.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(q => q.DomainEvents);
    }
}

public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.QuotationId).IsRequired();
        builder.Property(l => l.LineNumber).IsRequired();
        builder.Property(l => l.ItemType).HasConversion<int>().IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.VehicleSpecRef).HasMaxLength(100);
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.UnitPriceSar).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(l => l.LineTotalSar).HasPrecision(18, 2).IsRequired();

        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();
        builder.Property(l => l.RowVersion).IsRowVersion();

        builder.HasIndex(l => new { l.TenantId, l.QuotationId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.DomainEvents);
    }
}

public sealed class QuotationApprovalConfiguration : IEntityTypeConfiguration<QuotationApproval>
{
    public void Configure(EntityTypeBuilder<QuotationApproval> builder)
    {
        builder.ToTable("QuotationApprovals");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.QuotationId).IsRequired();
        builder.Property(a => a.TierLevel).IsRequired();
        builder.Property(a => a.RequiredRoleCode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AssignedUserId);
        builder.Property(a => a.Status).HasConversion<int>().IsRequired();
        builder.Property(a => a.DecisionAtUtc);
        builder.Property(a => a.DecidedByUserId);
        builder.Property(a => a.Comment).HasMaxLength(2000);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => new { a.TenantId, a.QuotationId, a.TierLevel }).IsUnique();
        // Approver-inbox query: pending rows for a role within a tenant.
        builder.HasIndex(a => new { a.TenantId, a.Status, a.RequiredRoleCode });
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class ApprovalTierConfiguration : IEntityTypeConfiguration<ApprovalTier>
{
    public void Configure(EntityTypeBuilder<ApprovalTier> builder)
    {
        builder.ToTable("ApprovalTiers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.TierLevel).IsRequired();
        builder.Property(t => t.RequiredRoleCode).HasMaxLength(50).IsRequired();
        builder.Property(t => t.MinAmountSar).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();

        // One config row per (tenant, tier); evaluator reads active tiers for a tenant.
        builder.HasIndex(t => new { t.TenantId, t.TierLevel }).IsUnique();
        builder.Ignore(t => t.DomainEvents);
    }
}
