using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class DeleteVehicleCommandHandler(
    IVehicleRepository vehicles,
    IVehicleHistoryRepository history,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock,
    ILogger<DeleteVehicleCommandHandler> logger)
    : IRequestHandler<DeleteVehicleCommand, VehicleCommandResult>
{
    public async Task<VehicleCommandResult> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;

        var vehicle = await vehicles.GetByIdAsync(tenantId, request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
            return new VehicleCommandResult(true, request.VehicleId, null, null); // idempotent

        if (vehicle.Status == VehicleStatus.OnRent)
            return new VehicleCommandResult(false, null, "vehicle.on_rent", "Cannot delete a vehicle that is currently on active rental.");

        // Soft-delete: transition to Disposed before hard-deleting so history is preserved via history event.
        history.Add(VehicleHistoryEvent.Create(
            tenantId, vehicle.Id,
            VehicleHistoryEventType.StatusChanged,
            $"Vehicle deleted from fleet. Previous status: {vehicle.Status}.",
            clock.UtcNow,
            previousValue: vehicle.Status.ToString(),
            newValue: "Deleted"));

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await vehicles.DeleteAsync(tenantId, request.VehicleId, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogDeleted(request.VehicleId, tenantId);
        return new VehicleCommandResult(true, request.VehicleId, null, null);
    }

    [LoggerMessage(EventId = 9621, Level = LogLevel.Warning,
        Message = "Vehicle {VehicleId} deleted from tenant {TenantId}")]
    partial void LogDeleted(Guid vehicleId, Guid tenantId);
}
