using AutoLeaseNet.Adapters.Common.Observability;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Common.Tests.Observability;

public sealed class PiiMaskingTests
{
    // T1.4 — RED: this test fails because Mask throws NotImplementedException.
    // T1.5 will implement the masker; test goes GREEN.
    [Fact]
    public void PiiMasking_masks_id_number_keeps_last_4()
    {
        // KSA Iqama / National ID example (10 digits)
        var masked = PiiMasking.Mask("idNumber", "1028558326");

        masked.Should().Be("******8326");
    }

    // T1.5 — additional cases: IBAN + license number both follow keep-last-4 policy.
    [Fact]
    public void PiiMasking_masks_iban_keeps_last_4()
    {
        // KSA IBAN: SA + 22 chars = 24 total
        var masked = PiiMasking.Mask("iban", "SA0380000000608010167519");

        masked.Should().Be("********************7519");
        masked.Length.Should().Be(24);
    }

    [Fact]
    public void PiiMasking_masks_drive_license_number_keeps_last_4()
    {
        var masked = PiiMasking.Mask("driveLicenseNumber", "087333256111");

        masked.Should().Be("********6111");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PiiMasking_returns_input_unchanged_for_null_or_empty(string? value)
    {
        var masked = PiiMasking.Mask("idNumber", value!);

        masked.Should().Be(value);
    }

    [Fact]
    public void PiiMasking_returns_short_value_fully_masked()
    {
        // Defensive: if a "10-digit ID" arrives with only 2 chars, mask all of it
        // — never accidentally leak a value shorter than the keep-N policy.
        var masked = PiiMasking.Mask("idNumber", "12");

        masked.Should().Be("**");
    }

    [Fact]
    public void PiiMasking_returns_triple_asterisks_for_unknown_sensitive_field()
    {
        // Email / password / secret / etc. — bulk mask, don't leak any chars
        var masked = PiiMasking.Mask("email", "ali@example.com");

        masked.Should().Be("***");
    }
}
