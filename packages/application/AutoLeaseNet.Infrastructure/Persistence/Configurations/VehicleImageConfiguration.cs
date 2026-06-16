using AutoLeaseNet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

public sealed class VehicleImageConfiguration : IEntityTypeConfiguration<VehicleImage>
{
    public void Configure(EntityTypeBuilder<VehicleImage> builder)
    {
        builder.ToTable("VehicleImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.VehicleId).IsRequired();
        builder.Property(i => i.ImageUrl).HasMaxLength(1024).IsRequired();
        builder.Property(i => i.ThumbnailUrl).HasMaxLength(1024);
        builder.Property(i => i.AltText).HasMaxLength(256);
        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.UpdatedAtUtc).IsRequired();

        builder.Ignore(i => i.DomainEvents);
        builder.Ignore(i => i.RowVersion);

        builder.HasIndex(i => new { i.TenantId, i.VehicleId, i.SortOrder });
    }
}
