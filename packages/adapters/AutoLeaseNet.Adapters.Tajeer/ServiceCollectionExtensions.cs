using AutoLeaseNet.Adapters.Common.Resilience;
using AutoLeaseNet.Adapters.Tajeer.Authentication;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Lookups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Tajeer;

public static class ServiceCollectionExtensions
{
    /// <summary>Named HttpClient for Tajeer; consume via <c>IHttpClientFactory.CreateClient(TajeerHttpClientName)</c>.</summary>
    public const string TajeerHttpClientName = "tajeer";

    /// <summary>
    /// Registers Tajeer adapter services per Spec 04 §5 / Spec 03 §4–9:
    /// - <see cref="TajeerOptions"/> bound + data-annotation validated at startup
    /// - <see cref="TajeerAuthHandler"/> as a transient delegating handler
    /// - Named <see cref="HttpClient"/> with BaseAddress, Timeout, auth handler attached,
    ///   and the shared Polly v8 resilience pipeline (retry / timeout / breaker).
    ///
    /// Full sub-client wiring (ITajeerClient, lookup cache, webhook validator, health check)
    /// lands in subsequent tasks (T3.5+, T4.x).
    /// </summary>
    public static IServiceCollection AddTajeer(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        services
            .AddOptions<TajeerOptions>()
            .Bind(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<TajeerAuthHandler>();

        services
            .AddHttpClient(TajeerHttpClientName, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<TajeerOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
                client.Timeout = opts.RequestTimeout;
            })
            .AddHttpMessageHandler<TajeerAuthHandler>()
            .AddResilienceHandler("tajeer-resilience", (pipelineBuilder, context) =>
            {
                // Reuse the same Polly v8 strategies as PollyPipelineFactory so behaviour
                // matches everywhere; the resilience handler form integrates with
                // IHttpClientFactory's lifecycle.
                var opts = context.ServiceProvider.GetRequiredService<IOptions<TajeerOptions>>().Value;
                var resilience = new ResilienceOptions
                {
                    Timeout = opts.RequestTimeout,
                };
                ResiliencePolicies.DefaultHttpPipeline(pipelineBuilder, context);
                _ = resilience;
            });

        services.AddScoped<TajeerLookupClient>();

        // Real Tajeer HTTP impl is the default registration for ITajeerContractClient.
        // Composition roots can override via AddInMemoryTajeerContracts() — see
        // AutoLeaseNet.Adapters.Tajeer.InMemory or the AddTajeerWithModeSwitch helper.
        services.AddScoped<TajeerContractClient>();
        services.AddScoped<ITajeerContractClient>(sp => sp.GetRequiredService<TajeerContractClient>());

        return services;
    }
}
