using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Application.Operations;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using MediatR;

namespace AutoLeaseNet.Infrastructure.Operations;

internal static class InspectionQueryGuards
{
    internal static Guid RequireTenant(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Inspection query requires an authenticated tenant context.");
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

public sealed class GetInspectionByIdQueryHandler(IInspectionRepository inspections, ITenantContext tenant)
    : IRequestHandler<GetInspectionByIdQuery, InspectionDetailDto?>
{
    public async Task<InspectionDetailDto?> Handle(GetInspectionByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionQueryGuards.RequireTenant(tenant);

        var i = await inspections.GetByIdAsync(tenantId, request.InspectionId, cancellationToken).ConfigureAwait(false);
        if (i is null) return null;

        return new InspectionDetailDto(
            Id: i.Id,
            VehicleId: i.VehicleId,
            LeaseId: i.LeaseId,
            Type: i.Type,
            Status: i.Status,
            PerformedByUserId: i.PerformedByUserId,
            PerformedAtUtc: i.PerformedAtUtc,
            CompletedAtUtc: i.CompletedAtUtc,
            AbandonedAtUtc: i.AbandonedAtUtc,
            AbandonedReason: i.AbandonedReason,
            OdometerKm: i.OdometerKm,
            FuelLevel: i.FuelLevel,
            AcCondition: i.AcCondition,
            RadioStereoCondition: i.RadioStereoCondition,
            ScreenCondition: i.ScreenCondition,
            SpeedometerCondition: i.SpeedometerCondition,
            KeysCondition: i.KeysCondition,
            CarSeatsCondition: i.CarSeatsCondition,
            SafetyTriangleCondition: i.SafetyTriangleCondition,
            FireExtinguisherCondition: i.FireExtinguisherCondition,
            FirstAidKitCondition: i.FirstAidKitCondition,
            SpareTireToolsCondition: i.SpareTireToolsCondition,
            TiresCondition: i.TiresCondition,
            SpareTireCondition: i.SpareTireCondition,
            Other1: i.Other1,
            Other2: i.Other2,
            Notes: i.Notes,
            SketchInfoJson: i.SketchInfoJson,
            RenterSignatureBlobUri: i.RenterSignatureBlobUri,
            Photos: i.Photos.OrderBy(p => p.Sequence)
                .Select(p => new InspectionPhotoDto(p.Id, p.BlobUri, p.Sequence)).ToList(),
            DamageMarkers: i.DamageMarkers
                .Select(m => new InspectionDamageMarkerDto(m.Id, m.Type, m.PositionX, m.PositionY)).ToList());
    }
}

public sealed class SearchInspectionsQueryHandler(IInspectionRepository inspections, ITenantContext tenant)
    : IRequestHandler<SearchInspectionsQuery, PagedResult<InspectionSummaryDto>>
{
    public async Task<PagedResult<InspectionSummaryDto>> Handle(SearchInspectionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionQueryGuards.RequireTenant(tenant);
        var (page, size) = InspectionQueryGuards.ClampPaging(request.Page, request.PageSize);

        var result = await inspections.SearchAsync(
            tenantId, request.VehicleId, request.LeaseId, request.Type, request.Status, page, size, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(i => new InspectionSummaryDto(
            Id: i.Id,
            VehicleId: i.VehicleId,
            LeaseId: i.LeaseId,
            Type: i.Type,
            Status: i.Status,
            PerformedAtUtc: i.PerformedAtUtc,
            CompletedAtUtc: i.CompletedAtUtc,
            OdometerKm: i.OdometerKm,
            FuelLevel: i.FuelLevel,
            PhotoCount: i.Photos.Count,
            DamageMarkerCount: i.DamageMarkers.Count)).ToList();

        return new PagedResult<InspectionSummaryDto>(items, page, size, result.TotalCount);
    }
}
