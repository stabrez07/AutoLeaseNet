using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds three new tables for the Vehicle module upgrade:
    ///   - VehicleHistoryEvents  : append-only audit log per vehicle
    ///   - VehicleServiceRecords : persistent PMS/CMS service history
    ///   - VehicleImages         : AI-generated or uploaded image URLs
    ///
    /// Each table inherits tenant-based RLS via the existing
    /// fn_TenancyPredicate (internal staff) policy applied in
    /// 20260529012701_Add_RLS_TenancyPolicy.
    /// </summary>
    public partial class Add_Vehicle_History_ServiceRecords_Images : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── VehicleHistoryEvents ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "VehicleHistoryEvents",
                columns: table => new
                {
                    Id            = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId      = table.Column<Guid>(nullable: false),
                    VehicleId     = table.Column<Guid>(nullable: false),
                    EventType     = table.Column<int>(nullable: false),
                    Description   = table.Column<string>(maxLength: 512, nullable: false),
                    PreviousValue = table.Column<string>(maxLength: 256, nullable: true),
                    NewValue      = table.Column<string>(maxLength: 256, nullable: true),
                    PerformedByName = table.Column<string>(maxLength: 128, nullable: false),
                    CreatedAtUtc  = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAtUtc  = table.Column<DateTimeOffset>(nullable: false),
                    CreatedBy     = table.Column<Guid>(nullable: true),
                    UpdatedBy     = table.Column<Guid>(nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_VehicleHistoryEvents", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_VehicleHistoryEvents_TenantId_VehicleId_CreatedAtUtc",
                table: "VehicleHistoryEvents",
                columns: new[] { "TenantId", "VehicleId", "CreatedAtUtc" });

            // ─── VehicleServiceRecords ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "VehicleServiceRecords",
                columns: table => new
                {
                    Id                  = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId            = table.Column<Guid>(nullable: false),
                    VehicleId           = table.Column<Guid>(nullable: false),
                    Type                = table.Column<int>(nullable: false),
                    ServiceCode         = table.Column<string>(maxLength: 32, nullable: false),
                    Description         = table.Column<string>(maxLength: 256, nullable: false),
                    ServicedAt          = table.Column<DateOnly>(nullable: false),
                    OdometerAtService   = table.Column<int>(nullable: false),
                    CostSar             = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Branch              = table.Column<string>(maxLength: 128, nullable: false),
                    Technician          = table.Column<string>(maxLength: 128, nullable: false),
                    PartsReplacedRaw    = table.Column<string>(maxLength: 1024, nullable: false),
                    NextServiceOdometer = table.Column<int>(nullable: true),
                    NextServiceDate     = table.Column<DateOnly>(nullable: true),
                    Notes               = table.Column<string>(maxLength: 1024, nullable: true),
                    CreatedAtUtc        = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAtUtc        = table.Column<DateTimeOffset>(nullable: false),
                    CreatedBy           = table.Column<Guid>(nullable: true),
                    UpdatedBy           = table.Column<Guid>(nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_VehicleServiceRecords", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_VehicleServiceRecords_TenantId_VehicleId_ServicedAt",
                table: "VehicleServiceRecords",
                columns: new[] { "TenantId", "VehicleId", "ServicedAt" });

            // ─── VehicleImages ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "VehicleImages",
                columns: table => new
                {
                    Id           = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId     = table.Column<Guid>(nullable: false),
                    VehicleId    = table.Column<Guid>(nullable: false),
                    ImageUrl     = table.Column<string>(maxLength: 1024, nullable: false),
                    ThumbnailUrl = table.Column<string>(maxLength: 1024, nullable: true),
                    AltText      = table.Column<string>(maxLength: 256, nullable: true),
                    IsAiGenerated = table.Column<bool>(nullable: false),
                    SortOrder    = table.Column<int>(nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                    CreatedBy    = table.Column<Guid>(nullable: true),
                    UpdatedBy    = table.Column<Guid>(nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_VehicleImages", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_VehicleImages_TenantId_VehicleId_SortOrder",
                table: "VehicleImages",
                columns: new[] { "TenantId", "VehicleId", "SortOrder" });

            // ─── RLS: apply fn_TenancyPredicate to the three new tables ──────
            migrationBuilder.Sql(@"
ALTER TABLE dbo.VehicleHistoryEvents ENABLE ROW LEVEL SECURITY;
CREATE SECURITY POLICY dbo.VehicleHistoryEventsRlsPolicy
    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleHistoryEvents,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleHistoryEvents AFTER INSERT
WITH (STATE = ON);

ALTER TABLE dbo.VehicleServiceRecords ENABLE ROW LEVEL SECURITY;
CREATE SECURITY POLICY dbo.VehicleServiceRecordsRlsPolicy
    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleServiceRecords,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleServiceRecords AFTER INSERT
WITH (STATE = ON);

ALTER TABLE dbo.VehicleImages ENABLE ROW LEVEL SECURITY;
CREATE SECURITY POLICY dbo.VehicleImagesRlsPolicy
    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleImages,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.VehicleImages AFTER INSERT
WITH (STATE = ON);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP SECURITY POLICY IF EXISTS dbo.VehicleHistoryEventsRlsPolicy;
DROP SECURITY POLICY IF EXISTS dbo.VehicleServiceRecordsRlsPolicy;
DROP SECURITY POLICY IF EXISTS dbo.VehicleImagesRlsPolicy;
");
            migrationBuilder.DropTable("VehicleHistoryEvents");
            migrationBuilder.DropTable("VehicleServiceRecords");
            migrationBuilder.DropTable("VehicleImages");
        }
    }
}
