using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Raised when a quotation reaches <see cref="QuotationStatus.Approved"/> — either the last
/// required tier approved, or no tier was required at submit. Downstream (Day 24+) the sales
/// rep is notified it is ready to send to the customer. Forward-declared; no subscriber yet.
/// </summary>
public sealed record QuotationApprovedDomainEvent(
    Guid QuotationId,
    Guid TenantId,
    Guid CustomerId,
    decimal TotalSar,
    DateTimeOffset ApprovedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = ApprovedAtUtc;
}
