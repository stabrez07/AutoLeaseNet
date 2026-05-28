using AutoLeaseNet.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Inspection"/> + child collections (photos + damage
/// markers). Spec 01 §5.6 column list is preserved column-for-column so the schema can
/// be inspected against the spec without translation. RLS policy on <c>TenantId</c>
/// arrives in Week 2 Day 9 alongside Lease / Vehicle / etc.
/// </summary>
public sealed class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> builder)
    {
        builder.ToTable("Inspections");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.VehicleId).IsRequired();
        builder.Property(i => i.LeaseId);

        builder.Property(i => i.Type).HasConversion<int>().IsRequired();
        builder.Property(i => i.Status).HasConversion<int>().IsRequired();

        builder.Property(i => i.PerformedByUserId).IsRequired();
        builder.Property(i => i.PerformedAtUtc).IsRequired();
        builder.Property(i => i.CompletedAtUtc);
        builder.Property(i => i.AbandonedAtUtc);
        builder.Property(i => i.AbandonedReason).HasMaxLength(512);

        builder.Property(i => i.OdometerKm).IsRequired();
        builder.Property(i => i.FuelLevel).HasConversion<byte>().IsRequired();

        builder.Property(i => i.AcCondition);
        builder.Property(i => i.RadioStereoCondition);
        builder.Property(i => i.ScreenCondition);
        builder.Property(i => i.SpeedometerCondition);
        builder.Property(i => i.KeysCondition);
        builder.Property(i => i.CarSeatsCondition);
        builder.Property(i => i.SafetyTriangleCondition);
        builder.Property(i => i.FireExtinguisherCondition);
        builder.Property(i => i.FirstAidKitCondition);
        builder.Property(i => i.SpareTireToolsCondition);
        builder.Property(i => i.TiresCondition);
        builder.Property(i => i.SpareTireCondition);

        builder.Property(i => i.Other1).HasMaxLength(200);
        builder.Property(i => i.Other2).HasMaxLength(200);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.SketchInfoJson);
        builder.Property(i => i.RenterSignatureBlobUri).HasMaxLength(500);

        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.UpdatedAtUtc).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();

        // Indexes — every read filters by TenantId; lease-detail page filters by LeaseId;
        // vehicle-history page filters by VehicleId ordered by PerformedAtUtc DESC.
        builder.HasIndex(i => new { i.TenantId, i.LeaseId, i.Type });
        builder.HasIndex(i => new { i.TenantId, i.VehicleId, i.PerformedAtUtc });
        builder.HasIndex(i => new { i.TenantId, i.Status });

        // Owned child collections — append-only while IN_PROGRESS, navigation backed by
        // the private List<T> on the aggregate via the backing-field convention.
        builder.HasMany(i => i.Photos)
            .WithOne()
            .HasForeignKey(p => p.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.DamageMarkers)
            .WithOne()
            .HasForeignKey(m => m.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.DamageMarkers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(i => i.DomainEvents);
    }
}

public sealed class InspectionPhotoConfiguration : IEntityTypeConfiguration<InspectionPhoto>
{
    public void Configure(EntityTypeBuilder<InspectionPhoto> builder)
    {
        builder.ToTable("InspectionPhotos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.InspectionId).IsRequired();
        builder.Property(p => p.BlobUri).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Sequence).IsRequired();
        builder.Property(p => p.AiDamageDetectionJson);

        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => new { p.TenantId, p.InspectionId, p.Sequence }).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class InspectionDamageMarkerConfiguration : IEntityTypeConfiguration<InspectionDamageMarker>
{
    public void Configure(EntityTypeBuilder<InspectionDamageMarker> builder)
    {
        builder.ToTable("InspectionDamageMarkers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.InspectionId).IsRequired();
        builder.Property(m => m.Type).HasConversion<int>().IsRequired();
        builder.Property(m => m.PositionX).HasPrecision(8, 4).IsRequired();
        builder.Property(m => m.PositionY).HasPrecision(8, 4).IsRequired();

        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.UpdatedAtUtc).IsRequired();
        builder.Property(m => m.RowVersion).IsRowVersion();

        builder.HasIndex(m => new { m.TenantId, m.InspectionId });
        builder.Ignore(m => m.DomainEvents);
    }
}
