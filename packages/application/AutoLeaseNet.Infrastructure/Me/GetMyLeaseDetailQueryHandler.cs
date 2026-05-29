using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyLeaseDetailQuery"/>. Two-step in the same shape as
/// <see cref="GetMyVehiclesQueryHandler"/>: the lease read runs under the
/// natural request scope (RLS scopes Leases by CustomerId) and an app-side
/// CustomerId predicate enforces the same constraint under EF InMemory; if the
/// lease carries a <c>VehicleId</c>, a bounded <see cref="SystemTenancyScope"/>
/// reads the vehicle row.
///
/// <para>
/// <b>Trust boundary.</b> Vehicles RLS is internal-staff-only. The vehicle read
/// is inside the bypass scope, but the vehicle id used in the <c>FirstOrDefault</c>
/// comes from a lease row that is NOT under the bypass — so the customer can
/// only ever see the vehicle attached to their own lease. Phase 2 extends RLS
/// on <c>Vehicles</c> with a customer-derived predicate and removes the scope.
/// </para>
///
/// <para>
/// Returns <c>null</c> for "not visible to this customer" so the endpoint can
/// emit 404 without distinguishing "doesn't exist" from "not yours".
/// </para>
/// </summary>
public sealed class GetMyLeaseDetailQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetMyLeaseDetailQuery, MyLeaseDetailDto?>
{
    public async Task<MyLeaseDetailDto?> Handle(
        GetMyLeaseDetailQuery request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/leases/{id} requires an authenticated tenant context.");
        }
        if (tenant.CustomerId is not Guid customerId || customerId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/leases/{id} requires a customer context (X-Dev-Customer-Id or customer_id claim).");
        }

        var lease = await db.Leases
            .AsNoTracking()
            .Where(l => l.Id == request.LeaseId && l.CustomerId == customerId)
            .Select(l => new
            {
                l.Id,
                l.TajeerContractNumber,
                l.Status,
                l.ContractTypeCode,
                l.ContractStartUtc,
                l.ContractEndUtc,
                l.ActualReturnUtc,
                l.AllowedKmPerHour,
                l.AllowedKmPerDay,
                l.UnlimitedKm,
                l.AllowedLateHours,
                l.ExtensionCount,
                l.RentAmount,
                l.PaidAmount,
                l.RemainingAmount,
                l.VatAmount,
                l.TotalAmount,
                l.PaymentMethodCode,
                l.DiscountType,
                l.DiscountValue,
                l.SavedAtUtc,
                l.IssuedAtUtc,
                l.SuspendedAtUtc,
                l.ResumedAtUtc,
                l.ClosedAtUtc,
                l.CancelledAtUtc,
                l.ExpiredAtUtc,
                l.SuspensionReasonCode,
                l.ClosureMainReasonCode,
                l.ClosureSubReasonCode,
                l.VehicleId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lease is null) return null;

        LeaseVehicleSummaryDto? vehicleDto = null;
        if (lease.VehicleId is Guid vehicleId)
        {
            using var systemScope = SystemTenancyScope.For(tenant.TenantId);
            vehicleDto = await db.Vehicles
                .AsNoTracking()
                .Where(v => v.Id == vehicleId)
                .Select(v => new LeaseVehicleSummaryDto(
                    v.Id, v.PlateNumber, v.PlateLetters, v.PlateTypeCode,
                    v.Make, v.Model, v.ModelYear, v.Color))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new MyLeaseDetailDto(
            lease.Id,
            lease.TajeerContractNumber,
            (int)lease.Status,
            lease.ContractTypeCode,
            lease.ContractStartUtc,
            lease.ContractEndUtc,
            lease.ActualReturnUtc,
            lease.AllowedKmPerHour,
            lease.AllowedKmPerDay,
            lease.UnlimitedKm,
            lease.AllowedLateHours,
            lease.ExtensionCount,
            lease.RentAmount,
            lease.PaidAmount,
            lease.RemainingAmount,
            lease.VatAmount,
            lease.TotalAmount,
            lease.PaymentMethodCode,
            lease.DiscountType,
            lease.DiscountValue,
            lease.SavedAtUtc,
            lease.IssuedAtUtc,
            lease.SuspendedAtUtc,
            lease.ResumedAtUtc,
            lease.ClosedAtUtc,
            lease.CancelledAtUtc,
            lease.ExpiredAtUtc,
            lease.SuspensionReasonCode,
            lease.ClosureMainReasonCode,
            lease.ClosureSubReasonCode,
            vehicleDto);
    }
}
