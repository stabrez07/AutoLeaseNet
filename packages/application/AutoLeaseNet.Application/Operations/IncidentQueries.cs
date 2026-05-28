using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Operations;

// Handlers live in AutoLeaseNet.Infrastructure.Operations so they can use the DbContext
// directly (matches the Inspection + Lookups convention).

/// <summary>Single-aggregate tenant-scoped lookup.</summary>
public sealed record GetIncidentByIdQuery(Guid IncidentId) : IRequest<IncidentDetailDto?>;

/// <summary>Tenant-scoped paged search ordered by ReportedAtUtc DESC.</summary>
public sealed record SearchIncidentsQuery(
    int Page,
    int PageSize,
    Guid? LeaseId,
    Guid? VehicleId,
    IncidentStatus? Status,
    IncidentSeverity? Severity) : IRequest<PagedResult<IncidentSummaryDto>>;

// ─── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record IncidentSummaryDto(
    Guid Id,
    Guid VehicleId,
    Guid? LeaseId,
    IncidentType Type,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset ReportedAtUtc,
    DateTimeOffset IncidentTimeUtc,
    bool RequiresReplacement);

public sealed record IncidentDetailDto(
    Guid Id,
    Guid VehicleId,
    Guid? LeaseId,
    Guid ReportedByPersonId,
    IncidentType Type,
    IncidentSeverity Severity,
    IncidentStatus Status,
    bool RequiresReplacement,
    DateTimeOffset ReportedAtUtc,
    DateTimeOffset IncidentTimeUtc,
    DateTimeOffset? InvestigationStartedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    decimal? LocationLat,
    decimal? LocationLng,
    string? LocationDescription,
    string Description,
    string? PoliceReportNumber,
    string? InsuranceClaimNumber,
    string? ResolutionNotes,
    Guid? ReplacementLeaseId);
