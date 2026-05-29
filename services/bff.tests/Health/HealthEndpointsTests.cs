using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Health;

/// <summary>
/// T2.4 + T2.5 — health probe endpoints.
/// Liveness: 200 OK, anonymous, no downstream checks.
/// Readiness: 200 OK when SQL reachable, 503 otherwise.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<HealthTestFactory>
{
    private readonly HealthTestFactory _factory;

    public HealthEndpointsTests(HealthTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Liveness_returns_200_without_auth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/liveness");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Liveness_does_not_require_tenant_header()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/liveness");

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Readiness_returns_200_when_SQL_reachable()
    {
        // Hits the local AutoLeaseNet_Dev DB via the connection string in appsettings.Development.json.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/readiness");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_returns_503_when_SQL_unreachable()
    {
        await using var brokenFactory = new BrokenSqlHealthTestFactory();
        var client = brokenFactory.CreateClient();

        var response = await client.GetAsync("/health/readiness");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}

public sealed class HealthTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AutoLeaseNet"] =
                    "Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=false",
                ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
                ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
                ["Tajeer:AppId"] = "test-app",
                ["Tajeer:AppKey"] = "test-key",
                ["Tajeer:AuthorizationToken"] = "Basic test",
                ["Tajeer:BranchId"] = "1",
                ["Tajeer:TimeoutSeconds"] = "10",
                ["Tajeer:WebhookSharedSecret"] = "test-secret",
                ["Tajeer:Mode"] = "InMemory",
                ["Outbox:Enabled"] = "false",
                ["Seed:Mode"] = "Empty",
            });
        });
    }
}

/// <summary>Overrides the SQL connection string to point at a port nothing's listening on.</summary>
public sealed class BrokenSqlHealthTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Port 11111 is reserved/unused → SqlException ConnectionFailure within timeout
                ["ConnectionStrings:AutoLeaseNet"] =
                    "Server=localhost,11111;Database=AutoLeaseNet_Dev;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=true;Encrypt=false",
                ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
                ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
                ["Tajeer:AppId"] = "test-app",
                ["Tajeer:AppKey"] = "test-key",
                ["Tajeer:AuthorizationToken"] = "Basic test",
                ["Tajeer:BranchId"] = "1",
                ["Tajeer:TimeoutSeconds"] = "10",
                ["Tajeer:WebhookSharedSecret"] = "test-secret",
                ["Tajeer:Mode"] = "InMemory",
                ["Outbox:Enabled"] = "false",
                ["Seed:Mode"] = "Empty",
            });
        });
    }
}
