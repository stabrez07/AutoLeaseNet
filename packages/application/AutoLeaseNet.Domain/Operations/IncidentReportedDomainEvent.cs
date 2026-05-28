using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Raised by <see cref="Incident.Report"/>. Spec 02 §5 — consumers include
/// notifications (ops + customer) and, in a future workstream, the Replacement
/// Saga (Spec 02 §6.5) for <c>RequiresReplacement == true</c>.
///
/// <para>
/// Phase 1 has no subscriber wired; same forward-declared pattern as
/// <see cref="InspectionCompletedDomainEvent"/>.
/// </para>
/// </summary>
public sealed record IncidentReportedDomainEvent(
    Guid IncidentId,
    Guid TenantId,
    Guid? LeaseId,
    Guid VehicleId,
    IncidentType Type,
    IncidentSeverity Severity,
    DateTimeOffset ReportedAtUtc,
    bool RequiresReplacement) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = ReportedAtUtc;
}
