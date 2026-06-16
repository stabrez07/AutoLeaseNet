using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Raised when a customer accepts a quotation (<see cref="QuotationStatus.Accepted"/>).
/// Downstream: Lease Issuance Saga subscribes to this event and initiates SaveContract
/// via Tajeer (Spec 02 §6.2). Phase 1: event raised, saga wired in Day 29.
/// </summary>
public sealed record QuotationAcceptedDomainEvent(
    Guid QuotationId,
    Guid TenantId,
    Guid CustomerId,
    decimal TotalSar,
    int EstimatedDurationMonths,
    QuotationContractType ContractType,
    string? CustomerSignature,
    DateTimeOffset AcceptedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = AcceptedAtUtc;
}
