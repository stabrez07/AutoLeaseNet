using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyVehicleDetailQuery"/>. Same shape as
/// <see cref="GetMyVehiclesQueryHandler"/>: a single RLS-scoped query whose
/// application-side EXISTS join mirrors the Phase-2 Vehicles RLS predicate
/// (<c>dbo.fn_VehiclesTenancyPredicate</c>) and adds the "currently holding"
/// status gate. Returns <c>null</c> when the customer has no current lease on
/// the vehicle, which the endpoint maps to 404.
/// </summary>
public sealed class GetMyVehicleDetailQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetMyVehicleDetailQuery, MyVehicleDetailDto?>
{
    private static readonly LeaseStatus[] CurrentlyHoldingStatuses =
    {
        LeaseStatus.Active,
        LeaseStatus.Extended,
        LeaseStatus.Suspended,
    };

    public async Task<MyVehicleDetailDto?> Handle(
        GetMyVehicleDetailQuery request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/vehicles/{id} requires an authenticated tenant context.");
        }
        if (tenant.CustomerId is not Guid customerId || customerId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/vehicles/{id} requires a customer context (X-Dev-Customer-Id or customer_id claim).");
        }

        return await db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == request.VehicleId
                && v.TenantId == tenant.TenantId
                && db.Leases.Any(l => l.VehicleId == v.Id
                                      && l.CustomerId == customerId
                                      && CurrentlyHoldingStatuses.Contains(l.Status)))
            .Select(v => new MyVehicleDetailDto(
                v.Id,
                v.PlateNumber,
                v.PlateLetters,
                v.PlateTypeCode,
                v.Make,
                v.Model,
                v.ModelYear,
                v.Color,
                (int)v.FuelType,
                (int)v.TransmissionType,
                (int)v.BodyType,
                v.Seats,
                v.CurrentKm,
                v.LicenseExpiryDate,
                v.InsuranceExpiryDate,
                v.InspectionExpiryDate,
                v.InsuranceCompany,
                v.InsurancePolicyNumber,
                v.NextServiceDueKm,
                v.NextServiceDueDate))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
