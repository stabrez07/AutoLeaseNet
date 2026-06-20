using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Pricing_Setup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingDiscountPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AllowedPresetsCsv = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingDiscountPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingFormulaDefinitions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Expression = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OutputField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Precision = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_PricingFormulaDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingVersions", x => x.Id);
                });


            migrationBuilder.CreateIndex(
                name: "IX_PricingDiscountPolicies_TenantId",
                schema: "dbo",
                table: "PricingDiscountPolicies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaDefinitions_TenantId",
                schema: "dbo",
                table: "PricingFormulaDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaDefinitions_TenantId_Code",
                schema: "dbo",
                table: "PricingFormulaDefinitions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingFormulaDefinitions_TenantId_IsActive",
                schema: "dbo",
                table: "PricingFormulaDefinitions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingVersions_TenantId",
                schema: "dbo",
                table: "PricingVersions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingVersions_TenantId_Status_EffectiveFromUtc",
                schema: "dbo",
                table: "PricingVersions",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingDiscountPolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PricingFormulaDefinitions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PricingVersions",
                schema: "dbo");

        }
    }
}
