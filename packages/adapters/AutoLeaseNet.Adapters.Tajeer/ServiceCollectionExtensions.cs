using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Adapters.Tajeer.Configuration;

namespace AutoLeaseNet.Adapters.Tajeer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Tajeer adapter services. Per doc 04 §5 standard.
    /// Full registration (HttpClient, message handlers, resilience pipeline, health checks,
    /// telemetry, idempotency wrapper) will be added incrementally per doc 03.
    /// </summary>
    public static IServiceCollection AddTajeer(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        services.Configure<TajeerOptions>(configurationSection);
        // TODO: Add HttpClient with auth handler + logging handler + resilience pipeline.
        // TODO: Register ITajeerClient implementation, lookup cache, webhook validator, health check.
        return services;
    }
}
