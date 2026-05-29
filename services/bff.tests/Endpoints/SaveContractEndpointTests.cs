using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Application.Ports.Seeding;
using AutoLeaseNet.Bff.Tests.Support;
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
/// T5.5 / T5.6 / Day D — <c>POST /api/v1/dev/save-contract</c> with the domain-shaped
/// command. Factory seeds the DB via BogusDataSeeder; tests use the seeded aggregate ids
/// to drive the endpoint end-to-end.
/// </summary>
public sealed class SaveContractEndpointTests : IClassFixture<SaveContractEndpointFactory>
{
    private readonly SaveContractEndpointFactory _factory;
    public SaveContractEndpointTests(SaveContractEndpointFactory factory) => _factory = factory;

    private async Task<SaveContractDevDto> BuildBodyAsync()
    {
        await _factory.EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var customer = await db.Customers.AsNoTracking().FirstAsync();
        var vehicle = await db.Vehicles.AsNoTracking().FirstAsync(v => v.Status == AutoLeaseNet.Domain.Vehicles.VehicleStatus.Available);
        var driver = await db.Drivers.AsNoTracking().FirstAsync(d => d.Status == AutoLeaseNet.Domain.Drivers.DriverStatus.Active);
        var policy = await db.RentPolicies.AsNoTracking().FirstAsync();
        var branch = await db.Branches.AsNoTracking().FirstAsync();
        var coverage = await db.ExtendedCoverages.AsNoTracking().FirstAsync();

        return new SaveContractDevDto
        {
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            PrimaryDriverId = driver.Id,
            RentPolicyId = policy.Id,
            ExtendedCoverageId = coverage.Id,
            WorkingBranchId = branch.Id,
            ReceiveBranchId = branch.Id,
            ReturnBranchId = branch.Id,
            ContractStartUtc = DateTimeOffset.UtcNow.AddHours(1),
            ContractEndUtc = DateTimeOffset.UtcNow.AddDays(2),
            ContractTypeCode = 1,
            AllowedKmPerDay = 300,
            RentAmount = 200m,
            PaidAmount = 50m,
            PaymentMethodCode = 1,
        };
    }

    [Fact]
    public async Task POST_save_contract_without_Idempotency_Key_returns_400()
    {
        _factory.ResetTajeerCalls();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/dev/save-contract", await BuildBodyAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Idempotency-Key");
        _factory.Tajeer.SaveCalls.Should().BeEmpty(because: "Tajeer must not be called when the request is rejected at the gate");
    }

    [Fact]
    public async Task POST_save_contract_with_valid_body_returns_202_and_writes_a_Lease()
    {
        _factory.ResetTajeerCalls();
        using var client = _factory.CreateAuthenticatedClient();
        var body = await BuildBodyAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/save-contract")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", $"idem-bff-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadAsStringAsync();
        var parsed = JsonDocument.Parse(json).RootElement;
        parsed.GetProperty("leaseId").GetGuid().Should().NotBeEmpty();
        parsed.GetProperty("tajeerContractNumber").GetInt64().Should().BeGreaterThan(0);
        parsed.GetProperty("issuanceUrl").GetString().Should().StartWith("https://inmemory.tajeer.local/#/public-contract/");

        _factory.Tajeer.SaveCalls.Should().HaveCount(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var lease = await db.Leases.AsNoTracking()
            .FirstAsync(l => l.TajeerContractNumber == parsed.GetProperty("tajeerContractNumber").GetInt64());
        lease.CustomerId.Should().Be(body.CustomerId);
        lease.VehicleId.Should().Be(body.VehicleId);
    }

    [Fact]
    public async Task POST_save_contract_returns_422_when_customer_unknown()
    {
        _factory.ResetTajeerCalls();
        using var client = _factory.CreateAuthenticatedClient();
        var body = await BuildBodyAsync();
        body = body with { CustomerId = Guid.NewGuid() };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/save-contract")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", $"idem-422-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body422 = await response.Content.ReadAsStringAsync();
        body422.Should().Contain("lease.customer.not_found");
        _factory.Tajeer.SaveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_save_contract_replays_cached_response_for_same_Idempotency_Key()
    {
        _factory.ResetTajeerCalls();
        using var client = _factory.CreateAuthenticatedClient();
        var body = await BuildBodyAsync();
        var idemKey = $"idem-replay-{Guid.NewGuid():N}";

        async Task<string> Send()
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/save-contract")
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Add("Idempotency-Key", idemKey);
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
            return await resp.Content.ReadAsStringAsync();
        }

        var first = await Send();
        var second = await Send();

        second.Should().Be(first, because: "replay must return byte-identical response");
        _factory.Tajeer.SaveCalls.Should().HaveCount(1, because: "second call is served from the idempotency cache");
    }
}

/// <summary>Domain-shaped body for the endpoint — mirrors SaveContractDevRequest on the BFF.</summary>
public sealed record SaveContractDevDto
{
    public required Guid CustomerId { get; init; }
    public required Guid VehicleId { get; init; }
    public required Guid PrimaryDriverId { get; init; }
    public Guid? ExtraDriverId { get; init; }
    public Guid? AuthorizedDriverId { get; init; }
    public required Guid RentPolicyId { get; init; }
    public Guid? ExtendedCoverageId { get; init; }
    public required Guid WorkingBranchId { get; init; }
    public required Guid ReceiveBranchId { get; init; }
    public required Guid ReturnBranchId { get; init; }
    public required DateTimeOffset ContractStartUtc { get; init; }
    public required DateTimeOffset ContractEndUtc { get; init; }
    public required int ContractTypeCode { get; init; }
    public int AllowedKmPerHour { get; init; }
    public int AllowedKmPerDay { get; init; }
    public bool UnlimitedKm { get; init; }
    public int AllowedLateHours { get; init; }
    public required decimal RentAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public required int PaymentMethodCode { get; init; }
    public int? DiscountType { get; init; }
    public decimal? DiscountValue { get; init; }
}

/// <summary>
/// WebApplicationFactory for save-contract tests:
/// - Forces Development env so /dev endpoints are mapped and the seeder runs.
/// - Replaces DbContext with EF Core InMemory provider (no real SQL).
/// - Sets <c>Seed:Mode=Demo</c> + a fixed tenant so seeded aggregates are queryable.
/// - Swaps in a shared <see cref="InMemoryTajeerContractClient"/> so the test can count
///   <c>SaveCalls</c> across requests.
/// </summary>
public sealed class SaveContractEndpointFactory : WebApplicationFactory<Program>
{
    public InMemoryTajeerContractClient Tajeer { get; } = new();
    public static readonly Guid SeededTenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seedFinished;

    public void ResetTajeerCalls()
    {
        var calls = (List<SaveContractRequest>)Tajeer.SaveCalls;
        calls.Clear();
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "InternalStaff");
        return client;
    }

    /// <summary>The Development startup hook runs the seeder asynchronously during host
    /// build. By the time CreateClient() returns the host is built, but we still confirm
    /// at least one Customer exists before the first test runs.</summary>
    public async Task EnsureSeededAsync()
    {
        if (_seedFinished) return;
        // Force the host to build by creating a client. The Program.cs Development
        // startup hook awaits `seeder.SeedAsync(...)` before app.Run(), so the seed
        // should be complete by the time CreateClient() returns; the polling below
        // is defensive against any future startup-timing race.
        // Timeout 120s — Demo seed generation (Bogus + full aggregate graph) can
        // exceed 30s on constrained or contended test runners.
        using var probe = CreateClient();
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (await db.Customers.AnyAsync()) { _seedFinished = true; return; }
            await Task.Delay(100);
        }

        var mode = scope.ServiceProvider
            .GetRequiredService<IConfiguration>()
            .GetValue<string>("Seed:Mode") ?? "(null)";
        var customerCount = await db.Customers.CountAsync();
        throw new InvalidOperationException(
            $"Seeder did not populate Customers within 120s. SeederType={seeder.GetType().Name}; Seed:Mode={mode}; Customers={customerCount}; DbName={_dbName}.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.DemoSeedDefaults(SeededTenantId, "20260524")));
        builder.ConfigureTestServices(services =>
        {
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName);

            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            services.AddSingleton(Tajeer);
            services.AddSingleton<ITajeerContractClient>(_ => Tajeer);
        });
    }
}
