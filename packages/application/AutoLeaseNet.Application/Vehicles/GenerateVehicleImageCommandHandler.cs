using AutoLeaseNet.Application.Ports.Images;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Vehicles;

public sealed partial class GenerateVehicleImageCommandHandler(
    IVehicleRepository vehicles,
    IVehicleHistoryRepository history,
    IVehicleImageRepository images,
    IVehicleImageService imageService,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock,
    ILogger<GenerateVehicleImageCommandHandler> logger)
    : IRequestHandler<GenerateVehicleImageCommand, VehicleCommandResult>
{
    public async Task<VehicleCommandResult> Handle(GenerateVehicleImageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;

        var vehicle = await vehicles.GetByIdAsync(tenantId, request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
            return new VehicleCommandResult(false, null, "vehicle.not_found", $"Vehicle {request.VehicleId} not found.");

        VehicleImageResult generated;
        try
        {
            generated = await imageService.GenerateAsync(vehicle.Make, vehicle.Model, vehicle.Color, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogImageGenFailed(vehicle.Id, ex.Message);
            return new VehicleCommandResult(false, null, "image.generation_failed", ex.Message);
        }

        var image = VehicleImage.Create(
            tenantId, vehicle.Id,
            generated.ImageUrl,
            clock.UtcNow,
            thumbnailUrl: generated.ThumbnailUrl,
            altText: generated.AltText,
            isAiGenerated: generated.IsAiGenerated);

        images.Add(image);

        history.Add(VehicleHistoryEvent.Create(
            tenantId, vehicle.Id,
            VehicleHistoryEventType.ImageAdded,
            $"AI-generated image added for {vehicle.Make} {vehicle.Model} ({vehicle.Color ?? "unknown color"}).",
            clock.UtcNow,
            newValue: image.ImageUrl));

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogImageGenerated(image.Id, vehicle.Id, tenantId);
        return new VehicleCommandResult(true, image.Id, null, null);
    }

    [LoggerMessage(EventId = 9651, Level = LogLevel.Information,
        Message = "AI image {ImageId} generated for vehicle {VehicleId} in tenant {TenantId}")]
    partial void LogImageGenerated(Guid imageId, Guid vehicleId, Guid tenantId);

    [LoggerMessage(EventId = 9652, Level = LogLevel.Warning,
        Message = "AI image generation failed for vehicle {VehicleId}: {Reason}")]
    partial void LogImageGenFailed(Guid vehicleId, string reason);
}
