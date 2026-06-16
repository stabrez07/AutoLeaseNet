using MediatR;

namespace AutoLeaseNet.Application.Drivers;

public sealed record CreateDriverCommand(
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber,
    string? DateOfBirth,
    string? NationalityCode, string DriverLicenseNumber, int LicenseClass,
    string LicenseExpiryDate,
    string? Mobile, string? Email, string? NationalAddress,
    Guid? CustomerId,
    string IdempotencyKey) : IRequest<DriverCommandResult>;

public sealed record DriverCommandResult(
    bool Success, Guid? DriverId, string? ErrorCode, string? ErrorMessage);
