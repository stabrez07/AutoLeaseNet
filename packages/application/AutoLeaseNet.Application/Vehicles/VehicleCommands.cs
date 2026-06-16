using MediatR;

namespace AutoLeaseNet.Application.Vehicles;

public sealed record CreateVehicleCommand(
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string? EngineNumber,
    string Make, string Model, int ModelYear, string? Color,
    int FuelType, int TransmissionType, int BodyType, int Seats,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    Guid OwnerBranchId, int CurrentKm,
    decimal? PurchasePrice, string? PurchaseDate,
    string IdempotencyKey) : IRequest<VehicleCommandResult>;

public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string? Color, int? Seats, string? Make, string? Model, int? ModelYear,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    Guid? CurrentBranchId, int? CurrentKm,
    decimal? PurchasePrice, string? PurchaseDate,
    string? Notes,
    string IdempotencyKey) : IRequest<VehicleCommandResult>;

public sealed record DeleteVehicleCommand(
    Guid VehicleId,
    string IdempotencyKey) : IRequest<VehicleCommandResult>;

public sealed record CreateServiceRecordCommand(
    Guid VehicleId,
    int Type,
    string ServiceCode,
    string Description,
    string ServicedAt,
    int OdometerAtService,
    decimal CostSar,
    string Branch,
    string Technician,
    IEnumerable<string>? PartsReplaced,
    int? NextServiceOdometer,
    string? NextServiceDate,
    string? Notes,
    string IdempotencyKey) : IRequest<VehicleCommandResult>;

/// <summary>Each row is one raw CSV line already parsed into fields.</summary>
public sealed record BulkVehicleRow(
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string Make, string Model, int ModelYear, string? Color,
    int FuelType, int TransmissionType, int BodyType, int Seats,
    Guid OwnerBranchId, int CurrentKm);

public sealed record BulkCreateVehiclesCommand(
    IReadOnlyList<BulkVehicleRow> Rows,
    string IdempotencyKey) : IRequest<BulkVehicleCommandResult>;

public sealed record BulkVehicleRowError(int RowIndex, string ErrorCode, string ErrorMessage);

public sealed record BulkVehicleCommandResult(
    bool Success,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<BulkVehicleRowError> Errors);

public sealed record GenerateVehicleImageCommand(
    Guid VehicleId,
    string IdempotencyKey) : IRequest<VehicleCommandResult>;

public sealed record VehicleCommandResult(
    bool Success, Guid? VehicleId, string? ErrorCode, string? ErrorMessage);
