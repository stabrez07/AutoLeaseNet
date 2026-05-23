using AutoLeaseNet.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(32).IsRequired();
        builder.Property(b => b.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(b => b.NameAr).HasMaxLength(128).IsRequired();
        builder.Property(b => b.CityEn).HasMaxLength(64);
        builder.Property(b => b.CityAr).HasMaxLength(64);
        builder.Property(b => b.RegionEn).HasMaxLength(64);
        builder.Property(b => b.RegionAr).HasMaxLength(64);
        builder.Property(b => b.LicenseNumber).HasMaxLength(64);
        builder.Property(b => b.Address).HasMaxLength(256);
        builder.Property(b => b.Latitude).HasPrecision(10, 7);
        builder.Property(b => b.Longitude).HasPrecision(10, 7);
        builder.Property(b => b.PhoneNumber).HasMaxLength(32);
        builder.Property(b => b.WorkingHoursJson).HasMaxLength(2048);

        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.UpdatedAtUtc).IsRequired();
        builder.Property(b => b.RowVersion).IsRowVersion();

        builder.HasIndex(b => new { b.TenantId, b.Code }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.TajeerBranchId }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.IsActive });

        builder.Ignore(b => b.DomainEvents);
    }
}
