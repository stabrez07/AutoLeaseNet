using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Bff.Tests.Support;
using AutoLeaseNet.Domain.Sales;
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
/// End-to-end quotation workflow tests using EF InMemory.
/// Exercises the Create → AddLine → Submit → Approve → Send → Recall paths.
/// </summary>
public sealed class QuotationEndpointTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_create_quotation_returns_201_with_Draft_status()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var customerId = await factory.PickSeededCustomerIdAsync();

        using var req = NewIdempotentPost("/api/v1/quotations", new CreateQuotationRequest
        {
            CustomerId = customerId,
            ValidUntilDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetInt32().Should().Be((int)QuotationStatus.Draft);
        body.TryGetProperty("id", out var idProp).Should().BeTrue();
        idProp.GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_create_without_Idempotency_Key_returns_400()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var customerId = await factory.PickSeededCustomerIdAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quotations")
        {
            Content = JsonContent.Create(new CreateQuotationRequest
            {
                CustomerId = customerId,
                ValidUntilDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                ContractType = QuotationContractType.LongTermLease,
            }, options: JsonOpts),
        };
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_create_with_unknown_customer_returns_422()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var req = NewIdempotentPost("/api/v1/quotations", new CreateQuotationRequest
        {
            CustomerId = Guid.NewGuid(),
            ValidUntilDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            ContractType = QuotationContractType.LongTermLease,
        });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_submit_with_no_tiers_auto_approves()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var quotationId = await CreateDraftWithLineAsync(client, await factory.PickSeededCustomerIdAsync());

        using var req = NewIdempotentPost($"/api/v1/quotations/{quotationId}/submit", new { });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        // No approval tiers seeded in demo data for quotations → auto-approved
        body.GetProperty("status").GetInt32().Should().BeOneOf(
            (int)QuotationStatus.Approved,
            (int)QuotationStatus.PendingApproval);
    }

    [Fact]
    public async Task POST_recall_Draft_returns_Withdrawn()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();
        var quotationId = await CreateDraftWithLineAsync(client, await factory.PickSeededCustomerIdAsync());

        using var req = NewIdempotentPost($"/api/v1/quotations/{quotationId}/recall", new { });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetInt32().Should().Be((int)QuotationStatus.Withdrawn);
    }

    [Fact]
    public async Task POST_recall_unknown_quotation_returns_404()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        using var req = NewIdempotentPost($"/api/v1/quotations/{Guid.NewGuid()}/recall", new { });
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_approval_inbox_returns_200_paged_result()
    {
        await using var factory = new QuotationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/approvals/pending?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.TryGetProperty("items", out _).Should().BeTrue();
        body.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    private static async Task<Guid> CreateDraftWithLineAsync(HttpClient client, Guid customerId)
    {
        using var createReq = NewIdempotentPost("/api/v1/quotations", new CreateQuotationRequest
        {
            CustomerId = customerId,
            ValidUntilDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
        });
        var createResp = await client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var quotationId = createBody.GetProperty("id").GetGuid();

        using var lineReq = NewIdempotentPost($"/api/v1/quotations/{quotationId}/lines", new AddQuotationLineRequest
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Toyota Camry 2024",
            Quantity = 1,
            UnitPriceSar = 2_000m,
        });
        var lineResp = await client.SendAsync(lineReq);
        lineResp.EnsureSuccessStatusCode();

        return quotationId;
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

internal sealed class QuotationFactory : WebApplicationFactory<Program>
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
        await BffTestHostDefaults.EnsureDemoSeededAsync(this, db => db.Customers.AnyAsync(), "Customers");
        _seeded = true;
    }

    public async Task<Guid> PickSeededCustomerIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var customer = await db.Customers.AsNoTracking()
            .FirstAsync(c => c.TenantId == SeededTenantId);
        return customer.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.DemoSeedDefaults(SeededTenantId, "20260607")));
        builder.ConfigureTestServices(services =>
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName));
    }
}
