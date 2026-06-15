using MediatR;

namespace AutoLeaseNet.Application.Billing;

/// <summary>
/// Create invoice from lease (typically fired by LeaseIssuedDomainEvent subscriber).
/// Idempotent via Redis cache: prevents duplicate invoice if event replayed.
/// </summary>
public sealed record CreateInvoiceFromLeaseCommand(
    Guid LeaseId,
    string IdempotencyKey) : IRequest<CreateInvoiceCommandResult>;

/// <summary>Command result: success + invoice details or error.</summary>
public sealed record CreateInvoiceCommandResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null);
