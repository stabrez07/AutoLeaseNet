using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Sales;

public sealed class QuotationApprovalConfiguration : IEntityTypeConfiguration<QuotationApproval>
{
    public void Configure(EntityTypeBuilder<QuotationApproval> builder)
    {
        builder.ToTable("QuotationApprovals", "dbo");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.QuotationId).IsRequired();
        builder.Property(a => a.TierLevel).IsRequired();
        builder.Property(a => a.RequiredRoleCode).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AssignedUserId);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(a => a.DecisionAtUtc);
        builder.Property(a => a.DecidedByUserId);
        builder.Property(a => a.Comment).HasMaxLength(2000);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.RowVersion)
            .IsRowVersion()
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.QuotationId);
        builder.HasIndex(a => new { a.QuotationId, a.TierLevel }).IsUnique();

        builder.Ignore(a => a.DomainEvents);
    }
}
