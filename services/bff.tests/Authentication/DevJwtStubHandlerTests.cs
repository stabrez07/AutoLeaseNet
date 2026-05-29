using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Authentication;

/// <summary>
/// T2.1 — DevJwtStubHandler reads tenant identity from X-Dev-* headers in Development env
/// and constructs a ClaimsPrincipal so downstream middleware/endpoints can rely on the
/// standard auth pipeline without needing a real IdP locally.
/// </summary>
public sealed class DevJwtStubHandlerTests : IClassFixture<DevWebApplicationFactory>
{
    private readonly DevWebApplicationFactory _factory;

    public DevJwtStubHandlerTests(DevWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Whoami_returns_tenant_id_claim_when_header_provided()
    {
        var client = _factory.CreateClient();
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", tenantId.ToString());

        var response = await client.GetAsync("/api/v1/dev/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var whoami = await response.Content.ReadFromJsonAsync<WhoamiResponse>();
        whoami.Should().NotBeNull();
        whoami!.IsAuthenticated.Should().BeTrue();
        whoami.TenantId.Should().Be(tenantId.ToString());
    }

    [Fact]
    public async Task Whoami_returns_401_when_no_X_Dev_Tenant_Id_header()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dev/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Whoami_carries_user_type_customer_id_and_branch_claims()
    {
        var client = _factory.CreateClient();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "EXTERNAL_FLEET_ADMIN");
        client.DefaultRequestHeaders.Add("X-Dev-Customer-Id", customerId.ToString());
        client.DefaultRequestHeaders.Add(
            "X-Dev-Branch-Ids",
            $"{Guid.Empty},{Guid.NewGuid()}");

        var response = await client.GetAsync("/api/v1/dev/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var whoami = await response.Content.ReadFromJsonAsync<WhoamiResponse>();
        whoami!.UserType.Should().Be("EXTERNAL_FLEET_ADMIN");
        whoami.CustomerId.Should().Be(customerId.ToString());
        whoami.BranchIds.Should().HaveCount(2);
    }

    // T2.2 — verify the injected ITenantContext (resolved via DI from claims) returns the
    // same data as the raw User.Claims. This proves the full chain: header → stub handler →
    // claims principal → ClaimsTenantContext → injected port.
    [Fact]
    public async Task Whoami_Tenancy_section_matches_claims_via_DI()
    {
        var client = _factory.CreateClient();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "EXTERNAL_FLEET_ADMIN");
        client.DefaultRequestHeaders.Add("X-Dev-Customer-Id", customerId.ToString());

        var response = await client.GetAsync("/api/v1/dev/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var whoami = await response.Content.ReadFromJsonAsync<WhoamiWithTenancyResponse>();
        whoami!.Tenancy.Should().NotBeNull();
        whoami.Tenancy!.TenantId.Should().Be(tenantId.ToString());
        whoami.Tenancy.CustomerId.Should().Be(customerId.ToString());
        whoami.Tenancy.UserType.Should().Be("EXTERNAL_FLEET_ADMIN");
        whoami.Tenancy.IsInternalStaff.Should().BeFalse();
        whoami.Tenancy.IsSystem.Should().BeFalse();
    }

    private sealed record WhoamiResponse(
        bool IsAuthenticated,
        string? TenantId,
        string? CustomerId,
        string? UserId,
        string? UserType,
        IReadOnlyList<string> BranchIds,
        IReadOnlyList<string> Roles);

    private sealed record WhoamiWithTenancyResponse(TenancyDto Tenancy);

    private sealed record TenancyDto(
        string TenantId,
        string? CustomerId,
        string? UserId,
        string UserType,
        IReadOnlyList<string> BranchIds,
        bool IsInternalStaff,
        bool IsSystem);
}

/// <summary>
/// WebApplicationFactory pinning environment to Development so DevJwtStubHandler activates.
/// Production-guard tests use a separate factory.
/// </summary>
public sealed class DevWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AutoLeaseNet"] = "Server=ignored;Database=ignored;",
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
