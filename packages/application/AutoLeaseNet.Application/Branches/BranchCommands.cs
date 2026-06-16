using MediatR;

namespace AutoLeaseNet.Application.Branches;

public sealed record CreateBranchCommand(
    string Code, string NameEn, string NameAr,
    string? CityEn, string? CityAr, string? RegionEn, string? RegionAr,
    string? Address, string? PhoneNumber, string? LicenseNumber,
    decimal? Latitude, decimal? Longitude,
    int TajeerBranchId, long TajeerOperatorId,
    string IdempotencyKey) : IRequest<BranchCommandResult>;

public sealed record UpdateBranchStatusCommand(
    Guid BranchId, bool Activate, string IdempotencyKey) : IRequest<BranchCommandResult>;

public sealed record BranchCommandResult(
    bool Success, Guid? BranchId, string? ErrorCode, string? ErrorMessage);
