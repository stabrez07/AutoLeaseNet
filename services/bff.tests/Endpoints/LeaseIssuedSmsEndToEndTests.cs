using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoLeaseNet.Adapters.Sms.InMemory;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Application.Leases.Notifications;
using AutoLeaseNet.Application.Ports.Messaging;
using AutoLeaseNet.Domain.Customers;
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
/// T7.5 — end-to-end: a Tajeer webhook for a contract owned by a B2C customer with a
/// known mobile flips the Lease to Active and dispatches a localised SMS via the
/// InMemorySmsSender (which the test inspects).
/// </summary>
public sealed class LeaseIssuedSmsEndToEndTests
{
    private const string SharedSecret = "test-webhook-secret-day7";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Webhook_contract_create_dispatches_Arabic_SMS_to_renter_mobile()
    {
        await using var factory = new SmsE2EFactory();
        var (lease, customer) = await factory.SeedLeaseAndRenterAsync(
            tajeerContractNumber: 7777,
            mobile: "+966501239876",
            preferredLanguage: PreferredLanguage.Ar);
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = "evt-sms-ar-1",
                timestamp = "2026-05-24T12:00",
                category = "contract",
                type = "contract.create",
                referenceId = "7777",
                message = "Contract 7777 issued.",
            }, JsonOpts), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var reread = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == lease.Id);
        reread.Status.Should().Be(LeaseStatus.Active);

        // Post-Outbox: SMS dispatch is asynchronous via the drain. Wait up to 5s.
        await WaitForAsync(() => factory.Sms.Sent.Count == 1, TimeSpan.FromSeconds(5));

        factory.Sms.Sent.Should().HaveCount(1, because: "one issuance event = one SMS dispatch");
        var sms = factory.Sms.Sent.Single();
        sms.ToE164.Should().Be("+966501239876");
        sms.Body.Should().Contain("7777");
        sms.Body.Should().Contain("عقد التأجير", because: "Ar template should include this phrase");
        sms.Tags!["template"].Should().Be(LeaseIssuedSmsTemplates.TemplateKeyAr);
    }

    [Fact]
    public async Task Webhook_contract_create_with_no_renter_customer_still_returns_200_and_updates_lease()
    {
        await using var factory = new SmsE2EFactory();
        var lease = await factory.SeedLeaseWithoutCustomerAsync(tajeerContractNumber: 8888);
        using var client = factory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/tajeer")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = "evt-no-customer",
                type = "contract.create",
                referenceId = "8888",
                category = "contract",
            }, JsonOpts), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("secret-key", SharedSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var reread = await db.Leases.AsNoTracking().SingleAsync(l => l.Id == lease.Id);
        reread.Status.Should().Be(LeaseStatus.Active);

        // Even when the handler decides not to send (no customer), let the drain finish
        // its cycle so the OutboxEvent row is observably ProcessedAtUtc-set.
        await WaitForAsync(() => db.OutboxEvents.AsNoTracking()
            .Any(o => o.ProcessedAtUtc != null), TimeSpan.FromSeconds(5));

        factory.Sms.Sent.Should().BeEmpty(because: "no customer reference on the lease means no SMS recipient");
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(100);
        }
    }
}

/// <summary>
/// WebApplicationFactory wired for the Day-7 end-to-end test. EF Core InMemory + shared
/// InMemorySmsSender so the test can introspect Sent. Seed:Mode=Empty so we control
/// the dataset.
/// </summary>
internal sealed class SmsE2EFactory : WebApplicationFactory<Program>
{
    public InMemorySmsSender Sms { get; } = new();
    private readonly string _dbName = Guid.NewGuid().ToString();
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");

    public async Task<(Lease lease, Customer customer)> SeedLeaseAndRenterAsync(
        long tajeerContractNumber, string mobile, PreferredLanguage preferredLanguage)
    {
        EnsureHost();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var nowUtc = DateTimeOffset.UtcNow;

        var customer = Customer.CreateB2C(new B2CCreateInput
        {
            TenantId = TenantId,
            PersonNameEn = "Renter",
            IdTypeCode = 1, PersonIdNumber = $"id-{tajeerContractNumber}",
            Mobile = mobile,
            PreferredLanguage = preferredLanguage,
            NowUtc = nowUtc,
        });
        db.Customers.Add(customer);

        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = TenantId,
            CustomerId = customer.Id,
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
        return (lease, customer);
    }

    public async Task<Lease> SeedLeaseWithoutCustomerAsync(long tajeerContractNumber)
    {
        EnsureHost();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var nowUtc = DateTimeOffset.UtcNow;
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = TenantId,
            CustomerId = null,
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
        // CreateClient builds the host; ignored result.
        using var _ = CreateClient();
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
                ["Tajeer:WebhookSharedSecret"] = "test-webhook-secret-day7",
                ["Tajeer:Webhook:LogOnly"] = "false",
                ["Tajeer:Mode"] = "InMemory",
                // SMS is dispatched by the OutboxDrainService now (post-Outbox workstream).
                // Drain runs at 1s interval here so the test only waits a moment.
                ["Outbox:Enabled"] = "true",
                ["Outbox:DrainIntervalSeconds"] = "1",
                ["Seed:Mode"] = "Empty",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
            services.AddAutoLeaseNetDbContext(opt => opt.UseInMemoryDatabase(databaseName: _dbName));

            services.RemoveAll<ITajeerContractClient>();
            services.RemoveAll<InMemoryTajeerContractClient>();
            var stub = new InMemoryTajeerContractClient();
            services.AddSingleton(stub);
            services.AddSingleton<ITajeerContractClient>(_ => stub);

            // Day-7 swap: replace the default InMemorySmsSender registration with our
            // shared instance so the test can introspect Sent across requests.
            services.RemoveAll<ISmsSender>();
            services.RemoveAll<InMemorySmsSender>();
            services.AddSingleton(Sms);
            services.AddSingleton<ISmsSender>(_ => Sms);
        });
    }
}
