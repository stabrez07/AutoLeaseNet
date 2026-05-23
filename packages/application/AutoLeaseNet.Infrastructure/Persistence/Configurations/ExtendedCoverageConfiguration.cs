using AutoLeaseNet.Domain.ExtendedCoverages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class ExtendedCoverageConfiguration : IEntityTypeConfiguration<ExtendedCoverage>
{
    public void Configure(EntityTypeBuilder<ExtendedCoverage> builder)
    {
        builder.ToTable("ExtendedCoverages");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.CoverageType).HasConversion<int>().IsRequired();

        builder.Property(c => c.Code).HasMaxLength(32).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(128).IsRequired();
        builder.Property(c => c.DescriptionEn).HasMaxLength(512);
        builder.Property(c => c.DescriptionAr).HasMaxLength(512);

        builder.Property(c => c.DailyRate).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.DeductibleAmount).HasPrecision(18, 2);

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.TajeerExtendedCoverageId }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.IsActive });

        builder.Ignore(c => c.DomainEvents);
    }
}
