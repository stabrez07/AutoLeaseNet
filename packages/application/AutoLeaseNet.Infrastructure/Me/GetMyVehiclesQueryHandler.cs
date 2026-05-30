using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyVehiclesQuery"/>. Single RLS-scoped query — the
/// Phase-2 Vehicles RLS extension (migration <c>Add_Vehicles_RLS_PhaseTwo</c>)
/// authorises an external customer on a vehicle iff they hold (or held) a
/// lease on it, so the historical two-step <c>SystemTenancyScope</c> bypass is
/// gone. The application-side EXISTS join mirrors the DB-side predicate and
/// adds the "currently holding" status filter (Active/Extended/Suspended) that
/// is the handler's business rule rather than the RLS contract.
///
/// <para>
/// EF InMemory has no RLS, so the EXISTS join is the only control there.
/// Under real SQL+RLS it is redundant with <c>dbo.fn_VehiclesTenancyPredicate</c>
/// but harmless and self-documenting.
/// </para>
/// </summary>
public sealed class GetMyVehiclesQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetMyVehiclesQuery, IReadOnlyList<MyVehicleDto>>
{
    private static readonly LeaseStatus[] CurrentlyHoldingStatuses =
    {
        LeaseStatus.Active,
        LeaseStatus.Extended,
        LeaseStatus.Suspended,
    };

    public async Task<IReadOnlyList<MyVehicleDto>> Handle(
        GetMyVehiclesQuery request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/vehicles requires an authenticated tenant context.");
        }
        if (tenant.CustomerId is not Guid customerId || customerId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/vehicles requires a customer context (X-Dev-Customer-Id or customer_id claim).");
        }

        return await db.Vehicles
            .AsNoTracking()
            .Where(v => v.TenantId == tenant.TenantId
                && db.Leases.Any(l => l.VehicleId == v.Id
                                      && l.CustomerId == customerId
                                      && CurrentlyHoldingStatuses.Contains(l.Status)))
            .OrderBy(v => v.Make).ThenBy(v => v.Model).ThenBy(v => v.ModelYear)
            .Select(v => new MyVehicleDto(
                v.Id,
                v.PlateNumber,
                v.PlateLetters,
                v.PlateTypeCode,
                v.Make,
                v.Model,
                v.ModelYear,
                v.Color,
                v.CurrentKm,
                v.LicenseExpiryDate,
                v.InsuranceExpiryDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
