using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Leases;

/// <summary>
/// Raised by <see cref="Lease.MarkIssued"/> when Tajeer's issuance webhook flips the
/// local lease to <see cref="LeaseStatus.Active"/>. Phase-1 subscribers: SMS-on-issuance
/// (Day 7). Phase-2+ subscribers: customer-portal push notification, invoice scheduling
/// trigger, telematics device pairing, etc.
/// </summary>
public sealed record LeaseIssuedDomainEvent(
    Guid LeaseId,
    Guid TenantId,
    Guid? CustomerId,
    long TajeerContractNumber,
    string IssuanceUrl,
    DateTimeOffset IssuedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = IssuedAtUtc;
}
