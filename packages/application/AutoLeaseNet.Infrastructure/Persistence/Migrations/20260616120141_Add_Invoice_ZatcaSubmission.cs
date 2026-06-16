using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Invoice_ZatcaSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "Tenant-scoped sequential number (e.g., INV-2026-0001)"),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Foreign key to the Lease aggregate"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Foreign key to the Customer aggregate"),
                    Status = table.Column<int>(type: "int", nullable: false, comment: "Invoice state per Spec 02 §4.4"),
                    IssueDateUtc = table.Column<DateOnly>(type: "date", nullable: false, comment: "Date the invoice was created (typically when lease issued)"),
                    DueDateUtc = table.Column<DateOnly>(type: "date", nullable: false, comment: "Invoice due date (30 days from issue by default)"),
                    BaseAmountSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "Base rental amount in SAR (Phase 1: single line item)"),
                    VatSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "VAT amount (15% KSA standard rate)"),
                    TotalSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "Total invoice amount (base + VAT)"),
                    UblXml = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: true, comment: "ZATCA UBL 2.1 XML (populated by Day-26 builder); null until submitted"),
                    ZatcaInvoiceHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "ZATCA invoice hash (SHA-256); set on clearance"),
                    ClearedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "ZATCA clearance timestamp; set when status = Cleared"),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "Last submission/clearance error message"),
                    SubmissionAttempts = table.Column<int>(type: "int", nullable: false, comment: "Count of ZATCA submission attempts"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Tenant identifier for RLS isolation (Spec 01 §3)."),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZatcaSubmissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Reference to Invoice aggregate (1:1 relationship)."),
                    Status = table.Column<int>(type: "int", nullable: false, comment: "Submission state per Spec 02 §4.5 (Draft, Submitted, Cleared, etc.)."),
                    UblXml = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true, comment: "Canonical UBL 2.1 XML (before signing)."),
                    SignedUblXml = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true, comment: "Signed UBL XML with ECDSA P-256 signature embedded."),
                    InvoiceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "SHA-256 hash of canonical UBL (hex-encoded)."),
                    ZatcaTransactionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "ZATCA-assigned transaction ID (returned on successful submission)."),
                    ZatcaReportingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "ZATCA reporting status (e.g., 'CLEARED', 'REJECTED', 'QUEUED')."),
                    ClearedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "Timestamp when ZATCA confirmed clearance."),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "Last submission/clearance error message (if failed)."),
                    SubmissionAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "Submission attempt count (for retry logic tracking)."),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Tenant ID for RLS isolation."),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "Submission creation timestamp."),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "Last update timestamp (concurrency token)."),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZatcaSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_LeaseId",
                table: "Invoices",
                columns: new[] { "TenantId", "LeaseId" });

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZatcaSubmissions_TenantId_InvoiceId",
                schema: "dbo",
                table: "ZatcaSubmissions",
                columns: new[] { "TenantId", "InvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZatcaSubmissions_TenantId_Status",
                schema: "dbo",
                table: "ZatcaSubmissions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ZatcaSubmissions_TenantId_ZatcaTransactionId",
                schema: "dbo",
                table: "ZatcaSubmissions",
                columns: new[] { "TenantId", "ZatcaTransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "ZatcaSubmissions",
                schema: "dbo");
        }
    }
}
