using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Inspection_Aggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AbandonedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AbandonedReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OdometerKm = table.Column<int>(type: "int", nullable: false),
                    FuelLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    AcCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    RadioStereoCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    ScreenCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    SpeedometerCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    KeysCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    CarSeatsCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    SafetyTriangleCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    FireExtinguisherCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    FirstAidKitCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    SpareTireToolsCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    TiresCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    SpareTireCondition = table.Column<byte>(type: "tinyint", nullable: true),
                    Other1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Other2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SketchInfoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenterSignatureBlobUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionDamageMarkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PositionX = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    PositionY = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionDamageMarkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionDamageMarkers_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AiDamageDetectionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPhotos_Inspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDamageMarkers_InspectionId",
                table: "InspectionDamageMarkers",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDamageMarkers_TenantId_InspectionId",
                table: "InspectionDamageMarkers",
                columns: new[] { "TenantId", "InspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_InspectionId",
                table: "InspectionPhotos",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_TenantId_InspectionId_Sequence",
                table: "InspectionPhotos",
                columns: new[] { "TenantId", "InspectionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_TenantId_LeaseId_Type",
                table: "Inspections",
                columns: new[] { "TenantId", "LeaseId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_TenantId_Status",
                table: "Inspections",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_TenantId_VehicleId_PerformedAtUtc",
                table: "Inspections",
                columns: new[] { "TenantId", "VehicleId", "PerformedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionDamageMarkers");

            migrationBuilder.DropTable(
                name: "InspectionPhotos");

            migrationBuilder.DropTable(
                name: "Inspections");
        }
    }
}
