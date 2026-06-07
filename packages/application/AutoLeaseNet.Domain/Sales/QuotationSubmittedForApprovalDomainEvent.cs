using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Raised by <see cref="Quotation.SubmitForApproval"/> when one or more tiers are required.
/// Trigger for the Quote Approval Workflow Saga (Spec 02 §6.1) — notifies the first-tier
/// approver. Forward-declared in this foundation slice; the saga subscriber lands Day 23
/// (same no-subscriber-yet pattern as <see cref="Operations.IncidentReportedDomainEvent"/>).
/// </summary>
public sealed record QuotationSubmittedForApprovalDomainEvent(
    Guid QuotationId,
    Guid TenantId,
    Guid CustomerId,
    decimal TotalSar,
    byte FirstTierLevel,
    DateTimeOffset SubmittedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = SubmittedAtUtc;
}
