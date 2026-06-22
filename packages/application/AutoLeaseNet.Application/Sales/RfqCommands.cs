using MediatR;

namespace AutoLeaseNet.Application.Sales;

public sealed record CreateRfqCommand(
    Guid CustomerId, string Source,
    int VehicleQty, int TenureMonths,
    string? VehicleCategories, string? Services,
    int? AnnualMileageCapKm, DateOnly? ExpectedCloseDate,
    string? Notes,
    string IdempotencyKey) : IRequest<RfqCommandResult>;

public sealed record UpdateRfqStageCommand(
    Guid RfqId, string ToStage,
    string? Comment,
    string IdempotencyKey) : IRequest<RfqCommandResult>;

public sealed record UpdateRfqDetailsCommand(
    Guid RfqId,
    int? VehicleQty, int? TenureMonths,
    string? VehicleCategories, string? Services,
    int? AnnualMileageCapKm, DateOnly? ExpectedCloseDate,
    string? Notes, int? Probability,
    string IdempotencyKey) : IRequest<RfqCommandResult>;

public sealed record ConvertRfqToQuotationCommand(
    Guid RfqId,
    string IdempotencyKey) : IRequest<RfqCommandResult>;

public sealed record RfqCommandResult(
    bool Success, Guid? RfqId, Guid? QuotationId,
    string? ErrorCode, string? ErrorMessage);
