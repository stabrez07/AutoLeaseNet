using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Application.Operations;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using MediatR;

namespace AutoLeaseNet.Infrastructure.Operations;

internal static class IncidentQueryGuards
{
    internal static Guid RequireTenant(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Incident query requires an authenticated tenant context.");
        return tenant.TenantId;
    }

    internal static (int page, int size) ClampPaging(int page, int size)
    {
        if (page < 1) page = 1;
        if (size < 1) size = PagedResult<object>.DefaultPageSize;
        if (size > PagedResult<object>.MaxPageSize) size = PagedResult<object>.MaxPageSize;
        return (page, size);
    }
}

public sealed class GetIncidentByIdQueryHandler(IIncidentRepository incidents, ITenantContext tenant)
    : IRequestHandler<GetIncidentByIdQuery, IncidentDetailDto?>
{
    public async Task<IncidentDetailDto?> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = IncidentQueryGuards.RequireTenant(tenant);

        var i = await incidents.GetByIdAsync(tenantId, request.IncidentId, cancellationToken).ConfigureAwait(false);
        if (i is null) return null;

        return new IncidentDetailDto(
            Id: i.Id,
            VehicleId: i.VehicleId,
            LeaseId: i.LeaseId,
            ReportedByPersonId: i.ReportedByPersonId,
            Type: i.Type,
            Severity: i.Severity,
            Status: i.Status,
            RequiresReplacement: i.RequiresReplacement,
            ReportedAtUtc: i.ReportedAtUtc,
            IncidentTimeUtc: i.IncidentTimeUtc,
            InvestigationStartedAtUtc: i.InvestigationStartedAtUtc,
            ResolvedAtUtc: i.ResolvedAtUtc,
            ClosedAtUtc: i.ClosedAtUtc,
            LocationLat: i.LocationLat,
            LocationLng: i.LocationLng,
            LocationDescription: i.LocationDescription,
            Description: i.Description,
            PoliceReportNumber: i.PoliceReportNumber,
            InsuranceClaimNumber: i.InsuranceClaimNumber,
            ResolutionNotes: i.ResolutionNotes,
            ReplacementLeaseId: i.ReplacementLeaseId);
    }
}

public sealed class SearchIncidentsQueryHandler(IIncidentRepository incidents, ITenantContext tenant)
    : IRequestHandler<SearchIncidentsQuery, PagedResult<IncidentSummaryDto>>
{
    public async Task<PagedResult<IncidentSummaryDto>> Handle(SearchIncidentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = IncidentQueryGuards.RequireTenant(tenant);
        var (page, size) = IncidentQueryGuards.ClampPaging(request.Page, request.PageSize);

        var result = await incidents.SearchAsync(
            tenantId, request.LeaseId, request.VehicleId, request.Status, request.Severity, page, size, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(i => new IncidentSummaryDto(
            Id: i.Id,
            VehicleId: i.VehicleId,
            LeaseId: i.LeaseId,
            Type: i.Type,
            Severity: i.Severity,
            Status: i.Status,
            ReportedAtUtc: i.ReportedAtUtc,
            IncidentTimeUtc: i.IncidentTimeUtc,
            RequiresReplacement: i.RequiresReplacement)).ToList();

        return new PagedResult<IncidentSummaryDto>(items, page, size, result.TotalCount);
    }
}
