using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyVehiclesQuery"/>. Two-step because Vehicles RLS is
/// internal-staff-only by Day-9 design (the policy predicate passes <c>NULL</c> for
/// CustomerId on Vehicles, blocking external-user reads):
///
/// <list type="number">
///   <item>Query <c>Leases</c> under the natural request scope — RLS filters to the
///         caller's customer's leases. Filter to status in (Active, Extended, Suspended) —
///         the colloquial meaning of "the vehicle I currently have". Project to the
///         distinct vehicle id set.</item>
///   <item>Open a <see cref="SystemTenancyScope"/> bounded strictly to the Vehicles read.
///         Query <c>Vehicles</c> filtered by the id set from step 1.</item>
/// </list>
///
/// <para>
/// <b>Trust boundary.</b> The SystemTenancyScope is a real RLS bypass — the security
/// guarantee is that the vehicle id set comes from a Leases query that is NOT under
/// SystemTenancyScope (so it IS customer-RLS-filtered). The Vehicles query then has a
/// <c>WHERE Id IN (…)</c> on that id set, so it's algebraically impossible to return a
/// vehicle the caller doesn't have a lease on. Three things to keep true under future
/// edits: (1) keep the SystemTenancyScope bounded to the Vehicles read only, (2) keep
/// the lease query outside that scope, (3) keep the Vehicles WHERE-IN clause in place.
/// </para>
///
/// <para>
/// Phase-2 follow-up: extend the RLS predicate on <c>Vehicles</c> with a customer-derived
/// clause (the Day-9 migration comment already flags this). When that lands, this handler
/// collapses to a single LINQ join and the SystemTenancyScope goes away.
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

        // Step 1: RLS-scoped Leases → vehicle id set. Application-side CustomerId filter
        // belt-and-braces with RLS; under InMemory (no RLS) it's the only control, under
        // real SQL it's redundant with the FILTER predicate but harmless.
        var vehicleIds = await db.Leases
            .AsNoTracking()
            .Where(l => l.CustomerId == customerId
                && l.VehicleId != null
                && CurrentlyHoldingStatuses.Contains(l.Status))
            .Select(l => l.VehicleId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (vehicleIds.Count == 0) return Array.Empty<MyVehicleDto>();

        // Step 2: bounded SystemTenancyScope for the Vehicles read only.
        using var systemScope = SystemTenancyScope.For(tenant.TenantId);

        return await db.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
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
