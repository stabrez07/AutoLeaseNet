using MediatR;

namespace AutoLeaseNet.Application.Sales;

/// <summary>
/// Commands for quotation PDF generation and distribution.
/// Idempotent with 24h TTL per Spec 03 §10 / CLAUDE.md §8.
/// </summary>
public sealed record GenerateQuotePdfCommand(
    string IdempotencyKey,
    Guid QuotationId) : IRequest<QuotePdfCommandResult>;

public sealed record SendQuotePdfCommand(
    string IdempotencyKey,
    Guid QuotationId,
    string RecipientEmail) : IRequest<QuotePdfCommandResult>;

public sealed record QuotePdfCommandResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
