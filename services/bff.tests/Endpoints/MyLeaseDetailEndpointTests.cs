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
/// Endpoint-level contract for <c>GET /api/v1/me/leases/{id}</c>. Same trust
/// model as <see cref="MeEndpointTests"/> / <see cref="MyVehiclesEndpointTests"/>:
///
/// <list type="number">
///   <item>Anonymous → 401.</item>
///   <item>Authenticated external customer + unknown lease id → 404.</item>
///   <item>Authenticated external customer + an own lease id → 200 with detail shape.</item>
/// </list>
///
/// EF InMemory has no RLS, so the third test picks any seeded lease in the
/// tenant — the actual customer-scoping proof lives in <c>RlsIsolationTests</c>.
/// The point here is the wire-shape and 404-for-unknown contract.
/// </summary>
public sealed class MyLeaseDetailEndpointTests
{
    [Fact]
    public async Task GET_me_lease_detail_anonymous_returns_401()
    {
        await using var factory = new MyLeaseDetailFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/me/leases/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_lease_detail_external_customer_unknown_id_returns_404()
    {
        await using var factory = new MyLeaseDetailFactory();
        await factory.EnsureSeededAsync();
        var demoCustomerId = await factory.PickAnyCustomerIdAsync();
        using var client = factory.CreateExternalClient(demoCustomerId);

        var response = await client.GetAsync($"/api/v1/me/leases/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_me_lease_detail_external_customer_known_id_returns_200_with_shape()
    {
        await using var factory = new MyLeaseDetailFactory();
        await factory.EnsureSeededAsync();
        var demoCustomerId = await factory.PickAnyCustomerIdAsync();
        var leaseId = await factory.PickAnyLeaseIdAsync();
        using var client = factory.CreateExternalClient(demoCustomerId);

        var response = await client.GetAsync($"/api/v1/me/leases/{leaseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<MyLeaseDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(leaseId);
        detail.ContractStartUtc.Should().BeBefore(detail.ContractEndUtc);
        detail.Status.Should().BeInRange(0, 8, because: "LeaseStatus enum values are 0..8");
        // Vehicle is optional (Day-5 leases can be null until Day-D reshape).
        if (detail.Vehicle is not null)
        {
            detail.Vehicle.PlateNumber.Should().NotBeNullOrWhiteSpace();
            detail.Vehicle.PlateLetters.Should().NotBeNullOrWhiteSpace();
        }
    }
}

internal sealed class MyLeaseDetailFactory : WebApplicationFactory<Program>
{
    public static readonly Guid SeededTenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded;

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
        await BffTestHostDefaults.EnsureDemoSeededAsync(this, db => db.Leases.AnyAsync(), "Leases");
        _seeded = true;
    }

    public async Task<Guid> PickAnyCustomerIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        return await db.Customers.AsNoTracking()
            .Where(c => c.TenantId == SeededTenantId)
            .Select(c => c.Id)
            .FirstAsync();
    }

    public async Task<Guid> PickAnyLeaseIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        return await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == SeededTenantId)
            .Select(l => l.Id)
            .FirstAsync();
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
