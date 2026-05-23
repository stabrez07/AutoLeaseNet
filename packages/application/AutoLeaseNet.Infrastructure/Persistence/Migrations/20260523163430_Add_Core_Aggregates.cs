using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Core_Aggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualReturnUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllowedKmPerDay",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AllowedKmPerHour",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AllowedLateHours",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorizedDriverId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Leases",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosureMainReasonCode",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosureSubReasonCode",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContractEndUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContractStartUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "ContractTypeCode",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DamagesObserved",
                table: "Leases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndKm",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiredAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExtendedCoverageId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtensionCount",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ExtraDriverId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuanceConditionNotes",
                table: "Leases",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethodCode",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PiiOptedOut",
                table: "Leases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryDriverId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiveBranchId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentAmount",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RentPolicyId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResumedAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnBranchId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnConditionNotes",
                table: "Leases",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnFuelLevelCode",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaveFailureReason",
                table: "Leases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartFuelLevelCode",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartKm",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAtUtc",
                table: "Leases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspensionReasonCode",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TajeerExtendedCoverageId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TajeerIssuanceToken",
                table: "Leases",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TajeerOperatorId",
                table: "Leases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TajeerReceiveBranchId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TajeerRentPolicyId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TajeerReturnBranchId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TajeerWorkingBranchId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "UnlimitedKm",
                table: "Leases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Leases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkingBranchId",
                table: "Leases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CityEn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CityAr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RegionEn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RegionAr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TajeerBranchId = table.Column<int>(type: "int", nullable: false),
                    TajeerOperatorId = table.Column<long>(type: "bigint", nullable: false),
                    WorkingHoursJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NationalAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreferredLanguage = table.Column<int>(type: "int", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LegalNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CommercialRegistration = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VatNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BillingAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreditCurrency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PersonNameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PersonNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IdTypeCode = table.Column<int>(type: "int", nullable: true),
                    PersonIdNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    NationalityCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    KycVerified = table.Column<bool>(type: "bit", nullable: false),
                    KycVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    KycVerifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PiiOptedOut = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonNameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PersonNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IdTypeCode = table.Column<int>(type: "int", nullable: false),
                    PersonIdNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    NationalityCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DriverLicenseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LicenseClass = table.Column<int>(type: "int", nullable: false),
                    LicenseIssuePlaceId = table.Column<long>(type: "bigint", nullable: true),
                    LicenseIssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LicenseExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NationalAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TammAuthorizationStatus = table.Column<int>(type: "int", nullable: false),
                    TammAuthorizationRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TammAuthorizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DefensiveDrivingCertHeld = table.Column<bool>(type: "bit", nullable: false),
                    AccidentCountLast3Yrs = table.Column<int>(type: "int", nullable: false),
                    PiiOptedOut = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtendedCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CoverageType = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductibleAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TajeerExtendedCoverageId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ExtendedCoverages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BaseDailyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseHourlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AllowedKmPerDay = table.Column<int>(type: "int", nullable: false),
                    AllowedKmPerHour = table.Column<int>(type: "int", nullable: false),
                    UnlimitedKm = table.Column<bool>(type: "bit", nullable: false),
                    LateHourFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraKmFee = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinRentalDays = table.Column<int>(type: "int", nullable: false),
                    MaxRentalDays = table.Column<int>(type: "int", nullable: true),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TajeerRentPolicyId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RentPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PlateLetters = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PlateTypeCode = table.Column<int>(type: "int", nullable: false),
                    Vin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EngineNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Make = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelYear = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FuelType = table.Column<int>(type: "int", nullable: false),
                    TransmissionType = table.Column<int>(type: "int", nullable: false),
                    BodyType = table.Column<int>(type: "int", nullable: false),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    LicenseExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsuranceExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectionExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsuranceCompany = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InsurancePolicyNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OwnerBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentKm = table.Column<int>(type: "int", nullable: false),
                    LastServiceKm = table.Column<int>(type: "int", nullable: true),
                    LastServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextServiceDueKm = table.Column<int>(type: "int", nullable: true),
                    NextServiceDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseInvoiceRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DepreciationPerMonth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentBookValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TelematicsProvider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DeviceImei = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastTelemetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId_ContractEndUtc",
                table: "Leases",
                columns: new[] { "TenantId", "ContractEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId_ContractStartUtc",
                table: "Leases",
                columns: new[] { "TenantId", "ContractStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId_CustomerId",
                table: "Leases",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId_VehicleId",
                table: "Leases",
                columns: new[] { "TenantId", "VehicleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_Code",
                table: "Branches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_IsActive",
                table: "Branches",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_TajeerBranchId",
                table: "Branches",
                columns: new[] { "TenantId", "TajeerBranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_CommercialRegistration",
                table: "Customers",
                columns: new[] { "TenantId", "CommercialRegistration" },
                unique: true,
                filter: "[CommercialRegistration] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_PersonIdNumber",
                table: "Customers",
                columns: new[] { "TenantId", "PersonIdNumber" },
                unique: true,
                filter: "[PersonIdNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Status",
                table: "Customers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Type",
                table: "Customers",
                columns: new[] { "TenantId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_CustomerId",
                table: "Drivers",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_LicenseExpiryDate",
                table: "Drivers",
                columns: new[] { "TenantId", "LicenseExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_PersonIdNumber",
                table: "Drivers",
                columns: new[] { "TenantId", "PersonIdNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_Status",
                table: "Drivers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtendedCoverages_TenantId_Code",
                table: "ExtendedCoverages",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtendedCoverages_TenantId_IsActive",
                table: "ExtendedCoverages",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtendedCoverages_TenantId_TajeerExtendedCoverageId",
                table: "ExtendedCoverages",
                columns: new[] { "TenantId", "TajeerExtendedCoverageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentPolicies_TenantId_Code",
                table: "RentPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentPolicies_TenantId_IsActive",
                table: "RentPolicies",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RentPolicies_TenantId_TajeerRentPolicyId",
                table: "RentPolicies",
                columns: new[] { "TenantId", "TajeerRentPolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_CurrentBranchId",
                table: "Vehicles",
                columns: new[] { "TenantId", "CurrentBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_PlateNumber_PlateLetters",
                table: "Vehicles",
                columns: new[] { "TenantId", "PlateNumber", "PlateLetters" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_Status",
                table: "Vehicles",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_Vin",
                table: "Vehicles",
                columns: new[] { "TenantId", "Vin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "ExtendedCoverages");

            migrationBuilder.DropTable(
                name: "RentPolicies");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Leases_TenantId_ContractEndUtc",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Leases_TenantId_ContractStartUtc",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Leases_TenantId_CustomerId",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Leases_TenantId_VehicleId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ActualReturnUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "AllowedKmPerDay",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "AllowedKmPerHour",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "AllowedLateHours",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "AuthorizedDriverId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ClosureMainReasonCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ClosureSubReasonCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ContractEndUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ContractStartUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ContractTypeCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "DamagesObserved",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "EndKm",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ExpiredAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ExtendedCoverageId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ExtensionCount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ExtraDriverId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "IssuanceConditionNotes",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "PaymentMethodCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "PiiOptedOut",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "PrimaryDriverId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ReceiveBranchId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "RentAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "RentPolicyId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ResumedAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ReturnBranchId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ReturnConditionNotes",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ReturnFuelLevelCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "SaveFailureReason",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "SavedAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "StartFuelLevelCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "StartKm",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "SuspensionReasonCode",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerExtendedCoverageId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerIssuanceToken",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerOperatorId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerReceiveBranchId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerRentPolicyId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerReturnBranchId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TajeerWorkingBranchId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "UnlimitedKm",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "WorkingBranchId",
                table: "Leases");
        }
    }
}
