using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PricingFormulaDefinitionConfiguration : IEntityTypeConfiguration<PricingFormulaDefinition>
{
    public void Configure(EntityTypeBuilder<PricingFormulaDefinition> builder)
    {
        builder.ToTable("PricingFormulaDefinitions", "dbo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Expression).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.OutputField).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Precision).IsRequired();
        builder.Property(x => x.RoundingMode).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });

        builder.Ignore(x => x.DomainEvents);
    }
}
