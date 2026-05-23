using AutoLeaseNet.Domain.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.TammAuthorizationStatus).HasConversion<int>().IsRequired();

        builder.Property(d => d.PersonNameEn).HasMaxLength(256).IsRequired();
        builder.Property(d => d.PersonNameAr).HasMaxLength(256);
        builder.Property(d => d.PersonIdNumber).HasMaxLength(64).IsRequired();
        builder.Property(d => d.NationalityCode).HasMaxLength(8);
        builder.Property(d => d.DriverLicenseNumber).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Mobile).HasMaxLength(32);
        builder.Property(d => d.Email).HasMaxLength(256);
        builder.Property(d => d.NationalAddress).HasMaxLength(256);
        builder.Property(d => d.TammAuthorizationRef).HasMaxLength(128);

        builder.Property(d => d.LicenseExpiryDate).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedAtUtc).IsRequired();
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasIndex(d => new { d.TenantId, d.Status });
        builder.HasIndex(d => new { d.TenantId, d.CustomerId });
        builder.HasIndex(d => new { d.TenantId, d.PersonIdNumber }).IsUnique();
        builder.HasIndex(d => new { d.TenantId, d.LicenseExpiryDate }); // "expiring in N days" report

        builder.Ignore(d => d.DomainEvents);
    }
}
