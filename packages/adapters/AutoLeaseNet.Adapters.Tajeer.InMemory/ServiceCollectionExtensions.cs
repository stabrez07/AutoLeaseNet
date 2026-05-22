using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Client;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Tajeer.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the legacy <see cref="ITajeerClient"/> facade with an in-memory backing.
    /// Pre-dates the Pattern B sub-clients; kept for tests that still consume the root
    /// facade. New code should depend on the individual sub-clients (e.g.
    /// <see cref="ITajeerContractClient"/>).
    /// </summary>
    public static IServiceCollection AddInMemoryTajeer(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTajeerClient>();
        services.AddSingleton<ITajeerClient>(sp => sp.GetRequiredService<InMemoryTajeerClient>());
        return services;
    }

    /// <summary>
    /// Replaces any prior <see cref="ITajeerContractClient"/> registration with the
    /// in-memory fake. Call AFTER <see cref="AutoLeaseNet.Adapters.Tajeer.ServiceCollectionExtensions.AddTajeer"/>
    /// from a composition root that wants the InMemory adapter (tests, offline dev).
    /// </summary>
    public static IServiceCollection AddInMemoryTajeerContracts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryTajeerContractClient>();
        services.Replace(ServiceDescriptor.Singleton<ITajeerContractClient>(
            sp => sp.GetRequiredService<InMemoryTajeerContractClient>()));
        return services;
    }

    /// <summary>
    /// Convenience wrapper that calls
    /// <see cref="AutoLeaseNet.Adapters.Tajeer.ServiceCollectionExtensions.AddTajeer"/> and
    /// then — if <see cref="TajeerOptions.Mode"/> is <see cref="TajeerMode.InMemory"/> —
    /// swaps <see cref="ITajeerContractClient"/> for the in-memory fake. Production
    /// composition roots can call this directly so the mode switch is one line.
    /// </summary>
    public static IServiceCollection AddTajeerWithModeSwitch(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        services.AddTajeer(configurationSection);

        var mode = ReadMode(configurationSection);
        if (mode == TajeerMode.InMemory)
        {
            services.AddInMemoryTajeerContracts();
        }
        return services;
    }

    // Read just the Mode field — avoids re-binding the whole options object (which would
    // trip ValidateDataAnnotations if required fields aren't populated yet).
    private static TajeerMode ReadMode(IConfigurationSection section)
    {
        var raw = section[nameof(TajeerOptions.Mode)];
        return Enum.TryParse<TajeerMode>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : TajeerMode.Real;
    }
}
