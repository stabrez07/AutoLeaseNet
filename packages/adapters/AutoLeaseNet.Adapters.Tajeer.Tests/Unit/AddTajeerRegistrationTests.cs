using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Lookups;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

public sealed class AddTajeerRegistrationTests
{
    private static IServiceProvider BuildProviderWithValidConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
                ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
                ["Tajeer:AppId"] = "app",
                ["Tajeer:AppKey"] = "key",
                ["Tajeer:AuthorizationToken"] = "Basic token",
                ["Tajeer:BranchId"] = "1",
                ["Tajeer:TimeoutSeconds"] = "10",
                ["Tajeer:WebhookSharedSecret"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTajeer(config.GetSection(TajeerOptions.SectionName));
        return services.BuildServiceProvider();
    }

    // T3.4 — AddTajeer registers the named HttpClient with the right base address + timeout.
    [Fact]
    public void Resolves_named_HttpClient_with_base_address_and_timeout()
    {
        using var provider = (ServiceProvider)BuildProviderWithValidConfig();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(ServiceCollectionExtensions.TajeerHttpClientName);

        client.BaseAddress.Should().Be(new Uri("https://tajeer-stg.api.elm.sa", UriKind.Absolute));
        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    // T3.4 — TajeerOptions resolves and validates.
    [Fact]
    public void Resolves_validated_TajeerOptions()
    {
        using var provider = (ServiceProvider)BuildProviderWithValidConfig();
        var options = provider.GetRequiredService<IOptions<TajeerOptions>>().Value;

        options.AppId.Should().Be("app");
        options.AppKey.Should().Be("key");
        options.AuthorizationToken.Should().Be("Basic token");
        options.BranchId.Should().Be(1);
    }

    // T3.5 — TajeerLookupClient is wired and resolves from a scope.
    [Fact]
    public void Resolves_TajeerLookupClient_from_scope()
    {
        using var provider = (ServiceProvider)BuildProviderWithValidConfig();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<TajeerLookupClient>();

        client.Should().NotBeNull();
    }
}
