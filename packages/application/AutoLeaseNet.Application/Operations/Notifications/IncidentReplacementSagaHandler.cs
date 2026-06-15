using System.Globalization;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Operations.Notifications;

/// <summary>
/// Spec 02 §6.5 starter orchestration. Handles total-loss incidents by:
/// 1) selecting a replacement vehicle,
/// 2) creating a replacement lease through the existing SaveContract flow,
/// 3) linking Incident → ReplacementLease,
/// 4) closing old lease on Tajeer, with compensation (cancel new lease) on close failure.
/// </summary>
public sealed partial class IncidentReplacementSagaHandler(
    IIncidentRepository incidents,
    ILeaseRepository leases,
    IVehicleRepository vehicles,
    IUnitOfWork uow,
    ITajeerContractClient tajeer,
    IMediator mediator,
    IClock clock,
    ILogger<IncidentReplacementSagaHandler> logger)
    : INotificationHandler<DomainEventNotification<IncidentReportedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<IncidentReportedDomainEvent> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var evt = notification.Event;
        if (!evt.RequiresReplacement) return;
        if (evt.LeaseId is not { } oldLeaseId)
        {
            LogSkippedNoLease(evt.IncidentId);
            return;
        }

        var incident = await incidents.GetByIdAsync(evt.TenantId, evt.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            LogSkippedIncidentMissing(evt.IncidentId, evt.TenantId);
            return;
        }
        if (incident.ReplacementLeaseId is not null)
        {
            LogSkippedAlreadyLinked(evt.IncidentId, incident.ReplacementLeaseId.Value);
            return;
        }

        var oldLease = await leases.GetByIdAsync(evt.TenantId, oldLeaseId, cancellationToken).ConfigureAwait(false);
        if (oldLease is null)
        {
            LogSkippedLeaseMissing(oldLeaseId, evt.IncidentId);
            return;
        }
        if (oldLease.VehicleId is not { } oldVehicleId)
        {
            LogSkippedLeaseVehicleMissing(oldLeaseId, evt.IncidentId);
            return;
        }

        var oldVehicle = await vehicles.GetByIdAsync(evt.TenantId, oldVehicleId, cancellationToken).ConfigureAwait(false);
        if (oldVehicle is null)
        {
            LogSkippedVehicleMissing(oldVehicleId, oldLeaseId);
            return;
        }

        var replacement = await vehicles.FindAvailableReplacementAsync(
            evt.TenantId,
            oldVehicle.Id,
            oldVehicle.CurrentBranchId,
            oldVehicle.BodyType,
            oldVehicle.Seats,
            cancellationToken).ConfigureAwait(false);

        if (replacement is null)
        {
            LogReplacementUnavailable(evt.IncidentId, oldLeaseId, oldVehicle.CurrentBranchId);
            return;
        }

        if (!TryBuildSaveCommand(evt.IncidentId, oldLease, replacement.Id, out var saveCommand))
        {
            LogSkippedMissingLeaseRefs(oldLeaseId, evt.IncidentId);
            return;
        }

        var save = await mediator.Send(saveCommand, cancellationToken).ConfigureAwait(false);
        if (!save.Success || save.LeaseId is not { } newLeaseId)
        {
            LogSaveFailed(evt.IncidentId, oldLeaseId, save.ErrorCode ?? "unknown", save.ErrorMessage);
            return;
        }

        var now = clock.UtcNow;
        var newLease = await leases.GetByIdAsync(evt.TenantId, newLeaseId, cancellationToken).ConfigureAwait(false);
        if (newLease is null)
        {
            LogSaveProducedMissingLease(newLeaseId, evt.IncidentId);
            return;
        }

        incident.LinkReplacementLease(newLeaseId, now);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!await TryCloseOldLeaseAsync(oldLease, oldVehicle, now, cancellationToken).ConfigureAwait(false))
        {
            await TryCompensateAsync(newLease, replacement, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        LogReplacementCompleted(evt.IncidentId, oldLease.Id, newLease.Id);
    }

    private static bool TryBuildSaveCommand(
        Guid incidentId,
        AutoLeaseNet.Domain.Leases.Lease oldLease,
        Guid replacementVehicleId,
        out SaveContractCommand command)
    {
        command = default!;
        if (oldLease.CustomerId is not { } customerId ||
            oldLease.PrimaryDriverId is not { } primaryDriverId ||
            oldLease.RentPolicyId is not { } rentPolicyId ||
            oldLease.WorkingBranchId is not { } workingBranchId ||
            oldLease.ReceiveBranchId is not { } receiveBranchId ||
            oldLease.ReturnBranchId is not { } returnBranchId)
        {
            return false;
        }

        command = new SaveContractCommand
        {
            IdempotencyKey = $"replacement-saga:{incidentId:N}",
            CustomerId = customerId,
            VehicleId = replacementVehicleId,
            PrimaryDriverId = primaryDriverId,
            ExtraDriverId = oldLease.ExtraDriverId,
            AuthorizedDriverId = oldLease.AuthorizedDriverId,
            RentPolicyId = rentPolicyId,
            ExtendedCoverageId = oldLease.ExtendedCoverageId,
            WorkingBranchId = workingBranchId,
            ReceiveBranchId = receiveBranchId,
            ReturnBranchId = returnBranchId,
            ContractStartUtc = oldLease.ContractStartUtc,
            ContractEndUtc = oldLease.ContractEndUtc,
            ContractTypeCode = oldLease.ContractTypeCode,
            AllowedKmPerHour = oldLease.AllowedKmPerHour,
            AllowedKmPerDay = oldLease.AllowedKmPerDay,
            UnlimitedKm = oldLease.UnlimitedKm,
            AllowedLateHours = oldLease.AllowedLateHours,
            RentAmount = oldLease.RentAmount,
            PaidAmount = oldLease.PaidAmount,
            PaymentMethodCode = oldLease.PaymentMethodCode,
            DiscountType = oldLease.DiscountType,
            DiscountValue = oldLease.DiscountValue,
        };
        return true;
    }

    private async Task<bool> TryCloseOldLeaseAsync(
        AutoLeaseNet.Domain.Leases.Lease oldLease,
        AutoLeaseNet.Domain.Vehicles.Vehicle oldVehicle,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (oldLease.TajeerContractNumber is not { } contractNumber)
        {
            LogCloseSkippedNoContract(oldLease.Id);
            return false;
        }

        var close = await tajeer.CloseAsync(new CloseContractRequest
        {
            ContractNumber = contractNumber,
            ClosureMainReasonCode = 2, // ClosureBeforePeriodExpiration
            ClosureSubReasonCode = 10, // ClosureForReplacementOrUpgrade
            ReturnDate = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ReturnedKm = oldLease.EndKm ?? oldVehicle.CurrentKm,
            ReturnedFuelLevelCode = oldLease.ReturnFuelLevelCode ?? 3,
            ReturnConditionNotes = "Closed by replacement saga",
            DamagesObserved = oldLease.DamagesObserved,
            FinalPaidAmount = oldLease.PaidAmount,
            DiscountAmount = oldLease.DiscountValue,
        }, ct).ConfigureAwait(false);

        if (!close.IsSuccess)
        {
            LogCloseFailed(oldLease.Id, close.ErrorCode ?? "unknown", close.ErrorMessage);
            return false;
        }

        oldLease.MarkClosed(
            closureMainReasonCode: 2,
            closureSubReasonCode: 10,
            endKm: oldLease.EndKm ?? oldVehicle.CurrentKm,
            returnFuelLevelCode: oldLease.ReturnFuelLevelCode,
            returnConditionNotes: "Closed by replacement saga",
            damagesObserved: oldLease.DamagesObserved,
            nowUtc: now);
        oldVehicle.EnterService(now);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    private async Task TryCompensateAsync(
        AutoLeaseNet.Domain.Leases.Lease newLease,
        AutoLeaseNet.Domain.Vehicles.Vehicle replacementVehicle,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (newLease.TajeerContractNumber is not { } newContractNumber)
        {
            LogCompensationSkippedNoContract(newLease.Id);
            return;
        }

        var cancel = await tajeer.CancelAsync(new CancelContractRequest
        {
            ContractNumber = newContractNumber,
        }, ct).ConfigureAwait(false);

        if (!cancel.IsSuccess)
        {
            LogCompensationFailed(newLease.Id, cancel.ErrorCode ?? "unknown", cancel.ErrorMessage);
            return;
        }

        newLease.MarkCancelled("replacement.close_failed.compensation", now);
        replacementVehicle.ReleaseReservation(now);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        LogCompensationSucceeded(newLease.Id, newContractNumber);
    }

    [LoggerMessage(EventId = 8501, Level = LogLevel.Information,
        Message = "Replacement saga skipped for Incident {IncidentId} — no LeaseId on event.")]
    partial void LogSkippedNoLease(Guid incidentId);

    [LoggerMessage(EventId = 8502, Level = LogLevel.Warning,
        Message = "Replacement saga skipped — Incident {IncidentId} not found in tenant {TenantId}.")]
    partial void LogSkippedIncidentMissing(Guid incidentId, Guid tenantId);

    [LoggerMessage(EventId = 8503, Level = LogLevel.Information,
        Message = "Replacement saga idempotent skip — Incident {IncidentId} already linked to replacement Lease {ReplacementLeaseId}.")]
    partial void LogSkippedAlreadyLinked(Guid incidentId, Guid replacementLeaseId);

    [LoggerMessage(EventId = 8504, Level = LogLevel.Warning,
        Message = "Replacement saga skipped — Lease {LeaseId} not found for Incident {IncidentId}.")]
    partial void LogSkippedLeaseMissing(Guid leaseId, Guid incidentId);

    [LoggerMessage(EventId = 8505, Level = LogLevel.Warning,
        Message = "Replacement saga skipped — Lease {LeaseId} has no VehicleId (Incident {IncidentId}).")]
    partial void LogSkippedLeaseVehicleMissing(Guid leaseId, Guid incidentId);

    [LoggerMessage(EventId = 8506, Level = LogLevel.Warning,
        Message = "Replacement saga skipped — Vehicle {VehicleId} not found for Lease {LeaseId}.")]
    partial void LogSkippedVehicleMissing(Guid vehicleId, Guid leaseId);

    [LoggerMessage(EventId = 8507, Level = LogLevel.Warning,
        Message = "Replacement unavailable for Incident {IncidentId}, Lease {LeaseId}, preferred branch {PreferredBranchId}.")]
    partial void LogReplacementUnavailable(Guid incidentId, Guid leaseId, Guid preferredBranchId);

    [LoggerMessage(EventId = 8508, Level = LogLevel.Warning,
        Message = "Replacement saga skipped — Lease {LeaseId} missing required refs for SaveContract clone (Incident {IncidentId}).")]
    partial void LogSkippedMissingLeaseRefs(Guid leaseId, Guid incidentId);

    [LoggerMessage(EventId = 8509, Level = LogLevel.Warning,
        Message = "Replacement save failed for Incident {IncidentId}, Lease {LeaseId}. ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}")]
    partial void LogSaveFailed(Guid incidentId, Guid leaseId, string errorCode, string? errorMessage);

    [LoggerMessage(EventId = 8510, Level = LogLevel.Error,
        Message = "Replacement save reported LeaseId {NewLeaseId} but repository lookup failed (Incident {IncidentId}).")]
    partial void LogSaveProducedMissingLease(Guid newLeaseId, Guid incidentId);

    [LoggerMessage(EventId = 8511, Level = LogLevel.Warning,
        Message = "Replacement close skipped — old Lease {LeaseId} has no Tajeer contract number.")]
    partial void LogCloseSkippedNoContract(Guid leaseId);

    [LoggerMessage(EventId = 8512, Level = LogLevel.Warning,
        Message = "Replacement close failed for old Lease {LeaseId}. ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}")]
    partial void LogCloseFailed(Guid leaseId, string errorCode, string? errorMessage);

    [LoggerMessage(EventId = 8513, Level = LogLevel.Warning,
        Message = "Replacement compensation skipped — new Lease {NewLeaseId} has no Tajeer contract number.")]
    partial void LogCompensationSkippedNoContract(Guid newLeaseId);

    [LoggerMessage(EventId = 8514, Level = LogLevel.Error,
        Message = "Replacement compensation failed for new Lease {NewLeaseId}. ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}")]
    partial void LogCompensationFailed(Guid newLeaseId, string errorCode, string? errorMessage);

    [LoggerMessage(EventId = 8515, Level = LogLevel.Information,
        Message = "Replacement compensation succeeded for new Lease {NewLeaseId}; cancelled contract {ContractNumber}.")]
    partial void LogCompensationSucceeded(Guid newLeaseId, long contractNumber);

    [LoggerMessage(EventId = 8516, Level = LogLevel.Information,
        Message = "Replacement saga completed. Incident {IncidentId}, old Lease {OldLeaseId}, new Lease {NewLeaseId}.")]
    partial void LogReplacementCompleted(Guid incidentId, Guid oldLeaseId, Guid newLeaseId);
}
