using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Bff.Tests.Endpoints;

/// <summary>
/// Day F — read-only lookup endpoints. Reuses the seed-driven WebApplicationFactory from
/// the SaveContract endpoint tests so the same demo dataset (3 branches / 4 policies /
/// 3 coverages / 20 customers / 60 vehicles / 80 drivers) is queryable.
/// </summary>
public sealed class LookupEndpointsTests : IClassFixture<SaveContractEndpointFactory>
{
    private readonly SaveContractEndpointFactory _factory;
    public LookupEndpointsTests(SaveContractEndpointFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_lookups_branches_returns_3_seeded_branches()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/branches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var arr = JsonDocument.Parse(json).RootElement;
        arr.GetArrayLength().Should().Be(3);
        arr[0].GetProperty("code").GetString().Should().StartWith("DMM-")
            .And.Subject.Should().BeOfType<string>();
        arr.EnumerateArray().Should().AllSatisfy(b =>
            b.GetProperty("isActive").GetBoolean().Should().BeTrue());
    }

    [Fact]
    public async Task GET_lookups_rent_policies_returns_4_seeded_policies()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/rent-policies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        arr.GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task GET_lookups_extended_coverages_returns_3_seeded_coverages()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/extended-coverages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        arr.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GET_lookups_customers_paged_default_returns_first_50()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(20);
        root.GetProperty("page").GetInt32().Should().Be(1);
        root.GetProperty("pageSize").GetInt32().Should().Be(50);
        root.GetProperty("items").GetArrayLength().Should().Be(20);
    }

    [Fact]
    public async Task GET_lookups_customers_with_search_filters_by_name()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/customers?search=Aramco");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(1, because: "only the seeded Aramco row matches");
        root.GetProperty("items")[0].GetProperty("displayName").GetString().Should().Contain("Aramco");
    }

    [Fact]
    public async Task GET_lookups_vehicles_paged_returns_60_total()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/vehicles?pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(60);
        root.GetProperty("items").GetArrayLength().Should().Be(60);
    }

    [Fact]
    public async Task GET_lookups_vehicles_with_status_filter_returns_only_matching_rows()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        // Status 1 = Available — seeder leaves them all Available initially.
        var response = await client.GetAsync("/api/v1/lookups/vehicles?status=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        foreach (var v in root.GetProperty("items").EnumerateArray())
        {
            v.GetProperty("status").GetInt32().Should().Be(1);
        }
    }

    [Fact]
    public async Task GET_lookups_drivers_paged_returns_80_total()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/drivers?pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("totalCount").GetInt32().Should().Be(80);
        root.GetProperty("items").GetArrayLength().Should().Be(80);
    }

    [Fact]
    public async Task GET_lookups_branches_returns_401_when_no_auth_header()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient(); // no auth header

        var response = await client.GetAsync("/api/v1/lookups/branches");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_lookups_customers_pageSize_clamped_to_max_200()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/lookups/customers?pageSize=10000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("pageSize").GetInt32().Should().Be(200, because: "MaxPageSize caps the user-supplied value");
    }
}
