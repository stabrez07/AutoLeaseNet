using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Bff.Endpoints;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Endpoints;

/// <summary>
/// Day-20 — BFF coverage for POST /leases/{id}/extend + POST /leases/{id}/suspend.
/// Uses the seeded Demo data the same way the CheckIn endpoint test does, and
/// follows the explicit-InMemory-Tajeer swap pattern (Spec/CI lesson from PR #15).
/// </summary>
public sealed class LeaseExtendSuspendEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_extend_on_Active_lease_returns_200_and_advances_contract_end()
    {
        await using var factory = new ExtendSuspendFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (leaseId, currentEnd) = await factory.PickActiveLeaseAsync();
        var newEnd = currentEnd.AddDays(7);

        using var req = NewIdempotentPost($"/api/v1/leases/{leaseId}/extend", new ExtendLeaseRequest
        {
            NewContractEndUtc = newEnd,
            AdditionalCharges = 200m,
            PaymentMethodCode = 1,
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetString().Should().Be(nameof(LeaseStatus.Extended));
        body.GetProperty("extensionCount").GetInt32().Should().Be(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == leaseId);
        lease.Status.Should().Be(LeaseStatus.Extended);
        lease.ContractEndUtc.Should().Be(newEnd);
    }

    [Fact]
    public async Task POST_suspend_on_Active_lease_returns_200_and_marks_lease_Suspended()
    {
        await using var factory = new ExtendSuspendFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (leaseId, _) = await factory.PickActiveLeaseAsync();

        using var req = NewIdempotentPost($"/api/v1/leases/{leaseId}/suspend", new SuspendLeaseRequest
        {
            SuspensionReasonCode = 7,
            Notes = "Body shop",
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetString().Should().Be(nameof(LeaseStatus.Suspended));
        body.GetProperty("suspensionReasonCode").GetInt32().Should().Be(7);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == leaseId);
        lease.Status.Should().Be(LeaseStatus.Suspended);
    }

    [Fact]
    public async Task POST_extend_without_Idempotency_Key_returns_400()
    {
        await using var factory = new ExtendSuspendFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var (leaseId, currentEnd) = await factory.PickActiveLeaseAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/leases/{leaseId}/extend")
        {
            Content = JsonContent.Create(new ExtendLeaseRequest { NewContractEndUtc = currentEnd.AddDays(1) }, options: JsonOpts),
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

internal sealed class ExtendSuspendFactory : WebApplicationFactory<Program>
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

    public async Task<(Guid LeaseId, DateTimeOffset CurrentEnd)> PickActiveLeaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == SeededTenantId && l.Status == LeaseStatus.Active)
            .FirstAsync();
        return (lease.Id, lease.ContractEndUtc);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.DemoSeedDefaults(SeededTenantId, "20260528")));
        builder.ConfigureTestServices(services =>
        {
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName);

            // Explicit InMemory Tajeer swap — matches CheckInFactory / SaveContractEndpointFactory.
            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            var stub = new InMemoryTajeerContractClient();
            services.AddSingleton(stub);
            services.AddSingleton<ITajeerContractClient>(_ => stub);
        });
    }
}
