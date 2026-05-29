using System.Net;
using System.Net.Http.Json;
using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Bff.Tests.Support;
using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Endpoints;

/// <summary>
/// Endpoint-level contract for <c>GET /api/v1/me/leases</c>. These tests use EF
/// InMemory which does NOT enforce RLS — the actual CustomerId scoping proof
/// lives in <c>RlsIsolationTests</c> (Integration category, real SQL). What we
/// pin down here:
///
/// <list type="number">
///   <item>Anonymous → 401.</item>
///   <item>Authenticated principal missing CustomerId → 400 with
///         <c>me.requires_customer_context</c>.</item>
///   <item>Authenticated external customer principal → 200 with the lease list shape.</item>
/// </list>
///
/// (3) returns ALL tenant leases on InMemory because there's no RLS in the
/// in-memory provider — that's correct for this layer; RLS is asserted
/// separately.
/// </summary>
public sealed class MeEndpointTests
{
    [Fact]
    public async Task GET_me_leases_anonymous_returns_401()
    {
        await using var factory = new MeFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateClient(); // no auth headers

        var response = await client.GetAsync("/api/v1/me/leases");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_leases_internal_staff_without_customer_returns_400()
    {
        await using var factory = new MeFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateInternalStaffClient();

        var response = await client.GetAsync("/api/v1/me/leases");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("me.requires_customer_context");
    }

    [Fact]
    public async Task GET_me_leases_external_customer_returns_200_with_lease_list()
    {
        await using var factory = new MeFactory();
        await factory.EnsureSeededAsync();
        var demoCustomerId = await factory.PickAnyCustomerIdAsync();
        using var client = factory.CreateExternalClient(demoCustomerId);

        var response = await client.GetAsync("/api/v1/me/leases");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var leases = await response.Content.ReadFromJsonAsync<List<MyLeaseDto>>();
        leases.Should().NotBeNull(because: "endpoint must return a JSON array");
        // Shape contract: each lease has the expected fields populated.
        if (leases!.Count > 0)
        {
            var first = leases[0];
            first.Id.Should().NotBeEmpty();
            first.Status.Should().BeGreaterThan(0,
                because: "LeaseStatus enum starts at 1 (PendingIssuance)");
            first.ContractStartUtc.Should().BeBefore(first.ContractEndUtc,
                because: "contract dates must be monotone");
        }
    }
}

internal sealed class MeFactory : WebApplicationFactory<Program>
{
    public static readonly Guid SeededTenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded;

    public HttpClient CreateInternalStaffClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "INTERNAL_STAFF");
        return client;
    }

    public HttpClient CreateExternalClient(Guid customerId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "EXTERNAL_INDIVIDUAL");
        client.DefaultRequestHeaders.Add("X-Dev-Customer-Id", customerId.ToString());
        return client;
    }

    public async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        await BffTestHostDefaults.EnsureDemoSeededAsync(this, db => db.Customers.AnyAsync(), "Customers");
        _seeded = true;
    }

    public async Task<Guid> PickAnyCustomerIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.TenantId == SeededTenantId)
            .FirstAsync();
        return customer.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.DemoSeedDefaults(SeededTenantId, "20260529")));
        builder.ConfigureTestServices(services =>
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName));
    }
}
