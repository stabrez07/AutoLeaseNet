using AutoLeaseNet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class VehicleServiceRecordConfiguration : IEntityTypeConfiguration<VehicleServiceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleServiceRecord> builder)
    {
        builder.ToTable("VehicleServiceRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.VehicleId).IsRequired();
        builder.Property(r => r.Type).HasConversion<int>().IsRequired();
        builder.Property(r => r.ServiceCode).HasMaxLength(32).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Branch).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Technician).HasMaxLength(128).IsRequired();
        builder.Property(r => r.PartsReplacedRaw).HasMaxLength(1024).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1024);
        builder.Property(r => r.CostSar).HasPrecision(18, 2);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();

        builder.Ignore(r => r.PartsReplaced);
        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.RowVersion);

        builder.HasIndex(r => new { r.TenantId, r.VehicleId, r.ServicedAt });
    }
}
