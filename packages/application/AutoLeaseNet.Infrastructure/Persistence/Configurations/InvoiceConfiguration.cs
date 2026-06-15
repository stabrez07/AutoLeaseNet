using AutoLeaseNet.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLeaseNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Invoice aggregate (Spec 01 §3 RLS + Spec 02 §4.4).
/// Maps Invoice root + enforces TenantId-based row-level security.
/// </summary>
internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);

        // Multi-tenancy enforcement
        builder.Property(i => i.TenantId)
            .IsRequired()
            .HasComment("Tenant identifier for RLS isolation (Spec 01 §3).");

        // Natural keys
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_Invoices_TenantId_InvoiceNumber");

        builder.HasIndex(i => new { i.TenantId, i.LeaseId })
            .HasDatabaseName("IX_Invoices_TenantId_LeaseId");

        // Properties
        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Tenant-scoped sequential number (e.g., INV-2026-0001)");

        builder.Property(i => i.LeaseId)
            .IsRequired()
            .HasComment("Foreign key to the Lease aggregate");

        builder.Property(i => i.CustomerId)
            .IsRequired()
            .HasComment("Foreign key to the Customer aggregate");

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasComment("Invoice state per Spec 02 §4.4");

        builder.Property(i => i.IssueDateUtc)
            .IsRequired()
            .HasComment("Date the invoice was created (typically when lease issued)");

        builder.Property(i => i.DueDateUtc)
            .IsRequired()
            .HasComment("Invoice due date (30 days from issue by default)");

        builder.Property(i => i.BaseAmountSar)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment("Base rental amount in SAR (Phase 1: single line item)");

        builder.Property(i => i.VatSar)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment("VAT amount (15% KSA standard rate)");

        builder.Property(i => i.TotalSar)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment("Total invoice amount (base + VAT)");

        builder.Property(i => i.UblXml)
            .HasMaxLength(100000)
            .HasComment("ZATCA UBL 2.1 XML (populated by Day-26 builder); null until submitted");

        builder.Property(i => i.ZatcaInvoiceHash)
            .HasMaxLength(128)
            .HasComment("ZATCA invoice hash (SHA-256); set on clearance");

        builder.Property(i => i.ClearedAtUtc)
            .HasComment("ZATCA clearance timestamp; set when status = Cleared");

        builder.Property(i => i.LastErrorMessage)
            .HasMaxLength(1000)
            .HasComment("Last submission/clearance error message");

        builder.Property(i => i.SubmissionAttempts)
            .HasComment("Count of ZATCA submission attempts");

        // Audit fields (inherited from Entity)
        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.UpdatedAtUtc).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();
    }
}
