using AutoLeaseNet.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Incident"/>. One table, no child collections —
/// claim attachments / photos arrive with the Storage adapter workstream later.
/// Column list mirrors Spec 01 §5.6.
/// </summary>
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.VehicleId).IsRequired();
        builder.Property(i => i.LeaseId);
        builder.Property(i => i.ReportedByPersonId).IsRequired();
        builder.Property(i => i.ReplacementLeaseId);

        builder.Property(i => i.Type).HasConversion<int>().IsRequired();
        builder.Property(i => i.Severity).HasConversion<int>().IsRequired();
        builder.Property(i => i.Status).HasConversion<int>().IsRequired();
        builder.Property(i => i.RequiresReplacement).IsRequired();

        builder.Property(i => i.ReportedAtUtc).IsRequired();
        builder.Property(i => i.IncidentTimeUtc).IsRequired();
        builder.Property(i => i.InvestigationStartedAtUtc);
        builder.Property(i => i.ResolvedAtUtc);
        builder.Property(i => i.ClosedAtUtc);

        builder.Property(i => i.LocationLat).HasPrecision(9, 6);
        builder.Property(i => i.LocationLng).HasPrecision(9, 6);
        builder.Property(i => i.LocationDescription).HasMaxLength(500);

        builder.Property(i => i.Description).IsRequired();
        builder.Property(i => i.PoliceReportNumber).HasMaxLength(50);
        builder.Property(i => i.InsuranceClaimNumber).HasMaxLength(50);
        builder.Property(i => i.ResolutionNotes).HasMaxLength(1000);

        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.UpdatedAtUtc).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();

        // Indexes — every read filters by TenantId; common drill-downs by LeaseId or
        // VehicleId, ordered by ReportedAtUtc DESC. Open-incidents dashboard hits Status.
        builder.HasIndex(i => new { i.TenantId, i.LeaseId, i.ReportedAtUtc });
        builder.HasIndex(i => new { i.TenantId, i.VehicleId, i.ReportedAtUtc });
        builder.HasIndex(i => new { i.TenantId, i.Status });

        builder.Ignore(i => i.DomainEvents);
    }
}
