using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Adapters.Zatca.InMemory;

public sealed class InMemoryZatcaClient : IZatcaClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryZatca(this IServiceCollection services)
    {
        services.AddSingleton<IZatcaClient, InMemoryZatcaClient>();
        return services;
    }
}
