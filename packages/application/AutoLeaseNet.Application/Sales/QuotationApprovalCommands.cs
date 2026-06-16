using AutoLeaseNet.Domain.Sales;
using MediatR;

namespace AutoLeaseNet.Application.Sales;

/// <summary>
/// Commands/queries for quotation approval routing (Spec 02 §6.1).
/// </summary>
public sealed record SubmitQuotationForApprovalCommand(
    string IdempotencyKey,
    Guid QuotationId) : IRequest<QuotationApprovalCommandResult>;

public sealed record RecordQuotationApprovalDecisionCommand(
    string IdempotencyKey,
    Guid QuotationId,
    byte TierLevel,
    bool Approved,
    string? Comment) : IRequest<QuotationApprovalCommandResult>;

public sealed record GetPendingQuotationApprovalsQuery : IRequest<IReadOnlyList<PendingQuotationApprovalDto>>;

public sealed record QuotationApprovalCommandResult(
    bool Success,
    Guid? QuotationId,
    QuotationStatus? Status,
    byte? NextTierLevel,
    string? NextRequiredRoleCode,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record PendingQuotationApprovalDto(
    Guid QuotationId,
    string QuoteNumber,
    decimal TotalSar,
    DateTimeOffset? SubmittedAtUtc,
    byte? NextTierLevel,
    string? NextRequiredRoleCode,
    int PendingTierCount);
