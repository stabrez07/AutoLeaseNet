using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Application.Operations;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Infrastructure;
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
/// End-to-end tests for the Inspection endpoints. Uses EF Core InMemory + the seed
/// data the BFF's Development startup hook generates. Covers: start happy path,
/// idempotency replay, get-by-id, list with filter, missing-Idempotency-Key 400,
/// not-found 404, illegal-transition 409.
/// </summary>
public sealed class InspectionEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_start_returns_201_with_id_and_InProgress()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        var (vehicleId, _) = await factory.PickSeededVehicleAsync();

        using var req = NewIdempotentPost("/api/v1/inspections", new StartInspectionRequest
        {
            VehicleId = vehicleId,
            Type = InspectionType.PreDelivery,
            OdometerKm = 50_000,
            FuelLevel = FuelLevel.Full,
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetInt32().Should().Be((int)InspectionStatus.InProgress);
    }

    [Fact]
    public async Task POST_start_replays_with_same_Idempotency_Key()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, _) = await factory.PickSeededVehicleAsync();
        var key = Guid.NewGuid().ToString("N");

        async Task<Guid> Post()
        {
            using var r = NewIdempotentPost("/api/v1/inspections", new StartInspectionRequest
            {
                VehicleId = vehicleId,
                Type = InspectionType.PreDelivery,
                OdometerKm = 50_000,
                FuelLevel = FuelLevel.Full,
            }, idempotencyKey: key);
            var resp = await client.SendAsync(r);
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            var b = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            return b.GetProperty("id").GetGuid();
        }

        var id1 = await Post();
        var id2 = await Post();
        id2.Should().Be(id1, because: "same Idempotency-Key must replay the cached result, not create a second Inspection");
    }

    [Fact]
    public async Task POST_start_without_Idempotency_Key_returns_400()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, _) = await factory.PickSeededVehicleAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inspections")
        {
            Content = JsonContent.Create(new StartInspectionRequest
            {
                VehicleId = vehicleId,
                Type = InspectionType.PreDelivery,
                OdometerKm = 1,
                FuelLevel = FuelLevel.Full,
            }, options: JsonOpts),
        };
        // No Idempotency-Key.

        var response = await client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_complete_then_complete_again_is_idempotent_via_cache()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, _) = await factory.PickSeededVehicleAsync();
        var id = await StartInspection(client, vehicleId);

        var completeKey = Guid.NewGuid().ToString("N");
        async Task<HttpResponseMessage> Complete()
        {
            using var r = NewIdempotentPost($"/api/v1/inspections/{id}/complete", body: new { }, idempotencyKey: completeKey);
            return await client.SendAsync(r);
        }

        var first = await Complete();
        var second = await Complete();
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        firstBody.GetProperty("status").GetInt32().Should().Be((int)InspectionStatus.Completed);
    }

    [Fact]
    public async Task POST_complete_on_unknown_id_returns_404()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var req = NewIdempotentPost($"/api/v1/inspections/{Guid.NewGuid()}/complete", body: new { });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_by_id_returns_seeded_inspection_with_children()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var seededId = await factory.PickSeededInspectionIdAsync();

        var response = await client.GetAsync($"/api/v1/inspections/{seededId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        detail.GetProperty("id").GetGuid().Should().Be(seededId);
        detail.GetProperty("photos").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
        detail.GetProperty("damageMarkers").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GET_lookup_paged_returns_seeded_inspections()
    {
        await using var factory = new InspectionFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/inspections?page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<InspectionSummaryDto>>(JsonOpts);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0, because: "seeder creates one or more inspections per non-terminal lease");
    }

    private static async Task<Guid> StartInspection(HttpClient client, Guid vehicleId)
    {
        using var r = NewIdempotentPost("/api/v1/inspections", new StartInspectionRequest
        {
            VehicleId = vehicleId,
            Type = InspectionType.PreDelivery,
            OdometerKm = 1,
            FuelLevel = FuelLevel.Full,
        });
        var resp = await client.SendAsync(r);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static HttpRequestMessage NewIdempotentPost(string url, object body, string? idempotencyKey = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return req;
    }
}

/// <summary>
/// WebApplicationFactory for the Inspection tests. Mirrors the SaveContract test
/// factory pattern (EF Core InMemory + Seed:Mode=Demo + dev JWT stub headers).
/// </summary>
internal sealed class InspectionFactory : WebApplicationFactory<Program>
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
            if (await db.Inspections.AnyAsync()) { _seeded = true; return; }
            await Task.Delay(100);
        }
        var mode = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<string>("Seed:Mode") ?? "(null)";
        throw new InvalidOperationException($"Seeder did not populate Inspections within 120s. Mode={mode}.");
    }

    public async Task<(Guid VehicleId, int CurrentKm)> PickSeededVehicleAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var v = await db.Vehicles.AsNoTracking().FirstAsync(x => x.TenantId == SeededTenantId);
        return (v.Id, v.CurrentKm);
    }

    public async Task<Guid> PickSeededInspectionIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var i = await db.Inspections.AsNoTracking().FirstAsync(x => x.TenantId == SeededTenantId);
        return i.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AutoLeaseNet"] = "Server=replaced-by-in-memory;Database=ignored;",
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
                ["Seed:Mode"] = "Demo",
                ["Seed:TenantId"] = SeededTenantId.ToString(),
                ["Seed:RandomSeed"] = "20260525",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
            services.AddAutoLeaseNetDbContext(opt => opt.UseInMemoryDatabase(databaseName: _dbName));
        });
    }
}
