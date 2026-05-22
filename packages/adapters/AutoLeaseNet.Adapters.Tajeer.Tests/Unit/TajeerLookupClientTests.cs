using System.Net;
using System.Text;
using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Lookups;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

public sealed class TajeerLookupClientTests
{
    // T3.5 — exercises GetAllBranchesAsync with a stubbed IHttpClientFactory whose
    // named "tajeer" client routes through a StubHttpMessageHandler. Verifies happy-path
    // JSON → DTO mapping, non-2xx → Failure (non-transient), and 5xx → Failure (transient).

    private const string BranchesJson = """
    [
      {
        "id": 101,
        "code": "RUH-01",
        "nameAr": "فرع الرياض",
        "nameEn": "Riyadh Branch",
        "cityAr": "الرياض",
        "cityEn": "Riyadh",
        "regionAr": "منطقة الرياض",
        "regionEn": "Riyadh Region",
        "licenseNumber": "LIC-001",
        "isActive": true
      },
      {
        "id": 102,
        "code": "JED-01",
        "nameAr": "فرع جدة",
        "nameEn": "Jeddah Branch",
        "cityAr": "جدة",
        "cityEn": "Jeddah",
        "regionAr": "منطقة مكة",
        "regionEn": "Makkah Region",
        "licenseNumber": "LIC-002",
        "isActive": true
      }
    ]
    """;

    [Fact]
    public async Task GetAllBranchesAsync_returns_mapped_dtos_on_2xx_json()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BranchesJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerLookupClient(factory, NullLogger<TajeerLookupClient>.Instance);

        var result = await sut.GetAllBranchesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var branches = result.Value!;
        branches.Should().HaveCount(2);
        branches[0].Id.Should().Be(101);
        branches[0].NameEn.Should().Be("Riyadh Branch");
        branches[0].NameAr.Should().Be("فرع الرياض");
        branches[1].Id.Should().Be(102);
        branches[1].Code.Should().Be("JED-01");
        branches[1].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllBranchesAsync_hits_canonical_path_api_lookups_branches()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerLookupClient(factory, NullLogger<TajeerLookupClient>.Instance);
        _ = await sut.GetAllBranchesAsync();

        stub.LastRequest.Should().NotBeNull();
        stub.LastRequest!.Method.Should().Be(HttpMethod.Get);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/lookups/branches");
    }

    [Fact]
    public async Task GetAllBranchesAsync_returns_non_transient_failure_on_4xx()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid"}""", Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerLookupClient(factory, NullLogger<TajeerLookupClient>.Instance);
        var result = await sut.GetAllBranchesAsync();

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.http.400");
    }

    [Fact]
    public async Task GetAllBranchesAsync_returns_transient_failure_on_5xx()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerLookupClient(factory, NullLogger<TajeerLookupClient>.Instance);
        var result = await sut.GetAllBranchesAsync();

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.502");
    }

    [Fact]
    public async Task GetAllBranchesAsync_returns_transient_failure_on_network_exception()
    {
        var stub = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("connection refused"));
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerLookupClient(factory, NullLogger<TajeerLookupClient>.Instance);
        var result = await sut.GetAllBranchesAsync();

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.network");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        private readonly Uri _baseAddress;

        public StubHttpClientFactory(HttpMessageHandler handler, string baseAddress)
        {
            _handler = handler;
            _baseAddress = new Uri(baseAddress, UriKind.Absolute);
        }

        public HttpClient CreateClient(string name)
        {
            if (name != ServiceCollectionExtensions.TajeerHttpClientName)
            {
                throw new InvalidOperationException(
                    $"Test factory only knows '{ServiceCollectionExtensions.TajeerHttpClientName}', got '{name}'.");
            }
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = _baseAddress,
            };
        }
    }
}
