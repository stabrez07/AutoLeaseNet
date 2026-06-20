using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PricingVersionConfiguration : IEntityTypeConfiguration<PricingVersion>
{
    public void Configure(EntityTypeBuilder<PricingVersion> builder)
    {
        builder.ToTable("PricingVersions", "dbo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.EffectiveFromUtc).IsRequired();
        builder.Property(x => x.EffectiveToUtc);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.EffectiveFromUtc });

        builder.Ignore(x => x.DomainEvents);
    }
}
