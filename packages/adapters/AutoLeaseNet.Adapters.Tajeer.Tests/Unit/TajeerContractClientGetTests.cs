using System.Net;
using System.Text;
using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// TajeerContractClient.GetAsync — read-side method for the reconciliation drift detector.
/// Mirrors the failure-semantics surface of Save/Close/etc. plus a 404 → vendor-not-found
/// branch unique to reads.
/// </summary>
public sealed class TajeerContractClientGetTests
{
    private const string HappyResponseJson = """
    {
      "contractNumber": 9876543210,
      "contractStatusCode": 4,
      "extensionCount": 0
    }
    """;

    [Fact]
    public async Task GetAsync_returns_mapped_response_on_2xx_with_valid_body()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(HappyResponseJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.GetAsync(9876543210L);

        result.IsSuccess.Should().BeTrue(
            because: $"valid 200 body should map to Success; got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value!.ContractNumber.Should().Be(9876543210L);
        result.Value.ContractStatusCode.Should().Be(4);
    }

    [Fact]
    public async Task GetAsync_uses_GET_method_with_contractNumber_in_path()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(HappyResponseJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        _ = await sut.GetAsync(9876543210L);

        stub.LastRequest.Should().NotBeNull();
        stub.LastRequest!.Method.Should().Be(HttpMethod.Get);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/contracts/9876543210");
        stub.LastRequest.Content.Should().BeNull(because: "GET never carries a body");
    }

    [Fact]
    public async Task GetAsync_returns_vendor_not_found_on_404()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("", Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.GetAsync(9876543210L);

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse(because: "404 is a drift signal, not a retryable infra blip");
        result.ErrorCode.Should().Be("tajeer.vendor.contract.not_found");
    }

    [Fact]
    public async Task GetAsync_returns_transient_failure_on_5xx()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.GetAsync(9876543210L);

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.503");
    }

    [Fact]
    public async Task GetAsync_returns_transient_failure_on_network_exception()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.GetAsync(9876543210L);

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.network");
    }

    [Fact]
    public async Task GetAsync_throws_on_non_positive_contract_number()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        await FluentActions.Awaiting(() => sut.GetAsync(0L)).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => sut.GetAsync(-1L)).Should().ThrowAsync<ArgumentOutOfRangeException>();
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
            return new HttpClient(_handler, disposeHandler: false) { BaseAddress = _baseAddress };
        }
    }
}
