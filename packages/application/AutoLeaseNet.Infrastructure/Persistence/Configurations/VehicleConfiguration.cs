using AutoLeaseNet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId).IsRequired();
        builder.Property(v => v.Status).HasConversion<int>().IsRequired();
        builder.Property(v => v.FuelType).HasConversion<int>().IsRequired();
        builder.Property(v => v.TransmissionType).HasConversion<int>().IsRequired();
        builder.Property(v => v.BodyType).HasConversion<int>().IsRequired();

        builder.Property(v => v.PlateNumber).HasMaxLength(16).IsRequired();
        builder.Property(v => v.PlateLetters).HasMaxLength(16).IsRequired();
        builder.Property(v => v.Vin).HasMaxLength(64).IsRequired();
        builder.Property(v => v.EngineNumber).HasMaxLength(64);

        builder.Property(v => v.Make).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(32);
        builder.Property(v => v.InsuranceCompany).HasMaxLength(128);
        builder.Property(v => v.InsurancePolicyNumber).HasMaxLength(64);
        builder.Property(v => v.PurchaseInvoiceRef).HasMaxLength(64);
        builder.Property(v => v.TelematicsProvider).HasMaxLength(32);
        builder.Property(v => v.DeviceImei).HasMaxLength(32);
        builder.Property(v => v.Notes).HasMaxLength(1024);

        builder.Property(v => v.AllocatedToCustomerId);
        builder.Property(v => v.AllocatedToContractId);

        builder.Property(v => v.PurchasePrice).HasPrecision(18, 2);
        builder.Property(v => v.DepreciationPerMonth).HasPrecision(18, 2);
        builder.Property(v => v.CurrentBookValue).HasPrecision(18, 2);

        builder.Property(v => v.CreatedAtUtc).IsRequired();
        builder.Property(v => v.UpdatedAtUtc).IsRequired();
        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasIndex(v => new { v.TenantId, v.Status });
        builder.HasIndex(v => new { v.TenantId, v.CurrentBranchId });
        builder.HasIndex(v => new { v.TenantId, v.PlateNumber, v.PlateLetters }).IsUnique();
        builder.HasIndex(v => new { v.TenantId, v.Vin }).IsUnique();

        builder.Ignore(v => v.DomainEvents);
    }
}
