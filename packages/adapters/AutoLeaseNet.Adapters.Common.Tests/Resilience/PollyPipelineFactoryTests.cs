using System.Net;
using AutoLeaseNet.Adapters.Common.Resilience;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Common.Tests.Resilience;

public sealed class PollyPipelineFactoryTests
{
    private static ResilienceOptions FastRetry() => new()
    {
        MaxRetryAttempts = 2,
        BaseDelay = TimeSpan.FromMilliseconds(1),
        Timeout = TimeSpan.FromSeconds(5),
        // Set a very high minimum throughput so the breaker doesn't fire during these unit tests.
        CircuitBreakerMinimumThroughput = 1000,
    };

    // T1.7 — Retry triggers on HttpRequestException (transient network failure).
    [Fact]
    public async Task Pipeline_retries_on_HttpRequestException()
    {
        var pipeline = PollyPipelineFactory.Build("test", FastRetry());
        var attempts = 0;

        Func<Task> act = async () =>
        {
            await pipeline.ExecuteAsync<HttpResponseMessage>(_ =>
            {
                attempts++;
                throw new HttpRequestException("simulated network failure");
            });
        };

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(3, because: "initial attempt + 2 retries = 3 calls total");
    }

    // T1.7 — No retry on 4xx (other than 408/429): one attempt only.
    [Fact]
    public async Task Pipeline_does_not_retry_on_400_BadRequest()
    {
        var pipeline = PollyPipelineFactory.Build("test", FastRetry());
        var attempts = 0;

        var result = await pipeline.ExecuteAsync(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        attempts.Should().Be(1, because: "4xx is caller-fault; retrying won't change the outcome");
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // T1.7 — Retries on 5xx (transient server fault).
    [Fact]
    public async Task Pipeline_retries_on_500_InternalServerError()
    {
        var pipeline = PollyPipelineFactory.Build("test", FastRetry());
        var attempts = 0;

        var result = await pipeline.ExecuteAsync(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        attempts.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
