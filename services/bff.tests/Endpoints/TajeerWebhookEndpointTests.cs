using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Domain.Leases;
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
/// Day 6 part 2 — Tajeer webhook receiver. Covers:
/// <list type="bullet">
///   <item>T6.3 valid signature acceptance, T6.3 invalid signature rejection (LogOnly=false).</item>
///   <item>T6.4 LogOnly=true persists invalid-signature rows but skips dispatch.</item>
///   <item>T6.5 dedup (second arrival returns 200 no-op; single WebhookLog row).</item>
///   <item>T6.6 contract.create with valid sig flips matching Lease to Active.</item>
///   <item>Malformed body → 400.</item>
/// </list>
/// </summary>
public sealed class TajeerWebhookEndpointTests
{
    private const string SharedSecret = "test-webhook-secret-day6";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj, JsonOpts), Encoding.UTF8, "application/json");

    [Fact]
    public async Task POST_webhook_invalid_signature_with_LogOnly_off_returns_401()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = JsonBody(new { id = "evt-1", type = "contract.create", referenceId = "999" }),
        };
        req.Headers.Add("secret-key", "WRONG");

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        (await db.WebhookLogs.CountAsync()).Should().Be(0, because: "401 must not persist a row");
    }

    [Fact]
    public async Task POST_webhook_invalid_signature_with_LogOnly_on_returns_200_and_persists_with_SignatureValid_false()
    {
        await using var factory = new WebhookFactory(logOnly: true);
        await factory.SeedTenantAsync();
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = JsonBody(new { id = "evt-logonly-1", type = "contract.create", referenceId = "999" }),
        };
        req.Headers.Add("secret-key", "WRONG");

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var row = await db.WebhookLogs.SingleAsync();
        row.SignatureValid.Should().BeFalse();
        row.ProcessingError.Should().Contain("Signature invalid");
    }

    [Fact]
    public async Task POST_webhook_malformed_body_returns_400()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = new StringContent("this is not json", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("webhook.body.malformed");
    }

    [Fact]
    public async Task POST_webhook_missing_required_fields_returns_400()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = JsonBody(new { timestamp = "2026-05-24T10:00" }), // no id, no type
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("webhook.body.missing_required");
    }

    [Fact]
    public async Task POST_webhook_contract_create_event_marks_lease_Issued()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        var lease = await factory.SeedPendingLeaseAsync(tajeerContractNumber: 4242);
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = JsonBody(new
            {
                id = "evt-issuance-1",
                timestamp = "2026-05-24T11:00",
                category = "contract",
                type = "contract.create",
                referenceId = "4242",
                message = "Contract 4242 created.",
            }),
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var reread = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == lease.Id);
        reread.Status.Should().Be(LeaseStatus.Active);
        reread.IssuedAtUtc.Should().NotBeNull();

        var hook = await db.WebhookLogs.AsNoTracking().SingleAsync();
        hook.SignatureValid.Should().BeTrue();
        hook.EventType.Should().Be("contract.create");
        hook.ProcessedAtUtc.Should().NotBeNull(because: "issuance dispatch completed successfully");
    }

    [Fact]
    public async Task POST_webhook_duplicate_eventId_returns_200_and_does_not_double_process()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        var lease = await factory.SeedPendingLeaseAsync(tajeerContractNumber: 4243);
        using var client = factory.CreateClient();

        async Task<HttpResponseMessage> Send()
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
            {
                Content = JsonBody(new { id = "evt-dup-1", type = "contract.create", referenceId = "4243", category = "contract" }),
            };
            r.Headers.Add("secret-key", SharedSecret);
            return await client.SendAsync(r);
        }

        var first = await Send();
        var second = await Send();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsStringAsync()).Should().Contain("duplicate-ignored");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        (await db.WebhookLogs.CountAsync()).Should().Be(1, because: "unique index on (Source, ExternalEventId) blocks the second insert");

        var reread = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == lease.Id);
        reread.Status.Should().Be(LeaseStatus.Active);
    }

    [Fact]
    public async Task POST_webhook_event_for_unknown_contract_persists_log_marked_failed()
    {
        await using var factory = new WebhookFactory(logOnly: false);
        await factory.SeedTenantAsync();
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = JsonBody(new { id = "evt-unknown-1", type = "contract.create", referenceId = "9999999" }),
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var row = await db.WebhookLogs.AsNoTracking().SingleAsync();
        row.ProcessingError.Should().Contain("No local Lease");
    }
}

/// <summary>
/// WebApplicationFactory wired for webhook tests. EF Core InMemory, fixed webhook secret,
/// Seed:Mode=Empty so the seeder doesn't conflict with our hand-crafted Lease row.
/// </summary>
internal sealed class WebhookFactory : WebApplicationFactory<Program>
{
    private readonly bool _logOnly;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _hostReady;

    public WebhookFactory(bool logOnly) { _logOnly = logOnly; }

    public async Task SeedTenantAsync()
    {
        EnsureHost();
        // Nothing to pre-seed when Seed:Mode=Empty; placeholder for future fixture data.
        await Task.CompletedTask;
    }

    public async Task<Lease> SeedPendingLeaseAsync(long tajeerContractNumber)
    {
        EnsureHost();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var nowUtc = DateTimeOffset.UtcNow;
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001"),
            TajeerContractNumber = tajeerContractNumber,
            IssuanceUrl = $"https://tajeerstg.logisti.sa/#/public-contract/{tajeerContractNumber}/tok",
            ContractTypeCode = 1,
            ContractStartUtc = nowUtc,
            ContractEndUtc = nowUtc.AddDays(2),
            RentAmount = 200m,
            PaymentMethodCode = 1,
            NowUtc = nowUtc,
        });
        db.Leases.Add(lease);
        await db.SaveChangesAsync();
        return lease;
    }

    private void EnsureHost()
    {
        if (_hostReady) return;
        using var _ = CreateClient();
        _hostReady = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AutoLeaseNet"] = "Server=ignored;Database=ignored;",
                ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
                ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
                ["Tajeer:AppId"] = "test-app",
                ["Tajeer:AppKey"] = "test-key",
                ["Tajeer:AuthorizationToken"] = "Basic test",
                ["Tajeer:BranchId"] = "1",
                ["Tajeer:TimeoutSeconds"] = "10",
                ["Tajeer:WebhookSharedSecret"] = "test-webhook-secret-day6",
                ["Tajeer:Webhook:LogOnly"] = _logOnly ? "true" : "false",
                ["Tajeer:Mode"] = "InMemory",
                ["Outbox:Enabled"] = "false",
                // Empty seed avoids dropping 200+ rows into webhook tests.
                ["Seed:Mode"] = "Empty",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
            services.AddAutoLeaseNetDbContext(opt => opt.UseInMemoryDatabase(databaseName: _dbName));

            // Provide a no-op ITajeerContractClient so DI resolves cleanly.
            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            var stub = new InMemoryTajeerContractClient();
            services.AddSingleton(stub);
            services.AddSingleton<ITajeerContractClient>(_ => stub);
        });
    }
}
