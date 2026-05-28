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
/// Day-20 workstream — coverage for <see cref="ITajeerContractClient.ExtendAsync"/>
/// and <see cref="ITajeerContractClient.SuspendAsync"/> through the same stub
/// HttpClient pattern used for Save / Calculate / Close.
/// </summary>
public sealed class TajeerContractClientExtendAndSuspendTests
{
    private const string ExtendHappyJson = """
    {
      "contractNumber": 6000,
      "contractStatusCode": 4,
      "newContractEndDate": "2026-06-10T18:00",
      "totalDue": 150.00,
      "vatAmount": 22.50,
      "grandTotal": 172.50
    }
    """;

    private const string SuspendHappyJson = """
    {
      "contractNumber": 6000,
      "contractStatusCode": 3,
      "suspendedAt": "2026-05-28T14:00"
    }
    """;

    private static ExtendContractRequest ExtendReq() => new()
    {
        ContractNumber = 6000L,
        NewContractEndDate = "2026-06-10T18:00",
        ExtensionReasonCode = 2,
        AdditionalChargesAmount = 150m,
        PaymentMethodCode = 1,
    };

    private static SuspendContractRequest SuspendReq() => new()
    {
        ContractNumber = 6000L,
        SuspensionReasonCode = 7, // e.g. NON_TRAFFIC_DAMAGE
        SuspensionNotes = "Body shop",
        SuspendedAt = "2026-05-28T14:00",
    };

    [Fact]
    public async Task ExtendAsync_PUTs_to_extend_with_serialized_body()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ExtendHappyJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.ExtendAsync(ExtendReq());

        result.IsSuccess.Should().BeTrue(because: $"got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value!.ContractStatusCode.Should().Be(4);
        result.Value.NewContractEndDate.Should().Be("2026-06-10T18:00");
        result.Value.GrandTotal.Should().Be(172.50m);

        stub.LastRequest!.Method.Should().Be(HttpMethod.Put);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/contracts/extend");
        stub.LastBody.Should().Contain("\"contractNumber\":6000");
        stub.LastBody.Should().Contain("\"newContractEndDate\":\"2026-06-10T18:00\"");
        stub.LastBody.Should().Contain("\"additionalChargesAmount\":150");
    }

    [Fact]
    public async Task ExtendAsync_maps_vendor_error_envelope_to_non_transient_failure()
    {
        const string errorBody = """
        { "errorKey": "server.error.contract.max_extensions", "errorCode": 471, "rawMessage": "Max extensions reached" }
        """;
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.ExtendAsync(ExtendReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.max_extensions");
    }

    [Fact]
    public async Task SuspendAsync_PUTs_to_suspend_with_reason_code_in_body()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuspendHappyJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.SuspendAsync(SuspendReq());

        result.IsSuccess.Should().BeTrue(because: $"got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value!.ContractStatusCode.Should().Be(3);

        stub.LastRequest!.Method.Should().Be(HttpMethod.Put);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/contracts/suspend");
        stub.LastBody.Should().Contain("\"suspensionReasonCode\":7");
        stub.LastBody.Should().Contain("\"suspendedAt\":\"2026-05-28T14:00\"");
    }

    [Fact]
    public async Task SuspendAsync_maps_503_to_transient_failure()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.SuspendAsync(SuspendReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.503");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            return _respond(request);
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
