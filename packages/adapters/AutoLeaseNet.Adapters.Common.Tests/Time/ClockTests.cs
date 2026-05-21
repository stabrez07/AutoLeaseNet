using AutoLeaseNet.Application.Ports.Time;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Common.Tests.Time;

public sealed class ClockTests
{
    // T1.8 — IClock + SystemClock + FakeClock. Domain/application code must depend on
    // IClock (never DateTime.UtcNow directly) so that time can be controlled in tests.
    [Fact]
    public void SystemClock_returns_UTC_close_to_DateTimeOffset_UtcNow()
    {
        IClock clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;

        var now = clock.UtcNow;

        var after = DateTimeOffset.UtcNow;
        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        now.Offset.Should().Be(TimeSpan.Zero, because: "UtcNow must be UTC");
    }

    [Fact]
    public void FakeClock_returns_deterministic_timestamp_injected_by_test()
    {
        var fixedMoment = new DateTimeOffset(2026, 5, 18, 14, 30, 0, TimeSpan.Zero);
        IClock clock = new FakeClock(fixedMoment);

        clock.UtcNow.Should().Be(fixedMoment);
        clock.UtcNow.Should().Be(fixedMoment, because: "second read still returns the same fixed moment");
    }

    [Fact]
    public void FakeClock_advances_when_test_advances()
    {
        var fake = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var t0 = fake.UtcNow;

        fake.Advance(TimeSpan.FromHours(2));

        (fake.UtcNow - t0).Should().Be(TimeSpan.FromHours(2));
    }

    /// <summary>Test-double clock that returns a controlled timestamp.</summary>
    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        private DateTimeOffset _now = initial;

        public DateTimeOffset UtcNow => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
