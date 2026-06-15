using AutoLeaseNet.Application.Ports.Integrations;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Zatca;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ZATCA adapter services per Spec 02 §4.5 / Spec 07:
    /// - <see cref="ZatcaOptions"/> bound + validated at startup
    /// - Real HTTP client (ZatcaClient) with configurable BaseAddress and timeout
    /// - Wires IZatcaClient → ZatcaClient implementation
    /// 
    /// Phase-1 scope: ZatcaClient currently returns a clear-error stub.
    /// Week-4 swaps that for UBL 2.1 + ECDSA + TLV-QR signing.
    /// </summary>
    public static IServiceCollection AddZatca(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        services
            .AddOptions<ZatcaOptions>()
            .Bind(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register HTTP client for ZATCA
        services.AddHttpClient<ZatcaClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ZatcaOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
            client.Timeout = opts.RequestTimeout;
        });

        // Register IZatcaClient implementation — real HTTP-backed client
        services.AddScoped<IZatcaClient>(sp => sp.GetRequiredService<ZatcaClient>());

        return services;
    }
}
