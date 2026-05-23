using AutoLeaseNet.Application.Ports.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Adapters.Seed;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IDataSeeder"/> implementation selected by
    /// <c>Seed:Mode</c> config:
    /// <list type="bullet">
    ///   <item><c>Demo</c> → <see cref="BogusDataSeeder"/></item>
    ///   <item><c>Empty</c> → <see cref="EmptyDataSeeder"/></item>
    ///   <item><c>ImportedFile</c> → throws <see cref="NotImplementedException"/> on resolve (placeholder for the future data-management module).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddSeed(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        services.AddSingleton(_ => configurationSection.Get<SeedOptions>() ?? new SeedOptions());

        services.AddScoped<IDataSeeder>(sp =>
        {
            var options = sp.GetRequiredService<SeedOptions>();
            return options.Mode switch
            {
                SeedMode.Demo => ActivatorUtilities.CreateInstance<BogusDataSeeder>(sp),
                SeedMode.Empty => new EmptyDataSeeder(options),
                SeedMode.ImportedFile => throw new NotImplementedException(
                    "Seed:Mode = ImportedFile reserved for the future data-management module."),
                _ => new EmptyDataSeeder(options),
            };
        });

        return services;
    }
}
