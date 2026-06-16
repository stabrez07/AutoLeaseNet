using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class CreateServiceRecordCommandHandler(
    IVehicleRepository vehicles,
    IVehicleServiceRecordRepository serviceRecords,
    IVehicleHistoryRepository history,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateServiceRecordCommandHandler> logger)
    : IRequestHandler<CreateServiceRecordCommand, VehicleCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<VehicleCommandResult> Handle(CreateServiceRecordCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenant.TenantId;

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:service-record:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<VehicleCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null) return cached;

        var vehicle = await vehicles.GetByIdAsync(tenantId, request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
            return Fail("vehicle.not_found", $"Vehicle {request.VehicleId} not found.");

        if (!DateOnly.TryParseExact(request.ServicedAt, "yyyy-MM-dd", out var servicedAt))
            return Fail("service_record.invalid_date", "ServicedAt must be yyyy-MM-dd.");

        DateOnly? nextServiceDate = null;
        if (!string.IsNullOrWhiteSpace(request.NextServiceDate))
        {
            if (!DateOnly.TryParseExact(request.NextServiceDate, "yyyy-MM-dd", out var nd))
                return Fail("service_record.invalid_next_date", "NextServiceDate must be yyyy-MM-dd.");
            nextServiceDate = nd;
        }

        VehicleServiceRecord record;
        try
        {
            record = VehicleServiceRecord.Create(new VehicleServiceRecordInput
            {
                TenantId = tenantId,
                VehicleId = vehicle.Id,
                Type = (ServiceRecordType)request.Type,
                ServiceCode = request.ServiceCode,
                Description = request.Description,
                ServicedAt = servicedAt,
                OdometerAtService = request.OdometerAtService,
                CostSar = request.CostSar,
                Branch = request.Branch,
                Technician = request.Technician,
                PartsReplaced = request.PartsReplaced,
                NextServiceOdometer = request.NextServiceOdometer,
                NextServiceDate = nextServiceDate,
                Notes = request.Notes,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("service_record.invalid_input", ex.Message);
        }

        serviceRecords.Add(record);
        vehicle.RecordServiceCompletion(servicedAt, request.OdometerAtService, request.NextServiceOdometer, nextServiceDate, clock.UtcNow);
        await vehicles.UpdateAsync(vehicle, cancellationToken).ConfigureAwait(false);

        history.Add(VehicleHistoryEvent.Create(
            tenantId, vehicle.Id,
            VehicleHistoryEventType.ServiceRecorded,
            $"{(ServiceRecordType)request.Type} service recorded: {request.Description} @ {request.OdometerAtService:N0} km. Cost: SAR {request.CostSar:N0}.",
            clock.UtcNow,
            newValue: record.Id.ToString()));

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new VehicleCommandResult(true, record.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(record.Id, vehicle.Id, tenantId);
        return result;
    }

    private static VehicleCommandResult Fail(string code, string msg) => new(false, null, code, msg);

    [LoggerMessage(EventId = 9631, Level = LogLevel.Information,
        Message = "Service record {RecordId} created for vehicle {VehicleId} in tenant {TenantId}")]
    partial void LogCreated(Guid recordId, Guid vehicleId, Guid tenantId);
}
