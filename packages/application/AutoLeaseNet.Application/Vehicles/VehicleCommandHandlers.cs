using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class CreateVehicleCommandHandler(
    IVehicleRepository vehicles,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateVehicleCommandHandler> logger)
    : IRequestHandler<CreateVehicleCommand, VehicleCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<VehicleCommandResult> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("vehicle.idempotency_required", "CreateVehicle requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:vehicle-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<VehicleCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        DateOnly? licenseExpiry = ParseDate(request.LicenseExpiryDate);
        DateOnly? insuranceExpiry = ParseDate(request.InsuranceExpiryDate);
        DateOnly? inspectionExpiry = ParseDate(request.InspectionExpiryDate);
        DateOnly? purchaseDate = ParseDate(request.PurchaseDate);

        Vehicle vehicle;
        try
        {
            vehicle = Vehicle.Create(new VehicleCreateInput
            {
                TenantId = tenantId,
                PlateNumber = request.PlateNumber,
                PlateLetters = request.PlateLetters,
                PlateTypeCode = request.PlateTypeCode,
                Vin = request.Vin,
                EngineNumber = request.EngineNumber,
                Make = request.Make,
                Model = request.Model,
                ModelYear = request.ModelYear,
                Color = request.Color,
                FuelType = (FuelType)request.FuelType,
                TransmissionType = (TransmissionType)request.TransmissionType,
                BodyType = (BodyType)request.BodyType,
                Seats = request.Seats,
                LicenseExpiryDate = licenseExpiry,
                InsuranceExpiryDate = insuranceExpiry,
                InspectionExpiryDate = inspectionExpiry,
                InsuranceCompany = request.InsuranceCompany,
                InsurancePolicyNumber = request.InsurancePolicyNumber,
                OwnerBranchId = request.OwnerBranchId,
                CurrentKm = request.CurrentKm,
                PurchasePrice = request.PurchasePrice,
                PurchaseDate = purchaseDate,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("vehicle.invalid_input", ex.Message);
        }

        vehicles.Add(vehicle);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new VehicleCommandResult(true, vehicle.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(vehicle.Id, tenantId);
        return result;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;
    }

    private static VehicleCommandResult Fail(string code, string message) => new(false, null, code, message);

    [LoggerMessage(EventId = 9601, Level = LogLevel.Information,
        Message = "Vehicle {VehicleId} created for tenant {TenantId}")]
    partial void LogCreated(Guid vehicleId, Guid tenantId);

    [LoggerMessage(EventId = 9602, Level = LogLevel.Debug,
        Message = "CreateVehicle idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
