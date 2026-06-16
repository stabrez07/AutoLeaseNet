using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class UpdateVehicleCommandHandler(
    IVehicleRepository vehicles,
    IVehicleHistoryRepository history,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateVehicleCommandHandler> logger)
    : IRequestHandler<UpdateVehicleCommand, VehicleCommandResult>
{
    public async Task<VehicleCommandResult> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenant.TenantId;

        var vehicle = await vehicles.GetByIdAsync(tenantId, request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
            return Fail("vehicle.not_found", $"Vehicle {request.VehicleId} not found.");

        var prevStatus = vehicle.Status.ToString();

        try
        {
            vehicle.Update(new VehicleUpdateInput
            {
                Color = request.Color,
                Seats = request.Seats,
                Make = request.Make,
                Model = request.Model,
                ModelYear = request.ModelYear,
                InsuranceCompany = request.InsuranceCompany,
                InsurancePolicyNumber = request.InsurancePolicyNumber,
                LicenseExpiryDate = ParseDate(request.LicenseExpiryDate),
                InsuranceExpiryDate = ParseDate(request.InsuranceExpiryDate),
                InspectionExpiryDate = ParseDate(request.InspectionExpiryDate),
                CurrentBranchId = request.CurrentBranchId,
                CurrentKm = request.CurrentKm,
                PurchasePrice = request.PurchasePrice,
                PurchaseDate = ParseDate(request.PurchaseDate),
                Notes = request.Notes,
                NowUtc = clock.UtcNow,
                UpdatedBy = Guid.Empty,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("vehicle.invalid_input", ex.Message);
        }

        history.Add(VehicleHistoryEvent.Create(
            tenantId, vehicle.Id,
            VehicleHistoryEventType.FieldsUpdated,
            "Vehicle fields updated via staff portal.",
            clock.UtcNow));

        await vehicles.UpdateAsync(vehicle, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogUpdated(vehicle.Id, tenantId);
        return new VehicleCommandResult(true, vehicle.Id, null, null);
    }

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;

    private static VehicleCommandResult Fail(string code, string msg) => new(false, null, code, msg);

    [LoggerMessage(EventId = 9611, Level = LogLevel.Information,
        Message = "Vehicle {VehicleId} updated for tenant {TenantId}")]
    partial void LogUpdated(Guid vehicleId, Guid tenantId);
}
