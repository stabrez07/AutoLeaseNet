using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Billing;

/// <summary>
/// Domain event: Invoice auto-generated from lease issuance.
/// Triggers downstream handlers: ZATCA submission (Day-26), notifications (Day-28).
/// </summary>
public sealed record InvoiceGeneratedDomainEvent(
    Guid InvoiceId,
    Guid LeaseId,
    Guid TenantId,
    Guid CustomerId,
    string InvoiceNumber,
    decimal TotalSar,
    DateTimeOffset CreatedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = CreatedAtUtc;
}
