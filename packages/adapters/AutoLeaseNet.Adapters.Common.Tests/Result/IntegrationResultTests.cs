using AutoLeaseNet.Adapters.Common.Result;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Common.Tests.Result;

public sealed class IntegrationResultTests
{
    // T1.1 — RED: this test fails because Success factory throws NotImplementedException.
    // T1.2 will implement the factory; test will go GREEN.
    [Fact]
    public void IntegrationResult_Success_carries_value()
    {
        var result = IntegrationResult<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.IsTransient.Should().BeFalse();
    }

    // T1.3 — Failure carries error metadata and distinguishes transient (retryable
    // network/5xx errors) from permanent (4xx business rule / validation errors).
    [Fact]
    public void IntegrationResult_Failure_distinguishes_transient_vs_permanent()
    {
        var transient = IntegrationResult<int>.Failure(
            errorCode: "NETWORK_TIMEOUT",
            errorMessage: "Upstream gateway timeout",
            isTransient: true,
            correlationId: "corr-abc-123");

        transient.IsSuccess.Should().BeFalse();
        transient.IsTransient.Should().BeTrue();
        transient.ErrorCode.Should().Be("NETWORK_TIMEOUT");
        transient.ErrorMessage.Should().Be("Upstream gateway timeout");
        transient.CorrelationId.Should().Be("corr-abc-123");
        transient.Value.Should().Be(default(int)); // 0 — no payload on failure

        var permanent = IntegrationResult<int>.Failure(
            errorCode: "TAJEER_LICENSE_EXPIRED",
            errorMessage: "Driver license has expired");

        permanent.IsSuccess.Should().BeFalse();
        permanent.IsTransient.Should().BeFalse(); // default
        permanent.ErrorCode.Should().Be("TAJEER_LICENSE_EXPIRED");
        permanent.CorrelationId.Should().BeNull(); // default
    }
}
