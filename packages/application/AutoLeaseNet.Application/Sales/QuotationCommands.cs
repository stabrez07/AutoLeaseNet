using AutoLeaseNet.Domain.Sales;
using MediatR;

namespace AutoLeaseNet.Application.Sales;

// ─── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateQuotationCommand : IRequest<QuotationCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly ValidUntilDate { get; init; }
    public required QuotationContractType ContractType { get; init; }
    public int EstimatedDurationMonths { get; init; }
    public decimal DiscountPercent { get; init; }
    public string? TermsAndConditionsMd { get; init; }
    public IReadOnlyList<CreateQuotationLineDto> Lines { get; init; } = [];
}

public sealed record CreateQuotationLineDto(
    QuotationItemType ItemType,
    string Description,
    string? VehicleSpecRef,
    int Quantity,
    decimal UnitPriceSar,
    decimal DiscountPercent);

public sealed record AddQuotationLineCommand : IRequest<QuotationCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid QuotationId { get; init; }
    public required QuotationItemType ItemType { get; init; }
    public required string Description { get; init; }
    public string? VehicleSpecRef { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPriceSar { get; init; }
    public decimal DiscountPercent { get; init; }
}

public sealed record SubmitQuotationForApprovalCommand(
    string IdempotencyKey,
    Guid QuotationId) : IRequest<QuotationCommandResult>;

public sealed record RecordApprovalDecisionCommand : IRequest<QuotationCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid QuotationId { get; init; }
    public required byte TierLevel { get; init; }
    public required bool Approved { get; init; }
    public string? Notes { get; init; }
}

public sealed record RecallQuotationCommand(
    string IdempotencyKey,
    Guid QuotationId) : IRequest<QuotationCommandResult>;

public sealed record MarkQuotationSentToCustomerCommand : IRequest<QuotationCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid QuotationId { get; init; }
    public string? PdfBlobUri { get; init; }
}

// ─── Shared result ─────────────────────────────────────────────────────────────

public sealed record QuotationCommandResult(
    bool Success,
    Guid? QuotationId,
    string? QuoteNumber,
    QuotationStatus? Status,
    decimal? SubTotalSar,
    decimal? TotalSar,
    IReadOnlyList<byte>? RequiredTierLevels,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static QuotationCommandResult Ok(
        Guid quotationId,
        string quoteNumber,
        QuotationStatus status,
        decimal subTotalSar,
        decimal totalSar,
        IReadOnlyList<byte>? requiredTierLevels = null)
        => new(true, quotationId, quoteNumber, status, subTotalSar, totalSar, requiredTierLevels, null, null);

    public static QuotationCommandResult Fail(string code, string message)
        => new(false, null, null, null, null, null, null, code, message);
}
