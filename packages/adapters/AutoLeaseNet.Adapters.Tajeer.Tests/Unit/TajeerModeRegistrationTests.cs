using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// T4.8 — <c>Tajeer:Mode</c> picks the <see cref="ITajeerContractClient"/> implementation.
/// </summary>
public sealed class TajeerModeRegistrationTests
{
    private static IConfiguration BuildConfig(string mode) =>
        new ConfigurationBuilder()
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
                ["Tajeer:Mode"] = mode,
            })
            .Build();

    [Fact]
    public void Mode_Real_resolves_real_TajeerContractClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTajeerWithModeSwitch(BuildConfig("Real").GetSection(TajeerOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ITajeerContractClient>();

        client.Should().BeOfType<TajeerContractClient>();
    }

    [Fact]
    public void Mode_InMemory_resolves_InMemoryTajeerContractClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTajeerWithModeSwitch(BuildConfig("InMemory").GetSection(TajeerOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ITajeerContractClient>();

        client.Should().BeOfType<InMemoryTajeerContractClient>();
    }

    [Fact]
    public void Mode_missing_defaults_to_Real()
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
                // Tajeer:Mode intentionally omitted
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTajeerWithModeSwitch(config.GetSection(TajeerOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ITajeerContractClient>();

        client.Should().BeOfType<TajeerContractClient>(because: "missing Mode must default to Real so Production is never accidentally faked");
    }
}
