using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Sales;

public sealed class ApprovalTierConfiguration : IEntityTypeConfiguration<ApprovalTier>
{
    public void Configure(EntityTypeBuilder<ApprovalTier> builder)
    {
        builder.ToTable("ApprovalTiers", "dbo");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.TierLevel).IsRequired();
        builder.Property(t => t.RequiredRoleCode).HasMaxLength(100).IsRequired();
        builder.Property(t => t.MinAmountSar).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => new { t.TenantId, t.TierLevel }).IsUnique();

        builder.Ignore(t => t.DomainEvents);
    }
}
