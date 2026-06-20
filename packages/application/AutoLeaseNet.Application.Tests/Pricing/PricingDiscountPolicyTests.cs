using AutoLeaseNet.Domain.Pricing;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Pricing;

public sealed class PricingDiscountPolicyTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa3333-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDefault_includes_10_and_20_percent_presets()
    {
        var policy = PricingDiscountPolicy.CreateDefault(TenantId, Now);

        policy.MaxDiscountPercent.Should().Be(20m);
        policy.AllowedPresets.Should().BeEquivalentTo([10m, 20m]);
        policy.IsPresetAllowed(10m).Should().BeTrue();
        policy.IsPresetAllowed(20m).Should().BeTrue();
    }

    [Fact]
    public void SetAllowedPresets_rejects_values_above_max_discount()
    {
        var policy = PricingDiscountPolicy.CreateDefault(TenantId, Now);

        var act = () => policy.SetAllowedPresets([10m, 25m], Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lowering_max_discount_below_existing_presets_fails()
    {
        var policy = PricingDiscountPolicy.CreateDefault(TenantId, Now);

        var act = () => policy.SetMaxDiscountPercent(15m, Now);

        act.Should().Throw<InvalidOperationException>();
    }
}
