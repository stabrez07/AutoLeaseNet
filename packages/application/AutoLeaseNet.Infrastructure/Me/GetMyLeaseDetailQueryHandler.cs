using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyLeaseDetailQuery"/>. The lease is RLS-scoped to
/// the calling customer (Day-9 <c>fn_TenancyPredicate</c> on Leases). If the
/// lease carries a <c>VehicleId</c>, the vehicle enrichment is a plain
/// FirstOrDefault — Phase-2 <c>fn_VehiclesTenancyPredicate</c> already grants
/// access because the customer has (or had) a lease on it. No
/// <see cref="SystemTenancyScope"/> needed.
///
/// <para>
/// Returns <c>null</c> for "not visible to this customer" so the endpoint
/// emits 404 without distinguishing "doesn't exist" from "not yours".
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
