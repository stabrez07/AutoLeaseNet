using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace AutoLeaseNet.Adapters.Common.Resilience;

/// <summary>
/// Tunable resilience options for a Pattern B adapter pipeline.
/// Defaults match the conservative profile used for Tajeer in <see cref="ResiliencePolicies"/>.
/// </summary>
public sealed class ResilienceOptions
{
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Builds a standalone Polly v8 resilience pipeline for an adapter. Use this when you need
/// direct pipeline access (tests, background workers, custom handlers). For HttpClientFactory
/// integration, use <see cref="ResiliencePolicies.DefaultHttpPipeline"/>.
///
/// Pipeline composition (outer → inner):
///   Timeout → Retry (exponential + jitter) → CircuitBreaker
///
/// Retries on: HttpRequestException, TaskCanceledException, 5xx, 408, 429
/// Does NOT retry on: 4xx (except 408/429) — those are caller-fault and won't resolve by retrying.
/// Circuit opens on: HttpRequestException, 5xx over the configured failure ratio.
/// </summary>
public static class PollyPipelineFactory
{
    public static ResiliencePipeline<HttpResponseMessage> Build(string adapterName, ResilienceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        ArgumentNullException.ThrowIfNull(options);

        return new ResiliencePipelineBuilder<HttpResponseMessage>
            {
                Name = adapterName,
            }
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.Timeout,
                Name = $"{adapterName}-timeout",
            })
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                Name = $"{adapterName}-retry",
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(IsTransientResponse),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                Name = $"{adapterName}-breaker",
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500),
            })
            .Build();
    }

    private static bool IsTransientResponse(HttpResponseMessage response)
        => (int)response.StatusCode >= 500
            || response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.TooManyRequests;
}
