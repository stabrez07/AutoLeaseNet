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
/// Handler for <see cref="CheckInLeaseCommand"/>. Validates Lease + Vehicle states,
/// creates and completes the CHECK_IN inspection, links it to the lease, calls
/// <c>Lease.MarkClosed</c> + <c>Vehicle.Return</c>, all in one unit-of-work commit.
/// Failures along the way return a stable error code (no partial state leaks because
/// the UoW only commits if every step succeeded).
/// </summary>
public sealed partial class CheckInLeaseCommandHandler(
    ILeaseRepository leases,
    IVehicleRepository vehicles,
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
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

        // 1. Resolve the lease (we need it for VehicleId + status check).
        var lease = await leases.GetByIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (lease is null) return Fail("lease.not_found", $"Lease {request.LeaseId} not found.");
        if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Extended && lease.Status != LeaseStatus.Suspended)
            return Fail("lease.invalid_state_for_check_in",
                $"Lease {request.LeaseId} status is {lease.Status}; must be Active, Extended, or Suspended.");
        if (lease.VehicleId is not { } vehicleId)
            return Fail("lease.no_vehicle", $"Lease {request.LeaseId} has no Vehicle reference; cannot check in.");

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

        // 4. Build the CHECK_IN inspection, complete it, link it.
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

        // 5. Close the lease + return the vehicle.
        try
        {
            lease.MarkClosed(
                closureMainReasonCode: request.ClosureMainReasonCode,
                closureSubReasonCode: request.ClosureSubReasonCode,
                endKm: request.OdometerKm,
                returnFuelLevelCode: (int)request.FuelLevel,
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

        var result = new CheckInLeaseCommandResult(
            Success: true,
            LeaseId: lease.Id,
            InspectionId: inspection.Id,
            LeaseStatus: lease.Status.ToString(),
            ErrorCode: null,
            ErrorMessage: null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogClosed(lease.Id, inspection.Id);
        return result;
    }

    private static CheckInLeaseCommandResult Fail(string code, string message) =>
        new(false, null, null, null, code, message);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "CheckIn idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "Lease {LeaseId} closed via check-in; Inspection {InspectionId}")]
    partial void LogClosed(Guid leaseId, Guid inspectionId);
}
