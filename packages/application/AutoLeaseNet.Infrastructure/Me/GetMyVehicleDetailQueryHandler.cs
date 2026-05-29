using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyVehicleDetailQuery"/>. Two-step, same shape as
/// <see cref="GetMyVehiclesQueryHandler"/>:
///
/// <list type="number">
///   <item>Lease-side EXISTS check under the natural request scope — the caller's
///         customer must have a lease in Active/Extended/Suspended on this vehicle id.
///         Returns <c>null</c> if no such lease exists.</item>
///   <item>Bounded <see cref="SystemTenancyScope"/> for the Vehicle read.</item>
/// </list>
///
/// <para>
/// The lease-side check is the trust anchor — see <see cref="GetMyVehiclesQueryHandler"/>
/// for the three invariants future editors must keep true. Phase 2's customer-derived
/// RLS on <c>Vehicles</c> collapses both this handler and <see cref="GetMyVehiclesQueryHandler"/>
/// to single LINQ joins.
/// </para>
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

        var hasCurrentLease = await db.Leases
            .AsNoTracking()
            .AnyAsync(l => l.CustomerId == customerId
                && l.VehicleId == request.VehicleId
                && CurrentlyHoldingStatuses.Contains(l.Status), cancellationToken)
            .ConfigureAwait(false);

        if (!hasCurrentLease) return null;

        using var systemScope = SystemTenancyScope.For(tenant.TenantId);

        return await db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == request.VehicleId)
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
