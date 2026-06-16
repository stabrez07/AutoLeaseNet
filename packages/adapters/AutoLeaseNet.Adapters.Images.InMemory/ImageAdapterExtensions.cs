using AutoLeaseNet.Application.Ports.Images;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Adapters.Images.InMemory;

public static class ImageAdapterExtensions
{
    /// <summary>
    /// Registers the in-memory (mock/dev) AI vehicle image service.
    /// Swap for the real adapter (e.g. AddOpenAiVehicleImages) when credentials are available.
    /// </summary>
    public static IServiceCollection AddInMemoryVehicleImages(this IServiceCollection services)
    {
        services.AddSingleton<IVehicleImageService, InMemoryVehicleImageService>();
        return services;
    }
}
