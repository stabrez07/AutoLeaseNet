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
/// Workstream 2026-05-28-tajeer-close-saga — coverage for
/// <see cref="ITajeerContractClient.CalculatePaymentAsync"/> and
/// <see cref="ITajeerContractClient.CloseAsync"/> against the same stub
/// HttpClient pattern <see cref="TajeerContractClientTests"/> uses for Save.
/// Asserts URL, HTTP method, payload shape, and the four failure mappings.
/// </summary>
public sealed class TajeerContractClientCalculateAndCloseTests
{
    private const string CalculateHappyJson = """
    {
      "contractNumber": 5000,
      "rentAmount": 500.00,
      "paidAmount": 100.00,
      "lateHoursFee": 25.00,
      "extraKmFee": 50.00,
      "damagesFee": 80.00,
      "discountAmount": 20.00,
      "totalDue": 535.00,
      "vatAmount": 80.25,
      "grandTotal": 615.25
    }
    """;

    private const string CloseHappyJson = """
    {
      "contractNumber": 5000,
      "contractStatusCode": 2,
      "closedAt": "2026-05-28T14:00",
      "finalPaidAmount": 615.25
    }
    """;

    private static CalculatePaymentRequest CalcReq() => new()
    {
        ContractNumber = 5000L,
        ReturnDate = "2026-05-28T14:00",
        ReturnedKm = 50320,
        ReturnedFuelLevelCode = 3,
        ExtraKm = 50,
        AdditionalCharges = 80m,
        DiscountAmount = 20m,
    };

    private static CloseContractRequest CloseReq() => new()
    {
        ContractNumber = 5000L,
        ClosureMainReasonCode = 1,
        ReturnDate = "2026-05-28T14:00",
        ReturnedKm = 50320,
        ReturnedFuelLevelCode = 3,
        FinalPaidAmount = 615.25m,
    };

    [Fact]
    public async Task CalculatePaymentAsync_PUTs_to_calculate_payment_with_serialized_body()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CalculateHappyJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CalculatePaymentAsync(CalcReq());

        result.IsSuccess.Should().BeTrue(because: $"got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value!.GrandTotal.Should().Be(615.25m);
        result.Value.ExtraKmFee.Should().Be(50m);

        stub.LastRequest!.Method.Should().Be(HttpMethod.Put);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/contracts/calculate-payment");
        stub.LastBody.Should().Contain("\"contractNumber\":5000");
        stub.LastBody.Should().Contain("\"extraKm\":50");
        stub.LastBody.Should().Contain("\"discountAmount\":20");
    }

    [Fact]
    public async Task CalculatePaymentAsync_maps_vendor_error_envelope_on_200_to_non_transient_failure()
    {
        const string errorBody = """
        { "errorKey": "server.error.contract.not_active", "errorCode": 312, "message": "Contract is not active" }
        """;
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CalculatePaymentAsync(CalcReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.not_active");
    }

    [Fact]
    public async Task CalculatePaymentAsync_maps_503_to_transient_failure()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CalculatePaymentAsync(CalcReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.503");
    }

    [Fact]
    public async Task CalculatePaymentAsync_maps_network_failure_to_transient()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CalculatePaymentAsync(CalcReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.network");
    }

    [Fact]
    public async Task CloseAsync_PUTs_to_closure_with_finalPaidAmount_and_closureMainReasonCode()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CloseHappyJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CloseAsync(CloseReq());

        result.IsSuccess.Should().BeTrue(because: $"got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value!.ContractStatusCode.Should().Be(2);
        result.Value.FinalPaidAmount.Should().Be(615.25m);

        stub.LastRequest!.Method.Should().Be(HttpMethod.Put);
        stub.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/contracts/closure");
        stub.LastBody.Should().Contain("\"closureMainReasonCode\":1");
        stub.LastBody.Should().Contain("\"finalPaidAmount\":615.25");
    }

    [Fact]
    public async Task CloseAsync_maps_vendor_business_error_on_400()
    {
        const string errorBody = """
        { "errorKey": "server.error.contract.already_closed", "errorCode": 421, "rawMessage": "Contract is already closed" }
        """;
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CloseAsync(CloseReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.already_closed");
    }

    [Fact]
    public async Task CloseAsync_maps_429_to_transient_failure()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("rate limited", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, "https://tajeer-stg.api.elm.sa");
        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);

        var result = await sut.CloseAsync(CloseReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.429");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

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
