using AutoLeaseNet.Adapters.Tajeer.Webhooks;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>T6.3 — Tajeer secret-key header validation (Spec 03 §12.2).</summary>
public sealed class WebhookSignatureValidatorTests
{
    [Fact]
    public void IsValid_returns_true_when_received_matches_expected()
    {
        WebhookSignatureValidator.IsValid("super-secret-shared-key", "super-secret-shared-key")
            .Should().BeTrue();
    }

    [Fact]
    public void IsValid_returns_false_when_received_does_not_match()
    {
        WebhookSignatureValidator.IsValid("imposter-key", "super-secret-shared-key")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "configured")]
    [InlineData("", "configured")]
    [InlineData("received", null)]
    [InlineData("received", "")]
    [InlineData(null, null)]
    public void IsValid_returns_false_when_either_side_is_null_or_empty(string? received, string? expected)
    {
        WebhookSignatureValidator.IsValid(received, expected).Should().BeFalse();
    }

    [Fact]
    public void IsValid_returns_false_when_lengths_differ_even_with_common_prefix()
    {
        // Defence against length-leak timing analysis — different lengths short-circuit,
        // but the contract is "false on mismatch" not "throw" so the caller doesn't see
        // exception timing variance either.
        WebhookSignatureValidator.IsValid("secret", "secret-with-suffix").Should().BeFalse();
    }
}
