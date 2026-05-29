using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Bff.Tests.Support;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Endpoints;

/// <summary>
/// Day-19 check-in saga BFF tests. Uses the seeded Demo data — picks an ACTIVE lease,
/// posts the check-in payload, asserts both Lease and Vehicle moved.
/// </summary>
public sealed class CheckInLeaseEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_check_in_on_Active_lease_returns_200_and_closes_lease_and_returns_vehicle()
    {
        await using var factory = new CheckInFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (leaseId, vehicleId, startKm) = await factory.PickActiveLeaseAsync();
        var endKm = startKm + 500;

        using var req = NewIdempotentPost($"/api/v1/leases/{leaseId}/check-in", new CheckInLeaseRequest
        {
            OdometerKm = endKm,
            FuelLevel = FuelLevel.Half,
            ClosureMainReasonCode = 1,
            ReturnConditionNotes = "Returned clean",
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetString().Should().Be(nameof(LeaseStatus.Closed));

        // Tajeer Calculate + Close ran — payment block must be present (InMemory adapter
        // gives a deterministic shape: 0 base rent, 15% VAT on caller-declared fees).
        var payment = body.GetProperty("payment");
        payment.ValueKind.Should().Be(JsonValueKind.Object);
        payment.GetProperty("finalPaidAmount").GetDecimal().Should().BeGreaterThanOrEqualTo(0m);
        payment.GetProperty("grandTotal").GetDecimal().Should().BeGreaterThanOrEqualTo(0m);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == leaseId);
        lease.Status.Should().Be(LeaseStatus.Closed);
        lease.EndKm.Should().Be(endKm);

        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicleId);
        vehicle.Status.Should().Be(VehicleStatus.Available);
        vehicle.CurrentKm.Should().Be(endKm);

        var inspection = await db.Inspections.AsNoTracking().SingleAsync(i => i.LeaseId == leaseId && i.Type == InspectionType.CheckIn);
        inspection.Status.Should().Be(InspectionStatus.Completed);
    }

    [Fact]
    public async Task POST_check_in_on_unknown_lease_returns_404()
    {
        await using var factory = new CheckInFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var req = NewIdempotentPost($"/api/v1/leases/{Guid.NewGuid()}/check-in", new CheckInLeaseRequest
        {
            OdometerKm = 1, FuelLevel = FuelLevel.Half, ClosureMainReasonCode = 1,
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_check_in_without_Idempotency_Key_returns_400()
    {
        await using var factory = new CheckInFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (leaseId, _, _) = await factory.PickActiveLeaseAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/leases/{leaseId}/check-in")
        {
            Content = JsonContent.Create(new CheckInLeaseRequest
            {
                OdometerKm = 1, FuelLevel = FuelLevel.Half, ClosureMainReasonCode = 1,
            }, options: JsonOpts),
        };
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static HttpRequestMessage NewIdempotentPost(string url, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return req;
    }
}

internal sealed class CheckInFactory : WebApplicationFactory<Program>
{
    public static readonly Guid SeededTenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded;

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "InternalStaff");
        client.DefaultRequestHeaders.Add("X-Dev-User-Id", Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030").ToString());
        return client;
    }

    public async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        using var _ = CreateClient();
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<AutoLeaseNet.Application.Ports.Seeding.IDataSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (await db.Leases.AnyAsync(l => l.Status == LeaseStatus.Active)) { _seeded = true; return; }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Seeder did not produce an Active lease within 120s.");
    }

    public async Task<(Guid LeaseId, Guid VehicleId, int CurrentKm)> PickActiveLeaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == SeededTenantId && l.Status == LeaseStatus.Active && l.VehicleId != null)
            .FirstAsync();
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == lease.VehicleId!.Value);
        return (lease.Id, vehicle.Id, vehicle.CurrentKm);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.DemoSeedDefaults(SeededTenantId, "20260525")));
        builder.ConfigureTestServices(services =>
        {
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName);

            // Explicit InMemory Tajeer swap — matches the SaveContract / Webhook factory
            // pattern. The Tajeer:Mode config-driven switch in Program.cs uses
            // services.Replace which doesn't always supersede the original Scoped
            // registration under WebApplicationFactory; CI showed Calculate hitting the
            // real HTTP client when only the config switch was relied on.
            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            var stub = new InMemoryTajeerContractClient();
            services.AddSingleton(stub);
            services.AddSingleton<ITajeerContractClient>(_ => stub);
        });
    }
}
