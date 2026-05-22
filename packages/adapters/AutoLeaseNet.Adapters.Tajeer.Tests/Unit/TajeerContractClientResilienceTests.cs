using System.Net;
using System.Text;
using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Retry;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// T4.6 — assert that the Tajeer named-client Polly pipeline retries on 503 and that
/// <see cref="TajeerContractClient"/> ultimately surfaces a transient
/// <see cref="AutoLeaseNet.Adapters.Common.Result.IntegrationResult{T}"/> failure.
///
/// The production pipeline lives in <c>ResiliencePolicies.DefaultHttpPipeline</c> with
/// <c>MaxRetryAttempts = 3</c>, exponential backoff (base 2s) + jitter. To keep this test
/// fast and deterministic, we wire a parallel pipeline here with <c>Delay = TimeSpan.Zero</c>
/// and <c>UseJitter = false</c>, mirroring the same retry predicate
/// (5xx ∨ 408 ∨ 429 ∨ <see cref="HttpRequestException"/>). The shape is asserted-equivalent;
/// the per-attempt delay is the only deliberate deviation.
/// </summary>
public sealed class TajeerContractClientResilienceTests
{
    private static SaveContractRequest MinimalRequest() => new()
    {
        Renter = new RenterDto { PersonAddress = "x", Mobile = "0501234567", IdTypeCode = 1, IdNumber = 1 },
        PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = 1m },
        VehicleDetails = new VehicleDetailsDto { VehicleId = 1 },
        WorkingBranchId = 1,
        RentPolicyId = 1,
        ContractStartDate = "2026-05-23T10:00",
        ContractEndDate = "2026-05-25T10:00",
        ReceiveBranchId = 1,
        ReturnBranchId = 1,
        ContractTypeCode = 1,
        OperatorId = 1,
    };

    [Fact]
    public async Task SaveAsync_retries_three_times_on_503_then_returns_transient_failure()
    {
        var counter = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("upstream down", Encoding.UTF8, "text/plain"),
            });

        var sut = BuildContractClient(counter);
        var result = await sut.SaveAsync(MinimalRequest());

        // Initial attempt + 3 retries = 4 invocations total.
        counter.Calls.Should().Be(4, because: "Polly retry strategy is MaxRetryAttempts=3");
        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        result.ErrorCode.Should().Be("tajeer.http.503");
    }

    [Fact]
    public async Task SaveAsync_succeeds_when_upstream_recovers_within_retry_budget()
    {
        var counter = new CountingHandler(call => call < 2
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("retry me") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "contractNumber": 1,
                  "token": "t",
                  "issuanceURL": "https://x/y",
                  "mainPaymentDetails":  { "paid": 0, "remaining": 0, "total": 0, "vat": 0 },
                  "otherPaymentDetails": { "paid": 0, "remaining": 0, "total": 0, "vat": 0 },
                  "totalPaymentDetails": { "paid": 0, "remaining": 0, "total": 0, "vat": 0 }
                }
                """, Encoding.UTF8, "application/json"),
            });

        var sut = BuildContractClient(counter);
        var result = await sut.SaveAsync(MinimalRequest());

        counter.Calls.Should().Be(3, because: "two 503s + the recovering 200");
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContractNumber.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_does_not_retry_on_400_business_error()
    {
        var counter = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""
                {
                  "errorKey": "server.error.contract.invalid",
                  "errorCode": 1,
                  "rawMessage": "Bad input"
                }
                """, Encoding.UTF8, "application/json"),
            });

        var sut = BuildContractClient(counter);
        var result = await sut.SaveAsync(MinimalRequest());

        counter.Calls.Should().Be(1, because: "4xx business errors are not transient and must not retry");
        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.invalid");
    }

    private static TajeerContractClient BuildContractClient(HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services
            .AddHttpClient(ServiceCollectionExtensions.TajeerHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://tajeer-stg.api.elm.sa", UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
            .AddResilienceHandler("tajeer-resilience-test", builder =>
            {
                // Mirrors ResiliencePolicies.DefaultHttpPipeline retry predicate;
                // zero delay + no jitter so the test stays sub-millisecond.
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.Zero,
                    UseJitter = false,
                    BackoffType = DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(r =>
                            (int)r.StatusCode >= 500
                            || r.StatusCode == HttpStatusCode.RequestTimeout
                            || r.StatusCode == HttpStatusCode.TooManyRequests),
                });
            });

        var provider = services.BuildServiceProvider();
        return new TajeerContractClient(
            provider.GetRequiredService<IHttpClientFactory>(),
            NullLogger<TajeerContractClient>.Instance);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _respond;
        private int _calls;
        public int Calls => _calls;

        public CountingHandler(Func<int, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var idx = Interlocked.Increment(ref _calls);
            return Task.FromResult(_respond(idx - 1));
        }
    }
}
