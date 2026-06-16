using AutoLeaseNet.Domain.Sales;
using MediatR;

namespace AutoLeaseNet.Application.Sales;

/// <summary>
/// Command: Customer accepts a quotation (Spec 02 §4.1 Accepted terminal state).
/// Transitions the quote to Accepted and raises <see cref="QuotationAcceptedDomainEvent"/>
/// which will trigger the Lease Issuance Saga (Day 29).
/// </summary>
public sealed record AcceptQuotationCommand(
    Guid QuotationId,
    string? CustomerSignature,
    string IdempotencyKey) : IRequest<AcceptQuotationResult>;

/// <summary>Result of accepting a quotation.</summary>
public sealed record AcceptQuotationResult(
    bool Success,
    Guid? QuotationId,
    string? QuoteNumber,
    string? Status,
    DateTimeOffset? AcceptedAtUtc,
    string? ErrorCode,
    string? ErrorMessage);
