using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Operations;

/// <summary>
/// Commands for the <see cref="Incident"/> aggregate. Same idempotency contract
/// + co-location convention as <c>InspectionCommands</c>: every state-changing
/// command takes an <c>IdempotencyKey</c>, all handlers live in
/// <see cref="IncidentCommandHandlers"/>.
/// </summary>
public sealed record ReportIncidentCommand : IRequest<IncidentCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required Guid ReportedByPersonId { get; init; }
    public required IncidentType Type { get; init; }
    public required IncidentSeverity Severity { get; init; }
    public required DateTimeOffset IncidentTimeUtc { get; init; }
    public required string Description { get; init; }
    public decimal? LocationLat { get; init; }
    public decimal? LocationLng { get; init; }
    public string? LocationDescription { get; init; }
    public string? PoliceReportNumber { get; init; }
    public string? InsuranceClaimNumber { get; init; }
}

public sealed record StartIncidentInvestigationCommand(
    string IdempotencyKey,
    Guid IncidentId) : IRequest<IncidentCommandResult>;

public sealed record ResolveIncidentCommand(
    string IdempotencyKey,
    Guid IncidentId,
    string ResolutionNotes) : IRequest<IncidentCommandResult>;

public sealed record CloseIncidentCommand(
    string IdempotencyKey,
    Guid IncidentId) : IRequest<IncidentCommandResult>;

public sealed record UpdateIncidentClaimCommand(
    string IdempotencyKey,
    Guid IncidentId,
    string? PoliceReportNumber,
    string? InsuranceClaimNumber) : IRequest<IncidentCommandResult>;

/// <summary>
/// Result envelope shared by every Incident command. Same shape as
/// <see cref="InspectionCommandResult"/> so the BFF status-code mapper stays uniform.
/// </summary>
public sealed record IncidentCommandResult(
    bool Success,
    Guid? IncidentId,
    IncidentStatus? Status,
    string? ErrorCode,
    string? ErrorMessage);
