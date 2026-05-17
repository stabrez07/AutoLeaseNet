using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Adapters.Tajeer.Client;

namespace AutoLeaseNet.Adapters.Tajeer.InMemory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryTajeer(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTajeerClient>();
        services.AddSingleton<ITajeerClient>(sp => sp.GetRequiredService<InMemoryTajeerClient>());
        return services;
    }
}
