using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Infrastructure.Reconciliation;

public static class ReconciliationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ReconciliationOptions"/>, the
    /// <see cref="ReconciliationService"/> hosted service, and every shipped
    /// <see cref="IReconciliationCheck"/> implementation. Future workstreams
    /// add their checks here.
    /// </summary>
    public static IServiceCollection AddReconciliation(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.Configure<ReconciliationOptions>(section);
        services.AddHostedService<ReconciliationService>();
        services.AddScoped<IReconciliationCheck, TajeerStatusMirrorCheck>();
        return services;
    }
}
