using AutoLeaseNet.Adapters.Common.Resilience;
using AutoLeaseNet.Adapters.Zatca.Authentication;
using AutoLeaseNet.Adapters.Zatca.Client;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Zatca;

public static class ServiceCollectionExtensions
{
    /// <summary>Named HttpClient for Fatoorah; consume via <c>IHttpClientFactory.CreateClient(ZatcaHttpClientName)</c>.</summary>
    public const string ZatcaHttpClientName = "zatca";

    /// <summary>
    /// Registers ZATCA adapter services per Spec 04 §5 / Spec 02 §4.5:
    /// - <see cref="ZatcaOptions"/> bound + data-annotation validated at startup
    /// - <see cref="ZatcaAuthHandler"/> as a transient delegating handler
    /// - Named <see cref="HttpClient"/> with BaseAddress, Timeout, auth handler, and the
    ///   shared Polly v8 resilience pipeline (matches the Tajeer wiring exactly)
    /// - Default <see cref="IZatcaClient"/> registration → real <see cref="ZatcaClient"/>
    ///   (Phase-1 stub; switch to InMemory via <see cref="AutoLeaseNet.Adapters.Zatca.InMemory.ServiceCollectionExtensions.AddZatcaWithModeSwitch"/>)
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

        services.AddTransient<ZatcaAuthHandler>();

        services
            .AddHttpClient(ZatcaHttpClientName, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ZatcaOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
                client.Timeout = opts.RequestTimeout;
            })
            .AddHttpMessageHandler<ZatcaAuthHandler>()
            .AddResilienceHandler("zatca-resilience", (pipelineBuilder, context) =>
            {
                ResiliencePolicies.DefaultHttpPipeline(pipelineBuilder, context);
            });

        // Default registration → real (stubbed) client. AddZatcaWithModeSwitch in the
        // InMemory package swaps this for the fake when Zatca:Mode=InMemory.
        services.AddScoped<ZatcaClient>();
        services.AddScoped<IZatcaClient>(sp => sp.GetRequiredService<ZatcaClient>());

        return services;
    }
}
