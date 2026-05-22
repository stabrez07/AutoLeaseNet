using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Bff.Authentication;
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
/// T5.5 + T5.6 — <c>POST /api/v1/dev/save-contract</c>: Idempotency-Key required, 202
/// on success, replay returns the cached result without re-calling Tajeer.
/// </summary>
public sealed class SaveContractEndpointTests : IClassFixture<SaveContractEndpointFactory>
{
    private readonly SaveContractEndpointFactory _factory;

    public SaveContractEndpointTests(SaveContractEndpointFactory factory) => _factory = factory;

    private static SaveContractDto BuildBody() => new(
        CustomerId: null,
        Request: new SaveContractRequest
        {
            Renter = new RenterDto { PersonAddress = "Riyadh", Mobile = "0501234567", IdTypeCode = 1, IdNumber = 1234567890 },
            PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = 200m, PaidAmount = 50m },
            VehicleDetails = new VehicleDetailsDto { VehicleId = 4242 },
            WorkingBranchId = 1,
            RentPolicyId = 1,
            ContractStartDate = "2026-05-23T10:00",
            ContractEndDate = "2026-05-25T10:00",
            ReceiveBranchId = 1,
            ReturnBranchId = 1,
            ContractTypeCode = 1,
            OperatorId = 99,
        });

    [Fact]
    public async Task POST_save_contract_without_Idempotency_Key_returns_400()
    {
        _factory.ResetTajeerCalls();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/dev/save-contract", BuildBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Idempotency-Key");
        _factory.Tajeer.SaveCalls.Should().BeEmpty(because: "Tajeer must not be called when the request is rejected at the gate");
    }

    [Fact]
    public async Task POST_save_contract_with_valid_body_returns_202_and_writes_a_Lease()
    {
        _factory.ResetTajeerCalls();
        _factory.ResetDb();
        using var client = _factory.CreateAuthenticatedClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/save-contract")
        {
            Content = JsonContent.Create(BuildBody()),
        };
        request.Headers.Add("Idempotency-Key", "idem-bff-001");

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
        var lease = await db.Leases.SingleAsync();
        lease.TajeerContractNumber.Should().Be(parsed.GetProperty("tajeerContractNumber").GetInt64());
    }

    [Fact]
    public async Task POST_save_contract_replays_cached_response_for_same_Idempotency_Key()
    {
        _factory.ResetTajeerCalls();
        _factory.ResetDb();
        using var client = _factory.CreateAuthenticatedClient();

        async Task<string> Send()
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/save-contract")
            {
                Content = JsonContent.Create(BuildBody()),
            };
            req.Headers.Add("Idempotency-Key", "idem-replay-bff");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
            return await resp.Content.ReadAsStringAsync();
        }

        var first = await Send();
        var second = await Send();

        second.Should().Be(first, because: "replay must return byte-identical response");
        _factory.Tajeer.SaveCalls.Should().HaveCount(1, because: "second call is served from the idempotency cache");
    }

    public sealed record SaveContractDto(Guid? CustomerId, SaveContractRequest Request);
}

/// <summary>
/// WebApplicationFactory for save-contract tests:
/// - Forces Development env so /dev endpoints are mapped.
/// - Replaces DbContext with EF Core InMemory provider (no real SQL).
/// - Swaps in a shared <see cref="InMemoryTajeerContractClient"/> instance so the test
///   can count <c>SaveCalls</c> across requests.
/// </summary>
public sealed class SaveContractEndpointFactory : WebApplicationFactory<Program>
{
    public InMemoryTajeerContractClient Tajeer { get; } = new();
    private string _dbName = Guid.NewGuid().ToString();

    public void ResetTajeerCalls()
    {
        // SaveCalls is a List<> on the captured client — replacing the client itself for
        // a clean slate keeps the test focused on call-count semantics.
        var calls = (List<SaveContractRequest>)Tajeer.SaveCalls;
        calls.Clear();
    }

    public void ResetDb()
    {
        _dbName = Guid.NewGuid().ToString();
        // Reconfigure the in-memory provider through the next CreateClient call
        // by relying on the override below reading the latest _dbName.
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "InternalStaff");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Pin a connection string so AddAutoLeaseNetInfrastructure doesn't fault when
        // resolving "AutoLeaseNet". The real DbContext registration is replaced below.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AutoLeaseNet"] = "Server=replaced-by-in-memory;Database=ignored;",
                // Dummy Tajeer credentials satisfy TajeerOptions.ValidateOnStart — the
                // real ITajeerContractClient is replaced below so these are never used.
                ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
                ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
                ["Tajeer:AppId"] = "test-app",
                ["Tajeer:AppKey"] = "test-key",
                ["Tajeer:AuthorizationToken"] = "Basic test",
                ["Tajeer:BranchId"] = "1",
                ["Tajeer:TimeoutSeconds"] = "10",
                ["Tajeer:WebhookSharedSecret"] = "test-secret",
                ["Tajeer:Mode"] = "InMemory",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Swap SQL DbContext for an EF Core InMemory database scoped to the current test name.
            services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
            services.AddDbContext<AutoLeaseNetDbContext>(opt =>
                opt.UseInMemoryDatabase(databaseName: _dbName));

            // Replace the ITajeerContractClient registration with the shared instance the
            // tests can introspect.
            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            services.AddSingleton(Tajeer);
            services.AddSingleton<ITajeerContractClient>(_ => Tajeer);
        });
    }
}
