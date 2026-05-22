using System.ComponentModel.DataAnnotations;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

public sealed class TajeerOptionsTests
{
    private static IReadOnlyDictionary<string, string?> ValidConfig() => new Dictionary<string, string?>
    {
        ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
        ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
        ["Tajeer:AppId"] = "app-id-1",
        ["Tajeer:AppKey"] = "app-key-1",
        ["Tajeer:AuthorizationToken"] = "Basic abc123",
        ["Tajeer:BranchId"] = "1",
        ["Tajeer:TimeoutSeconds"] = "30",
        ["Tajeer:WebhookSharedSecret"] = "secret",
        ["Tajeer:IsSandbox"] = "true",
    };

    private static TajeerOptions BuildResolvedOptions(IReadOnlyDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<TajeerOptions>()
            .Bind(config.GetSection(TajeerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<TajeerOptions>>().Value;
    }

    // T3.1 — happy path: valid config binds and validates.
    [Fact]
    public void Binds_from_in_memory_config_and_passes_validation()
    {
        var opt = BuildResolvedOptions(ValidConfig());

        opt.BaseUrl.Should().Be("https://tajeer-stg.api.elm.sa");
        opt.IssuanceUrlBase.Should().Be("https://tajeerstg.logisti.sa");
        opt.AppId.Should().Be("app-id-1");
        opt.AppKey.Should().Be("app-key-1");
        opt.AuthorizationToken.Should().Be("Basic abc123");
        opt.BranchId.Should().Be(1);
        opt.TimeoutSeconds.Should().Be(30);
        opt.RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        opt.WebhookSharedSecret.Should().Be("secret");
        opt.IsSandbox.Should().BeTrue();
    }

    // T3.1 — missing required field surfaces a validation error at first resolve.
    [Theory]
    [InlineData("Tajeer:AppId")]
    [InlineData("Tajeer:AppKey")]
    [InlineData("Tajeer:AuthorizationToken")]
    [InlineData("Tajeer:BaseUrl")]
    [InlineData("Tajeer:IssuanceUrlBase")]
    [InlineData("Tajeer:WebhookSharedSecret")]
    public void Missing_required_field_throws_OptionsValidationException(string missingKey)
    {
        var values = ValidConfig().Where(kv => kv.Key != missingKey)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Action act = () => _ = BuildResolvedOptions(values);

        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().NotBeEmpty();
    }

    // T3.1 — TimeoutSeconds is range-constrained (Range(1, 600)).
    [Theory]
    [InlineData("0")]
    [InlineData("601")]
    public void TimeoutSeconds_outside_range_fails_validation(string invalidTimeout)
    {
        var values = ValidConfig().ToDictionary(kv => kv.Key, kv => kv.Value);
        values["Tajeer:TimeoutSeconds"] = invalidTimeout;

        Action act = () => _ = BuildResolvedOptions(values);

        act.Should().Throw<OptionsValidationException>();
    }
}
