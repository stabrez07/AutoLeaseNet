using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Bff.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Endpoints;

public sealed class QuotationPricingSetupEndpointTests : IClassFixture<QuotationPricingSetupFactory>
{
    private readonly QuotationPricingSetupFactory _factory;

    public QuotationPricingSetupEndpointTests(QuotationPricingSetupFactory factory) => _factory = factory;

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", QuotationPricingSetupFactory.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "InternalStaff");
        return client;
    }

    private static JsonElement BuildValidPayload()
    {
        var payload = new
        {
            vehicles = new[] { new { id = "v-1", make = "Toyota", model = "Camry" } },
            insurance = Array.Empty<object>(),
            vehicleInterest = Array.Empty<object>(),
            depreciation = Array.Empty<object>(),
            maintenance = Array.Empty<object>(),
            discountOptions = Array.Empty<object>(),
            trackingCharges = Array.Empty<object>(),
            leaseTerms = Array.Empty<object>(),
            interestRateTable = Array.Empty<object>(),
            residualValueTable = Array.Empty<object>(),
            replacementPolicy = Array.Empty<object>(),
            feeMaster = Array.Empty<object>(),
            commissionRateTable = Array.Empty<object>(),
            profitMarginSetup = Array.Empty<object>(),
            calendarPeriods = Array.Empty<object>(),
        };
        var json = JsonSerializer.Serialize(payload);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task PUT_without_Idempotency_Key_returns_400()
    {
        using var client = CreateClient();
        var payload = BuildValidPayload();

        var response = await client.PutAsJsonAsync("/api/v1/admin/quotation-pricing-setup", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Idempotency-Key");
    }

    [Fact]
    public async Task PUT_with_valid_payload_returns_200_with_schema_version()
    {
        using var client = CreateClient();
        var payload = BuildValidPayload();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/quotation-pricing-setup")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseBody);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(QuotationPricingSetupEndpoints.CurrentSchemaVersion);
        doc.RootElement.TryGetProperty("updatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PUT_with_missing_required_arrays_returns_400()
    {
        using var client = CreateClient();
        var incompletePayload = new { vehicles = Array.Empty<object>() };

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/quotation-pricing-setup")
        {
            Content = JsonContent.Create(incompletePayload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("insurance");
    }

    [Fact]
    public async Task PUT_with_non_array_property_returns_400()
    {
        using var client = CreateClient();
        var badPayload = new
        {
            vehicles = "not-an-array",
            insurance = Array.Empty<object>(),
            vehicleInterest = Array.Empty<object>(),
            depreciation = Array.Empty<object>(),
            maintenance = Array.Empty<object>(),
            discountOptions = Array.Empty<object>(),
            trackingCharges = Array.Empty<object>(),
            leaseTerms = Array.Empty<object>(),
            interestRateTable = Array.Empty<object>(),
            residualValueTable = Array.Empty<object>(),
            replacementPolicy = Array.Empty<object>(),
            feeMaster = Array.Empty<object>(),
            commissionRateTable = Array.Empty<object>(),
            profitMarginSetup = Array.Empty<object>(),
            calendarPeriods = Array.Empty<object>(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/quotation-pricing-setup")
        {
            Content = JsonContent.Create(badPayload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("vehicles");
        body.Should().Contain("must be an array");
    }

    [Fact]
    public async Task PUT_with_future_schema_version_returns_400()
    {
        using var client = CreateClient();
        var futurePayload = new
        {
            schemaVersion = 999,
            vehicles = Array.Empty<object>(),
            insurance = Array.Empty<object>(),
            vehicleInterest = Array.Empty<object>(),
            depreciation = Array.Empty<object>(),
            maintenance = Array.Empty<object>(),
            discountOptions = Array.Empty<object>(),
            trackingCharges = Array.Empty<object>(),
            leaseTerms = Array.Empty<object>(),
            interestRateTable = Array.Empty<object>(),
            residualValueTable = Array.Empty<object>(),
            replacementPolicy = Array.Empty<object>(),
            feeMaster = Array.Empty<object>(),
            commissionRateTable = Array.Empty<object>(),
            profitMarginSetup = Array.Empty<object>(),
            calendarPeriods = Array.Empty<object>(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/quotation-pricing-setup")
        {
            Content = JsonContent.Create(futurePayload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("not supported");
    }

    [Fact]
    public async Task GET_returns_empty_payload_with_schema_version()
    {
        using var client = _factory.CreateClient();
        var uniqueTenant = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Dev-Tenant-Id", uniqueTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Dev-User-Type", "InternalStaff");

        var response = await client.GetAsync("/api/v1/admin/quotation-pricing-setup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(QuotationPricingSetupEndpoints.CurrentSchemaVersion);
        doc.RootElement.GetProperty("vehicles").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GET_after_PUT_returns_saved_data_with_audit_metadata()
    {
        using var client = CreateClient();
        var payload = BuildValidPayload();

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/quotation-pricing-setup")
        {
            Content = JsonContent.Create(payload),
        };
        putRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var putResponse = await client.SendAsync(putRequest);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/v1/admin/quotation-pricing-setup");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(QuotationPricingSetupEndpoints.CurrentSchemaVersion);
        doc.RootElement.TryGetProperty("updatedBy", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("updatedAt", out _).Should().BeTrue();
        doc.RootElement.GetProperty("vehicles").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void ValidateSetupPayload_returns_no_errors_for_valid_payload()
    {
        var element = BuildValidPayload();
        var errors = QuotationPricingSetupEndpoints.ValidateSetupPayload(element);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSetupPayload_returns_errors_for_missing_properties()
    {
        var json = JsonSerializer.Serialize(new { vehicles = Array.Empty<object>() });
        var element = JsonDocument.Parse(json).RootElement;
        var errors = QuotationPricingSetupEndpoints.ValidateSetupPayload(element);
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("insurance"));
    }
}

public sealed class QuotationPricingSetupFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TenantId = Guid.Parse("b2b2b2b2-0002-0000-0000-000000000002");
    private readonly string _dbName = $"pricing-setup-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(BffTestHostDefaults.Defaults()));
        builder.ConfigureTestServices(services =>
        {
            BffTestHostDefaults.ReplaceDbContextWithInMemory(services, _dbName);
        });
    }
}
