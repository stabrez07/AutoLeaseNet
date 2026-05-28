using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Handler for <see cref="CheckInLeaseCommand"/>. Validates Lease + Vehicle state,
/// calls Tajeer <c>CalculatePayment</c> for the preview, calls Tajeer <c>CloseContract</c>
/// for the vendor commit, then mirrors the result locally — CHECK_IN inspection +
/// <c>Lease.MarkClosed</c> + <c>Vehicle.Return</c>, all in one unit-of-work commit.
///
/// <para>
/// <b>Vendor-first ordering</b> keeps the inconsistency window scoped to "Tajeer 200
/// CLOSED → local SaveChanges". A crash inside that window self-heals on the next
/// idempotent replay (Tajeer is idempotent at its end too). Full outbox pattern is
/// deferred per the workstream plan.
/// </para>
/// </summary>
public sealed partial class CheckInLeaseCommandHandler(
    ILeaseRepository leases,
    IVehicleRepository vehicles,
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ITajeerContractClient tajeer,
    ILogger<CheckInLeaseCommandHandler> logger)
    : IRequestHandler<CheckInLeaseCommand, CheckInLeaseCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<CheckInLeaseCommandResult> Handle(CheckInLeaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("CheckInLeaseCommand requires an authenticated tenant context.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:check-in:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<CheckInLeaseCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        // 1. Resolve the lease (we need it for VehicleId + status check + TajeerContractNumber).
        var lease = await leases.GetByIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (lease is null) return Fail("lease.not_found", $"Lease {request.LeaseId} not found.");
        if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Extended && lease.Status != LeaseStatus.Suspended)
            return Fail("lease.invalid_state_for_check_in",
                $"Lease {request.LeaseId} status is {lease.Status}; must be Active, Extended, or Suspended.");
        if (lease.VehicleId is not { } vehicleId)
            return Fail("lease.no_vehicle", $"Lease {request.LeaseId} has no Vehicle reference; cannot check in.");
        if (lease.TajeerContractNumber is not { } contractNumber)
            return Fail("tajeer.contract_number_missing",
                $"Lease {request.LeaseId} has no TajeerContractNumber; cannot close at vendor.");

        // 2. Resolve the vehicle so we can transition it.
        var vehicle = await vehicles.GetByIdAsync(tenantId, vehicleId, ct).ConfigureAwait(false);
        if (vehicle is null) return Fail("vehicle.not_found", $"Vehicle {vehicleId} (from Lease) not found.");
        if (vehicle.Status != VehicleStatus.OnRent)
            return Fail("vehicle.not_on_rent",
                $"Vehicle {vehicleId} status is {vehicle.Status}; must be OnRent for check-in.");

        // 3. Odometer must not regress (Vehicle.Return enforces this too — we catch
        //    early to return a stable error code instead of an exception bubble).
        if (request.OdometerKm < vehicle.CurrentKm)
            return Fail("inspection.odometer_regression",
                $"OdometerKm {request.OdometerKm} is less than vehicle.CurrentKm {vehicle.CurrentKm}; odometer cannot decrease.");

        var nowUtc = clock.UtcNow;
        var tajeerTimestamp = nowUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var fuelCode = (int)request.FuelLevel;

        // 4. Tajeer CalculatePayment (preview — non-destructive).
        var calculateRequest = new CalculatePaymentRequest
        {
            ContractNumber = contractNumber,
            ReturnDate = tajeerTimestamp,
            ReturnedKm = request.OdometerKm,
            ReturnedFuelLevelCode = fuelCode,
            ExtraKm = request.ExtraKm,
            AdditionalCharges = request.AdditionalCharges,
            DiscountAmount = request.DiscountAmount,
        };
        var calculateResult = await tajeer.CalculatePaymentAsync(calculateRequest, ct).ConfigureAwait(false);
        if (!calculateResult.IsSuccess)
        {
            LogTajeerCalculateFailure(contractNumber, calculateResult.ErrorCode ?? "unknown", calculateResult.IsTransient);
            return Fail(
                code: calculateResult.IsTransient ? "tajeer.calculate.transient" : "tajeer.calculate.failure",
                message: $"Tajeer CalculatePayment failed for contract {contractNumber}: {calculateResult.ErrorMessage}");
        }
        var preview = calculateResult.Value!;

        // 5. Tajeer CloseContract (vendor commit). Use the preview's GrandTotal as the
        //    finalPaidAmount unless the caller declared one explicitly.
        var finalPaid = request.FinalPaidAmount ?? preview.GrandTotal;
        var closeRequest = new CloseContractRequest
        {
            ContractNumber = contractNumber,
            ClosureMainReasonCode = request.ClosureMainReasonCode,
            ClosureSubReasonCode = request.ClosureSubReasonCode,
            ReturnDate = tajeerTimestamp,
            ReturnedKm = request.OdometerKm,
            ReturnedFuelLevelCode = fuelCode,
            ReturnConditionNotes = request.ReturnConditionNotes,
            DamagesObserved = request.DamagesObserved,
            FinalPaidAmount = finalPaid,
            DiscountAmount = request.DiscountAmount,
        };
        var closeResult = await tajeer.CloseAsync(closeRequest, ct).ConfigureAwait(false);
        if (!closeResult.IsSuccess)
        {
            LogTajeerCloseFailure(contractNumber, closeResult.ErrorCode ?? "unknown", closeResult.IsTransient);
            return Fail(
                code: closeResult.IsTransient ? "tajeer.close.transient" : "tajeer.close.failure",
                message: $"Tajeer CloseContract failed for contract {contractNumber}: {closeResult.ErrorMessage}");
        }
        var vendorClose = closeResult.Value!;

        // 6. Build the CHECK_IN inspection, complete it, link it.
        var inspection = Inspection.Start(new StartInspectionInput
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            LeaseId = null, // explicitly link via LinkToLease for audit timestamp
            Type = InspectionType.CheckIn,
            PerformedByUserId = tenant.UserId ?? Guid.Empty,
            OdometerKm = request.OdometerKm,
            FuelLevel = request.FuelLevel,
            AcCondition = request.AcCondition,
            RadioStereoCondition = request.RadioStereoCondition,
            ScreenCondition = request.ScreenCondition,
            SpeedometerCondition = request.SpeedometerCondition,
            KeysCondition = request.KeysCondition,
            CarSeatsCondition = request.CarSeatsCondition,
            SafetyTriangleCondition = request.SafetyTriangleCondition,
            FireExtinguisherCondition = request.FireExtinguisherCondition,
            FirstAidKitCondition = request.FirstAidKitCondition,
            SpareTireToolsCondition = request.SpareTireToolsCondition,
            TiresCondition = request.TiresCondition,
            SpareTireCondition = request.SpareTireCondition,
            Notes = request.Notes,
            SketchInfoJson = request.SketchInfoJson,
            NowUtc = nowUtc,
        });
        inspection.Complete(nowUtc);
        inspection.LinkToLease(lease.Id, nowUtc);
        inspections.Add(inspection);

        // 7. Close the lease + return the vehicle.
        try
        {
            lease.MarkClosed(
                closureMainReasonCode: request.ClosureMainReasonCode,
                closureSubReasonCode: request.ClosureSubReasonCode,
                endKm: request.OdometerKm,
                returnFuelLevelCode: fuelCode,
                returnConditionNotes: request.ReturnConditionNotes,
                damagesObserved: request.DamagesObserved,
                nowUtc: nowUtc);
            vehicle.Return(request.OdometerKm, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("lease.close_rejected", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var payment = new CheckInPaymentBreakdown(
            RentAmount: preview.RentAmount,
            PaidAmount: preview.PaidAmount,
            LateHoursFee: preview.LateHoursFee,
            ExtraKmFee: preview.ExtraKmFee,
            DamagesFee: preview.DamagesFee,
            DiscountAmount: preview.DiscountAmount,
            TotalDue: preview.TotalDue,
            VatAmount: preview.VatAmount,
            GrandTotal: preview.GrandTotal,
            FinalPaidAmount: vendorClose.FinalPaidAmount);

        var result = new CheckInLeaseCommandResult(
            Success: true,
            LeaseId: lease.Id,
            InspectionId: inspection.Id,
            LeaseStatus: lease.Status.ToString(),
            ErrorCode: null,
            ErrorMessage: null,
            Payment: payment);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogClosed(lease.Id, inspection.Id, contractNumber);
        return result;
    }

    private static CheckInLeaseCommandResult Fail(string code, string message) =>
        new(false, null, null, null, code, message);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "CheckIn idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "Lease {LeaseId} closed via check-in; Inspection {InspectionId}; Tajeer contract {ContractNumber}")]
    partial void LogClosed(Guid leaseId, Guid inspectionId, long contractNumber);

    [LoggerMessage(EventId = 5103, Level = LogLevel.Warning, Message = "Tajeer CalculatePayment failed for contract {ContractNumber}: {ErrorCode} (transient={Transient})")]
    partial void LogTajeerCalculateFailure(long contractNumber, string errorCode, bool transient);

    [LoggerMessage(EventId = 5104, Level = LogLevel.Warning, Message = "Tajeer CloseContract failed for contract {ContractNumber}: {ErrorCode} (transient={Transient})")]
    partial void LogTajeerCloseFailure(long contractNumber, string errorCode, bool transient);
}
