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

public sealed record VehicleCommandResult(
    bool Success, Guid? VehicleId, string? ErrorCode, string? ErrorMessage);
