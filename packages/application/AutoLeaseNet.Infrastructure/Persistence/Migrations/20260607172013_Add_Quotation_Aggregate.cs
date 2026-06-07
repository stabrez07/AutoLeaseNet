using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Week-4 Day-22 (2026-06-07). Creates the Quotation aggregate's four tables
    /// (<c>Quotations</c>, <c>QuotationLines</c>, <c>QuotationApprovals</c>,
    /// <c>ApprovalTiers</c>) and extends <c>dbo.TenancyPolicy</c> (from
    /// <c>Add_RLS_TenancyPolicy</c>) to cover them — CLAUDE.md rule #4: the DB-engine
    /// tenancy floor must not regress when tables are added. All four use
    /// <c>fn_TenancyPredicate(TenantId, NULL)</c> — internal-only in Phase 1 (same stance
    /// as Inspections / Incidents). Phase-2 follow-up: swap <c>Quotations</c> to a
    /// CustomerId predicate when the customer accept-quote portal view lands.
    /// </remarks>
    public partial class Add_Quotation_Aggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TierLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiredRoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinAmountSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntilDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractType = table.Column<int>(type: "int", nullable: false),
                    EstimatedDurationMonths = table.Column<int>(type: "int", nullable: false),
                    TermsAndConditionsMd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubTotalSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    VatSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PdfBlobUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcceptedByCustomerSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TierLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiredRoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationApprovals_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VehicleSpecRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineTotalSar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLines_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTiers_TenantId_TierLevel",
                table: "ApprovalTiers",
                columns: new[] { "TenantId", "TierLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationApprovals_QuotationId",
                table: "QuotationApprovals",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationApprovals_TenantId_QuotationId_TierLevel",
                table: "QuotationApprovals",
                columns: new[] { "TenantId", "QuotationId", "TierLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationApprovals_TenantId_Status_RequiredRoleCode",
                table: "QuotationApprovals",
                columns: new[] { "TenantId", "Status", "RequiredRoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_QuotationId",
                table: "QuotationLines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_TenantId_QuotationId_LineNumber",
                table: "QuotationLines",
                columns: new[] { "TenantId", "QuotationId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_TenantId_CustomerId",
                table: "Quotations",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_TenantId_QuoteNumber",
                table: "Quotations",
                columns: new[] { "TenantId", "QuoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_TenantId_Status",
                table: "Quotations",
                columns: new[] { "TenantId", "Status" });

            // Extend the existing security policy to the four new tables. Internal-only in
            // Phase 1 (CustomerId arg = NULL → external users see no rows). Re-uses the
            // dbo.fn_TenancyPredicate created in Add_RLS_TenancyPolicy.
            migrationBuilder.Sql(@"
ALTER SECURITY POLICY dbo.TenancyPolicy
    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Quotations,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Quotations AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Quotations AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationLines,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationLines AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationLines AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationApprovals,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationApprovals AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.QuotationApprovals AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ApprovalTiers,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ApprovalTiers AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ApprovalTiers AFTER UPDATE;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the policy predicates first — a table referenced by a security policy
            // predicate cannot be dropped.
            migrationBuilder.Sql(@"
ALTER SECURITY POLICY dbo.TenancyPolicy
    DROP FILTER PREDICATE ON dbo.Quotations,
    DROP BLOCK  PREDICATE ON dbo.Quotations AFTER INSERT,
    DROP BLOCK  PREDICATE ON dbo.Quotations AFTER UPDATE,

    DROP FILTER PREDICATE ON dbo.QuotationLines,
    DROP BLOCK  PREDICATE ON dbo.QuotationLines AFTER INSERT,
    DROP BLOCK  PREDICATE ON dbo.QuotationLines AFTER UPDATE,

    DROP FILTER PREDICATE ON dbo.QuotationApprovals,
    DROP BLOCK  PREDICATE ON dbo.QuotationApprovals AFTER INSERT,
    DROP BLOCK  PREDICATE ON dbo.QuotationApprovals AFTER UPDATE,

    DROP FILTER PREDICATE ON dbo.ApprovalTiers,
    DROP BLOCK  PREDICATE ON dbo.ApprovalTiers AFTER INSERT,
    DROP BLOCK  PREDICATE ON dbo.ApprovalTiers AFTER UPDATE;
");

            migrationBuilder.DropTable(
                name: "ApprovalTiers");

            migrationBuilder.DropTable(
                name: "QuotationApprovals");

            migrationBuilder.DropTable(
                name: "QuotationLines");

            migrationBuilder.DropTable(
                name: "Quotations");
        }
    }
}
