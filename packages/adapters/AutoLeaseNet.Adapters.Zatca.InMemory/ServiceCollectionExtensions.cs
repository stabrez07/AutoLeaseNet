using AutoLeaseNet.Application.Ports.Integrations;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoLeaseNet.Adapters.Zatca.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory ZATCA client for development and testing.
    /// Call AFTER <see cref="AutoLeaseNet.Adapters.Zatca.ServiceCollectionExtensions.AddZatca"/>
    /// from a composition root that wants the InMemory adapter, or use
    /// <see cref="AddZatcaWithModeSwitch"/> for automatic mode-based selection.
    /// </summary>
    public static IServiceCollection AddInMemoryZatca(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ZatcaClientInMemory>();
        services.Replace(ServiceDescriptor.Scoped<IZatcaClient>(
            sp => sp.GetRequiredService<ZatcaClientInMemory>()));
        
        return services;
    }

    /// <summary>
    /// Convenience wrapper that calls
    /// <see cref="AutoLeaseNet.Adapters.Zatca.ServiceCollectionExtensions.AddZatca"/> and
    /// then — if <see cref="ZatcaOptions.Mode"/> is <see cref="ZatcaMode.InMemory"/> —
    /// swaps <see cref="IZatcaClient"/> for the in-memory fake. Development composition
    /// roots can call this directly so the mode switch is one line.
    /// </summary>
    public static IServiceCollection AddZatcaWithModeSwitch(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        services.AddZatca(configurationSection);

        var mode = ReadMode(configurationSection);
        if (mode == ZatcaMode.InMemory)
        {
            services.AddInMemoryZatca();
        }

        return services;
    }

    // Read just the Mode field — avoids re-binding the whole options object (which would
    // trip ValidateDataAnnotations if required fields aren't populated yet).
    private static ZatcaMode ReadMode(IConfigurationSection section)
    {
        var raw = section[nameof(ZatcaOptions.Mode)];
        return Enum.TryParse<ZatcaMode>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : ZatcaMode.Real;
    }
}
