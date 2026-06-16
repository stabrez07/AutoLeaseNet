using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class BulkCreateVehiclesCommandHandler(
    IVehicleRepository vehicles,
    IVehicleHistoryRepository history,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock,
    ILogger<BulkCreateVehiclesCommandHandler> logger)
    : IRequestHandler<BulkCreateVehiclesCommand, BulkVehicleCommandResult>
{
    public async Task<BulkVehicleCommandResult> Handle(BulkCreateVehiclesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenant.TenantId;
        var errors = new List<BulkVehicleRowError>();
        int created = 0, skipped = 0;

        for (int i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            cancellationToken.ThrowIfCancellationRequested();

            // Duplicate VIN check
            var existing = await vehicles.GetByVinAsync(tenantId, row.Vin, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                errors.Add(new BulkVehicleRowError(i + 2, "vehicle.duplicate_vin", $"VIN '{row.Vin}' already exists."));
                skipped++;
                continue;
            }

            // Duplicate plate check
            var existingPlate = await vehicles.GetByPlateAsync(tenantId, row.PlateNumber, row.PlateLetters, cancellationToken).ConfigureAwait(false);
            if (existingPlate is not null)
            {
                errors.Add(new BulkVehicleRowError(i + 2, "vehicle.duplicate_plate", $"Plate '{row.PlateNumber} {row.PlateLetters}' already exists."));
                skipped++;
                continue;
            }

            try
            {
                var vehicle = Vehicle.Create(new VehicleCreateInput
                {
                    TenantId = tenantId,
                    PlateNumber = row.PlateNumber,
                    PlateLetters = row.PlateLetters,
                    PlateTypeCode = row.PlateTypeCode,
                    Vin = row.Vin,
                    Make = row.Make,
                    Model = row.Model,
                    ModelYear = row.ModelYear,
                    Color = row.Color,
                    FuelType = (FuelType)row.FuelType,
                    TransmissionType = (TransmissionType)row.TransmissionType,
                    BodyType = (BodyType)row.BodyType,
                    Seats = row.Seats,
                    OwnerBranchId = row.OwnerBranchId,
                    CurrentKm = row.CurrentKm,
                    NowUtc = clock.UtcNow,
                });

                vehicles.Add(vehicle);
                history.Add(VehicleHistoryEvent.Create(
                    tenantId, vehicle.Id,
                    VehicleHistoryEventType.BulkImported,
                    $"Vehicle imported via bulk CSV upload. Row {i + 2}.",
                    clock.UtcNow));

                created++;
            }
            catch (ArgumentException ex)
            {
                errors.Add(new BulkVehicleRowError(i + 2, "vehicle.invalid_row", ex.Message));
                skipped++;
            }
        }

        if (created > 0)
            await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogBulkImport(created, skipped, tenantId);
        return new BulkVehicleCommandResult(errors.Count == 0, created, skipped, errors);
    }

    [LoggerMessage(EventId = 9641, Level = LogLevel.Information,
        Message = "Bulk import: created={Created} skipped={Skipped} for tenant {TenantId}")]
    partial void LogBulkImport(int created, int skipped, Guid tenantId);
}
