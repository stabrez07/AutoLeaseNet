using System.Net;
using System.Net.Http.Json;
using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Bff.Tests.Support;
using AutoLeaseNet.Domain.Leases;
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
/// Endpoint-level contract for <c>GET /api/v1/me/vehicles/{id}</c>. Same trust
/// model as <see cref="MyLeaseDetailEndpointTests"/>:
///
/// <list type="number">
///   <item>Anonymous → 401.</item>
///   <item>Authenticated external customer + unknown vehicle id → 404.</item>
///   <item>Authenticated external customer + a vehicle they currently have → 200.</item>
/// </list>
///
/// "Currently have" = a lease in Active/Extended/Suspended on this vehicle.
/// </summary>
public sealed class MyVehicleDetailEndpointTests
{
    [Fact]
    public async Task GET_me_vehicle_detail_anonymous_returns_401()
    {
        await using var factory = new MyVehicleDetailFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/me/vehicles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_vehicle_detail_external_customer_unknown_id_returns_404()
    {
        await using var factory = new MyVehicleDetailFactory();
        await factory.EnsureSeededAsync();
        var demoCustomerId = await factory.PickAnyCustomerIdAsync();
        using var client = factory.CreateExternalClient(demoCustomerId);

        var response = await client.GetAsync($"/api/v1/me/vehicles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_me_vehicle_detail_external_customer_current_lease_returns_200_with_shape()
    {
        await using var factory = new MyVehicleDetailFactory();
        await factory.EnsureSeededAsync();
        var (customerId, vehicleId) = await factory.PickCustomerWithActiveLeaseVehicleAsync();
        using var client = factory.CreateExternalClient(customerId);

        var response = await client.GetAsync($"/api/v1/me/vehicles/{vehicleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<MyVehicleDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(vehicleId);
        detail.PlateNumber.Should().NotBeNullOrWhiteSpace();
        detail.PlateLetters.Should().NotBeNullOrWhiteSpace();
        detail.Make.Should().NotBeNullOrWhiteSpace();
        detail.Model.Should().NotBeNullOrWhiteSpace();
        detail.ModelYear.Should().BeGreaterThanOrEqualTo(1990);
        detail.Seats.Should().BeGreaterThan(0);
    }
}

internal sealed class MyVehicleDetailFactory : WebApplicationFactory<Program>
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
        await BffTestHostDefaults.EnsureDemoSeededAsync(
            this, db => db.Leases.AnyAsync(l => l.Status == LeaseStatus.Active), "Active Lease");
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

    /// <summary>
    /// Picks a (CustomerId, VehicleId) pair where the customer has an Active lease on
    /// the vehicle — so the detail handler's lease-side EXISTS check finds something.
    /// </summary>
    public async Task<(Guid CustomerId, Guid VehicleId)> PickCustomerWithActiveLeaseVehicleAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == SeededTenantId
                && l.Status == LeaseStatus.Active
                && l.CustomerId != null
                && l.VehicleId != null)
            .FirstAsync();
        return (lease.CustomerId!.Value, lease.VehicleId!.Value);
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
