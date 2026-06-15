using AutoLeaseNet.Domain.Zatca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ZatcaSubmission aggregate.
/// Maps domain entity to database table with RLS, indexes, and constraints.
/// </summary>
public sealed class ZatcaSubmissionConfiguration : IEntityTypeConfiguration<ZatcaSubmission>
{
    public void Configure(EntityTypeBuilder<ZatcaSubmission> builder)
    {
        builder.ToTable("ZatcaSubmissions", schema: "dbo");
        builder.HasKey(z => z.Id);

        // Indexes
        builder.HasIndex(z => new { z.TenantId, z.InvoiceId })
            .IsUnique()
            .HasDatabaseName("IX_ZatcaSubmissions_TenantId_InvoiceId");

        builder.HasIndex(z => new { z.TenantId, z.ZatcaTransactionId })
            .HasDatabaseName("IX_ZatcaSubmissions_TenantId_ZatcaTransactionId");

        builder.HasIndex(z => new { z.TenantId, z.Status })
            .HasDatabaseName("IX_ZatcaSubmissions_TenantId_Status");

        // Properties
        builder.Property(z => z.TenantId)
            .IsRequired()
            .HasComment("Tenant ID for RLS isolation.");

        builder.Property(z => z.InvoiceId)
            .IsRequired()
            .HasComment("Reference to Invoice aggregate (1:1 relationship).");

        builder.Property(z => z.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasComment("Submission state per Spec 02 §4.5 (Draft, Submitted, Cleared, etc.).");

        builder.Property(z => z.UblXml)
            .HasMaxLength(int.MaxValue)
            .HasComment("Canonical UBL 2.1 XML (before signing).");

        builder.Property(z => z.SignedUblXml)
            .HasMaxLength(int.MaxValue)
            .HasComment("Signed UBL XML with ECDSA P-256 signature embedded.");

        builder.Property(z => z.InvoiceHash)
            .HasMaxLength(64)
            .HasComment("SHA-256 hash of canonical UBL (hex-encoded).");

        builder.Property(z => z.ZatcaTransactionId)
            .HasMaxLength(50)
            .HasComment("ZATCA-assigned transaction ID (returned on successful submission).");

        builder.Property(z => z.ZatcaReportingStatus)
            .HasMaxLength(50)
            .HasComment("ZATCA reporting status (e.g., 'CLEARED', 'REJECTED', 'QUEUED').");

        builder.Property(z => z.ClearedAtUtc)
            .HasComment("Timestamp when ZATCA confirmed clearance.");

        builder.Property(z => z.LastErrorMessage)
            .HasMaxLength(1000)
            .HasComment("Last submission/clearance error message (if failed).");

        builder.Property(z => z.SubmissionAttempts)
            .HasDefaultValue(0)
            .HasComment("Submission attempt count (for retry logic tracking).");

        builder.Property(z => z.CreatedAtUtc)
            .IsRequired()
            .HasComment("Submission creation timestamp.");

        builder.Property(z => z.UpdatedAtUtc)
            .IsRequired()
            .IsConcurrencyToken()
            .HasComment("Last update timestamp (concurrency token).");
    }
}
