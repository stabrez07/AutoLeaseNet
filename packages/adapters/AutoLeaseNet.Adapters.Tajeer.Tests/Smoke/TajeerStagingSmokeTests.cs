using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Lookups;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Smoke;

/// <summary>
/// T3.6 — smoke tests against real Tajeer Rabet staging. These are EXCLUDED from default
/// `dotnet test` runs via `[Trait("Category","Smoke")]` and the solution-level test runsettings.
/// Run explicitly with:
///   <c>dotnet test --filter Category=Smoke</c>
///
/// Credentials are loaded from .NET user-secrets (UserSecretsId on this csproj) or from
/// TAJEER_* environment variables. If <c>Tajeer:AppId</c> is missing the test exits early
/// (treated as configuration-skipped, never as a failure) so CI stays green when secrets
/// aren't provisioned.
///
/// User-secrets one-liner (run from the test project directory):
///   <c>dotnet user-secrets set "Tajeer:AppId" "..." </c>
///   <c>dotnet user-secrets set "Tajeer:AppKey" "..." </c>
///   <c>dotnet user-secrets set "Tajeer:AuthorizationToken" "Basic ..." </c>
///   <c>dotnet user-secrets set "Tajeer:BranchId" "1" </c>
///   <c>dotnet user-secrets set "Tajeer:WebhookSharedSecret" "..." </c>
/// </summary>
[Trait("Category", "Smoke")]
public sealed class TajeerStagingSmokeTests
{
    private readonly ITestOutputHelper _output;

    public TajeerStagingSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetAllBranchesAsync_against_real_staging_returns_non_empty_list()
    {
        var config = BuildConfiguration();

        if (string.IsNullOrWhiteSpace(config["Tajeer:AppId"]))
        {
            _output.WriteLine("SKIP: Tajeer:AppId not configured in user-secrets / env. Smoke test skipped.");
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTajeer(config.GetSection(TajeerOptions.SectionName));
        await using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<TajeerLookupClient>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var result = await sut.GetAllBranchesAsync(cts.Token);

        result.IsSuccess.Should().BeTrue(
            because: $"staging should return 200 OK; got error {result.ErrorCode} — {result.ErrorMessage}");
        result.Value.Should().NotBeNullOrEmpty(because: "every Tajeer tenant has at least one branch");

        var first = result.Value![0];
        _output.WriteLine($"Branch count: {result.Value.Count}");
        _output.WriteLine($"First branch: id={first.Id} code={first.Code} nameEn={first.NameEn} city={first.CityEn} active={first.IsActive}");
    }

    private static IConfiguration BuildConfiguration()
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
            ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
            ["Tajeer:TimeoutSeconds"] = "30",
            ["Tajeer:IsSandbox"] = "true",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .AddUserSecrets<TajeerStagingSmokeTests>(optional: true)
            .AddEnvironmentVariables(prefix: "TAJEER_")
            .Build();
    }

}
