using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace AutoLeaseNet.Adapters.Common.Resilience;

/// <summary>
/// Default Polly v8 resilience pipeline used by every Pattern B adapter
/// (Tajeer, ZATCA, Nafath, D365). Per doc 03 §9 and doc 04 §9.
/// </summary>
public static class ResiliencePolicies
{
    public static void DefaultHttpPipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext context)
    {
        builder
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => IsTransient(r))
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .AddConcurrencyLimiter(permitLimit: 50, queueLimit: 100);
    }

    public static bool IsTransient(HttpResponseMessage response) =>
        (int)response.StatusCode >= 500 ||
        response.StatusCode == HttpStatusCode.RequestTimeout ||
        response.StatusCode == HttpStatusCode.TooManyRequests;
}
