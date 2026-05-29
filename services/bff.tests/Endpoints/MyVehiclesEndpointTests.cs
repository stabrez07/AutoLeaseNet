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
/// Endpoint-level contract for <c>GET /api/v1/me/vehicles</c>. Same shape proof as
/// <see cref="MeEndpointTests"/> for <c>/me/leases</c>:
///
/// <list type="number">
///   <item>Anonymous → 401.</item>
///   <item>Authenticated principal missing CustomerId → 400 with
///         <c>me.requires_customer_context</c>.</item>
///   <item>Authenticated external customer principal → 200 with a JSON array
///         shape (each item carries plate triple + make/model/year).</item>
/// </list>
///
/// EF InMemory has no RLS, so (3) sees vehicles attached to <i>any</i> active
/// lease in the tenant — that's correct for this layer. The actual RLS scoping
/// proof is the SystemTenancyScope bracket in the handler + a future
/// RlsIsolationTests row against real SQL.
/// </summary>
public sealed class MyVehiclesEndpointTests
{
    [Fact]
    public async Task GET_me_vehicles_anonymous_returns_401()
    {
        await using var factory = new MyVehiclesFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_vehicles_internal_staff_without_customer_returns_400()
    {
        await using var factory = new MyVehiclesFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateInternalStaffClient();

        var response = await client.GetAsync("/api/v1/me/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("me.requires_customer_context");
    }

    [Fact]
    public async Task GET_me_vehicles_external_customer_returns_200_with_vehicle_list()
    {
        await using var factory = new MyVehiclesFactory();
        await factory.EnsureSeededAsync();
        var demoCustomerId = await factory.PickAnyCustomerIdAsync();
        using var client = factory.CreateExternalClient(demoCustomerId);

        var response = await client.GetAsync("/api/v1/me/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vehicles = await response.Content.ReadFromJsonAsync<List<MyVehicleDto>>();
        vehicles.Should().NotBeNull(because: "endpoint must return a JSON array");
        if (vehicles!.Count > 0)
        {
            var first = vehicles[0];
            first.Id.Should().NotBeEmpty();
            first.PlateNumber.Should().NotBeNullOrWhiteSpace(
                because: "plate triple must always have a numeric portion");
            first.PlateLetters.Should().NotBeNullOrWhiteSpace(
                because: "plate triple must always have an Arabic-letter portion");
            first.Make.Should().NotBeNullOrWhiteSpace();
            first.Model.Should().NotBeNullOrWhiteSpace();
            first.ModelYear.Should().BeGreaterThanOrEqualTo(1990,
                because: "Vehicle.Create enforces ModelYear >= 1990");
        }
    }
}

internal sealed class MyVehiclesFactory : WebApplicationFactory<Program>
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
