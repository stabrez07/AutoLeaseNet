using AutoLeaseNet.Domain.RentPolicies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class RentPolicyConfiguration : IEntityTypeConfiguration<RentPolicy>
{
    public void Configure(EntityTypeBuilder<RentPolicy> builder)
    {
        builder.ToTable("RentPolicies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(32).IsRequired();
        builder.Property(p => p.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(p => p.NameAr).HasMaxLength(128).IsRequired();
        builder.Property(p => p.DescriptionEn).HasMaxLength(512);
        builder.Property(p => p.DescriptionAr).HasMaxLength(512);

        builder.Property(p => p.BaseDailyRate).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.BaseHourlyRate).HasPrecision(18, 2);
        builder.Property(p => p.LateHourFee).HasPrecision(18, 2);
        builder.Property(p => p.ExtraKmFee).HasPrecision(18, 4);
        builder.Property(p => p.SecurityDeposit).HasPrecision(18, 2);

        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.TajeerRentPolicyId }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.IsActive });

        builder.Ignore(p => p.DomainEvents);
    }
}
