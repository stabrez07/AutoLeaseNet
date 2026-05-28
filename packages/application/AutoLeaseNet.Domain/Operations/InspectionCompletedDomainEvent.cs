using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Raised by <see cref="Inspection.Complete"/> when the aggregate transitions to
/// COMPLETED. Phase-1 subscribers: none yet (the check-out / check-in sagas wire
/// theirs in the next workstream). Published post-commit by
/// <c>DomainEventDispatchInterceptor</c>.
/// </summary>
public sealed record InspectionCompletedDomainEvent(
    Guid InspectionId,
    Guid TenantId,
    Guid VehicleId,
    Guid? LeaseId,
    InspectionType Type,
    DateTimeOffset PerformedAtUtc,
    DateTimeOffset CompletedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = CompletedAtUtc;
}
