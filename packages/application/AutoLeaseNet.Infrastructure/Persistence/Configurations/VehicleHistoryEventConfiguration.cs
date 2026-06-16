using AutoLeaseNet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class VehicleHistoryEventConfiguration : IEntityTypeConfiguration<VehicleHistoryEvent>
{
    public void Configure(EntityTypeBuilder<VehicleHistoryEvent> builder)
    {
        builder.ToTable("VehicleHistoryEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.VehicleId).IsRequired();
        builder.Property(e => e.EventType).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512).IsRequired();
        builder.Property(e => e.PreviousValue).HasMaxLength(256);
        builder.Property(e => e.NewValue).HasMaxLength(256);
        builder.Property(e => e.PerformedByName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.VehicleId, e.CreatedAtUtc });

        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.RowVersion);
    }
}
