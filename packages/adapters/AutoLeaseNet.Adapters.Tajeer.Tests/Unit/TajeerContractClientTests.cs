using System.Net;
using System.Text;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// T4.x — TajeerContractClient.SaveAsync, exercised against a stub HttpClientFactory.
/// Polly-pipeline retry behaviour is asserted separately in
/// <see cref="TajeerContractClientResilienceTests"/> (DI-wired test).
/// </summary>
public sealed class TajeerContractClientTests
{
    private const string HappyResponseJson = """
    {
      "contractNumber": 9876543210,
      "token": "tok_abc123",
      "issuanceURL": "https://tajeerstg.logisti.sa/#/public-contract/9876543210/tok_abc123",
      "mainPaymentDetails":  { "paid": 100.0, "remaining": 0.0,   "total": 100.0, "vat": 13.04 },
      "otherPaymentDetails": { "paid": 0.0,   "remaining": 50.0,  "total": 50.0,  "vat": 6.52 },
      "totalPaymentDetails": { "paid": 100.0, "remaining": 50.0,  "total": 150.0, "vat": 19.56 }
    }
    """;

    private static SaveContractRequest MinimalRequest() => new()
    {
        Renter = new RenterDto
        {
            PersonAddress = "Riyadh, Olaya",
            Mobile = "0501234567",
            IdTypeCode = 1,
            IdNumber = 1234567890,
        },
        PaymentDetails = new PaymentDetailsDto
        {
            PaymentMethodCode = 1,
            RentAmount = 150m,
        },
        VehicleDetails = new VehicleDetailsDto
        {
            VehicleId = 4242,
        },
        WorkingBranchId = 1,
        RentPolicyId = 1,
        ContractStartDate = "2026-05-23T10:00",
        ContractEndDate = "2026-05-25T10:00",
        ReceiveBranchId = 1,
        ReturnBranchId = 1,
        ContractTypeCode = 1,
        OperatorId = 999,
    };

    // T4.3 RED → T4.4 GREEN.
    [Fact]
    public async Task SaveAsync_returns_mapped_response_on_2xx_with_valid_body()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(HappyResponseJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeTrue(
            because: $"valid 200 body should map to Success; got {result.ErrorCode} — {result.ErrorMessage}");
        result.Value.Should().NotBeNull();
        result.Value!.ContractNumber.Should().Be(9876543210);
        result.Value.Token.Should().Be("tok_abc123");
        result.Value.IssuanceUrl.Should().StartWith("https://tajeerstg.logisti.sa/#/public-contract/");
        result.Value.TotalPaymentDetails.Total.Should().Be(150m);
        result.Value.TotalPaymentDetails.Vat.Should().Be(19.56m);
    }

    [Fact]
    public async Task SaveAsync_posts_request_body_as_camelCase_json_with_tajeer_typo_preserved()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(HappyResponseJson, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var request = MinimalRequest() with
        {
            AdditionalServices = new AdditionalServicesDto { DeliveryRequested = true, ChildSeatCount = 1 },
        };

        _ = await sut.SaveAsync(request);

        stub.LastRequest.Should().NotBeNull();
        stub.LastRequest!.Method.Should().Be(HttpMethod.Post);
        stub.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");

        stub.LastBody.Should().NotBeNullOrEmpty();
        // Tajeer's documented misspelling MUST be on the wire — Spec 03 §6.2.
        stub.LastBody.Should().Contain("\"addtionalServices\":");
        stub.LastBody.Should().NotContain("\"additionalServices\":");
        stub.LastBody.Should().Contain("\"workingBranchId\":1");
        stub.LastBody.Should().Contain("\"contractTypeCode\":1");
    }

    [Fact]
    public async Task SaveAsync_returns_vendor_business_error_on_4xx_with_errorKey()
    {
        const string errorBody = """
        {
          "errorKey": "server.error.renter.mobile.invalid",
          "errorCode": 168,
          "rawMessage": "Renter mobile must be a valid Saudi mobile number"
        }
        """;
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse(because: "business validation isn't going to pass on retry");
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.renter.mobile.invalid");
        result.ErrorMessage.Should().Contain("Renter mobile");
    }

    [Fact]
    public async Task SaveAsync_returns_vendor_business_error_on_200_with_errorKey()
    {
        // Defensive parsing per Spec 03 §8.1 Q4 — Tajeer occasionally returns 200 + error body.
        const string errorBody = """
        {
          "errorKey": "server.error.contract.duplicate",
          "errorCode": 412,
          "message": "Contract already saved for this vehicle"
        }
        """;
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.duplicate");
        result.ErrorMessage.Should().Contain("Contract already saved");
    }

    [Fact]
    public async Task SaveAsync_returns_transient_failure_on_5xx()
    {
        var stub = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.503");
    }

    [Fact]
    public async Task SaveAsync_returns_transient_failure_on_network_exception()
    {
        var stub = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("connection refused"));
        var factory = new StubHttpClientFactory(stub, baseAddress: "https://tajeer-stg.api.elm.sa");

        var sut = new TajeerContractClient(factory, NullLogger<TajeerContractClient>.Instance);
        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.network");
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
