using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// End-to-end Incident endpoint tests. Mirrors the Inspection endpoint factory —
/// no Tajeer dependency, so the simpler in-memory swap (DbContext only) is enough.
/// </summary>
public sealed class IncidentEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_report_returns_201_with_id_and_Open()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, leaseId) = await factory.PickSeededVehicleAndLeaseAsync();

        using var req = NewIdempotentPost("/api/v1/incidents", new ReportIncidentRequest
        {
            VehicleId = vehicleId,
            LeaseId = leaseId,
            ReportedByPersonId = Guid.NewGuid(),
            Type = IncidentType.TrafficAccident,
            Severity = IncidentSeverity.Minor,
            IncidentTimeUtc = DateTimeOffset.UtcNow.AddHours(-1),
            Description = "Parking incident",
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetInt32().Should().Be((int)IncidentStatus.Open);
        body.TryGetProperty("id", out var idProp).Should().BeTrue();
        idProp.GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_investigate_then_resolve_advances_state_machine()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var incidentId = await factory.PickSeededIncidentIdAsync(status: IncidentStatus.Closed);
        // Closed incidents from seed are terminal — pick one we can drive: report a fresh one.
        var (vehicleId, leaseId) = await factory.PickSeededVehicleAndLeaseAsync();
        var freshId = await ReportFreshAsync(client, vehicleId, leaseId);

        using var inv = NewIdempotentPost($"/api/v1/incidents/{freshId}/investigate", new { });
        (await client.SendAsync(inv)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var res = NewIdempotentPost($"/api/v1/incidents/{freshId}/resolve", new ResolveIncidentRequest("Polished + invoiced"));
        var resResp = await client.SendAsync(res);
        resResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetInt32().Should().Be((int)IncidentStatus.Resolved);
    }

    [Fact]
    public async Task POST_close_is_idempotent_when_already_Closed()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, leaseId) = await factory.PickSeededVehicleAndLeaseAsync();
        var freshId = await ReportFreshAsync(client, vehicleId, leaseId);

        using var close1 = NewIdempotentPost($"/api/v1/incidents/{freshId}/close", new { });
        using var close2 = NewIdempotentPost($"/api/v1/incidents/{freshId}/close", new { });
        var r1 = await client.SendAsync(close1);
        var r2 = await client.SendAsync(close2);

        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, because: "MarkClosed on already-Closed is a domain-level no-op");
    }

    [Fact]
    public async Task PATCH_claim_after_Close_returns_409()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        // Seed produces only Closed incidents — perfect for the immutable-claim assertion.
        var closedId = await factory.PickSeededIncidentIdAsync(status: IncidentStatus.Closed);

        using var req = NewIdempotentPatch($"/api/v1/incidents/{closedId}/claim", new UpdateIncidentClaimRequest("RP-late", null));
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GET_by_id_returns_detail_for_seeded_incident()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var seededId = await factory.PickSeededIncidentIdAsync(status: IncidentStatus.Closed);

        var response = await client.GetAsync($"/api/v1/incidents/{seededId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("id").GetGuid().Should().Be(seededId);
        body.GetProperty("status").GetInt32().Should().Be((int)IncidentStatus.Closed);
    }

    [Fact]
    public async Task GET_lookups_returns_paged_results_with_status_filter()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/incidents?page=1&pageSize=10&status=4"); // 4 = Closed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0, because: "seed produces at least one Closed incident");
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            item.GetProperty("status").GetInt32().Should().Be((int)IncidentStatus.Closed);
        }
    }

    [Fact]
    public async Task POST_report_without_Idempotency_Key_returns_400()
    {
        await using var factory = new IncidentFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (vehicleId, leaseId) = await factory.PickSeededVehicleAndLeaseAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents")
        {
            Content = JsonContent.Create(new ReportIncidentRequest
            {
                VehicleId = vehicleId, LeaseId = leaseId, ReportedByPersonId = Guid.NewGuid(),
                Type = IncidentType.Other, Severity = IncidentSeverity.Minor,
                IncidentTimeUtc = DateTimeOffset.UtcNow.AddHours(-1), Description = "x",
            }, options: JsonOpts),
        };
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> ReportFreshAsync(HttpClient client, Guid vehicleId, Guid leaseId)
    {
        using var req = NewIdempotentPost("/api/v1/incidents", new ReportIncidentRequest
        {
            VehicleId = vehicleId,
            LeaseId = leaseId,
            ReportedByPersonId = Guid.NewGuid(),
            Type = IncidentType.Breakdown,
            Severity = IncidentSeverity.Minor,
            IncidentTimeUtc = DateTimeOffset.UtcNow.AddHours(-2),
            Description = "Fresh incident for transition test",
        });
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
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

    private static HttpRequestMessage NewIdempotentPatch(string url, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return req;
    }
}

internal sealed class IncidentFactory : WebApplicationFactory<Program>
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
            if (await db.Incidents.AnyAsync()) { _seeded = true; return; }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Seeder did not populate Incidents within 120s.");
    }

    public async Task<(Guid VehicleId, Guid LeaseId)> PickSeededVehicleAndLeaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == SeededTenantId && l.VehicleId != null)
            .FirstAsync();
        return (lease.VehicleId!.Value, lease.Id);
    }

    public async Task<Guid> PickSeededIncidentIdAsync(IncidentStatus status)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var i = await db.Incidents.AsNoTracking()
            .FirstAsync(x => x.TenantId == SeededTenantId && x.Status == status);
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
                ["Seed:Mode"] = "Demo",
                ["Seed:TenantId"] = SeededTenantId.ToString(),
                ["Seed:RandomSeed"] = "20260528",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
            services.AddAutoLeaseNetDbContext(opt => opt.UseInMemoryDatabase(databaseName: _dbName));
        });
    }
}
