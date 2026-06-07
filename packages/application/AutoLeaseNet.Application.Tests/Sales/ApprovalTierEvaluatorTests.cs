using AutoLeaseNet.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

/// <summary>
/// Coverage for <see cref="ApprovalTierEvaluator"/> (Spec 08 §3): threshold selection,
/// ordering, inactive-tier exclusion.
/// </summary>
public sealed class ApprovalTierEvaluatorTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa2222-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private static ApprovalTier Tier(byte level, decimal min, bool active = true) =>
        ApprovalTier.Create(TenantId, level, $"ROLE_T{level}", min, Now, active);

    private static ApprovalTier[] StandardTiers() =>
    [
        Tier(1, 0m),
        Tier(2, 50_000m),
        Tier(3, 200_000m),
    ];

    [Fact]
    public void Small_amount_requires_only_tier_1()
    {
        var required = ApprovalTierEvaluator.RequiredTiers(10_000m, StandardTiers());

        required.Should().HaveCount(1);
        required[0].TierLevel.Should().Be(1);
    }

    [Fact]
    public void Mid_amount_requires_tiers_1_and_2_in_order()
    {
        var required = ApprovalTierEvaluator.RequiredTiers(100_000m, StandardTiers());

        required.Select(t => t.TierLevel).Should().Equal((byte)1, (byte)2);
    }

    [Fact]
    public void Large_amount_at_threshold_boundary_requires_all_three()
    {
        var required = ApprovalTierEvaluator.RequiredTiers(200_000m, StandardTiers());

        required.Select(t => t.TierLevel).Should().Equal((byte)1, (byte)2, (byte)3);
    }

    [Fact]
    public void Unordered_input_is_returned_sorted_by_tier_level()
    {
        var shuffled = new[] { Tier(3, 200_000m), Tier(1, 0m), Tier(2, 50_000m) };

        var required = ApprovalTierEvaluator.RequiredTiers(500_000m, shuffled);

        required.Select(t => t.TierLevel).Should().Equal((byte)1, (byte)2, (byte)3);
    }

    [Fact]
    public void Inactive_tier_is_never_required_even_above_threshold()
    {
        var tiers = new[] { Tier(1, 0m), Tier(2, 50_000m, active: false) };

        var required = ApprovalTierEvaluator.RequiredTiers(100_000m, tiers);

        required.Select(t => t.TierLevel).Should().Equal((byte)1);
    }

    [Fact]
    public void No_tier_below_threshold_returns_empty()
    {
        var tiers = new[] { Tier(1, 5_000m), Tier(2, 50_000m) };

        var required = ApprovalTierEvaluator.RequiredTiers(1_000m, tiers);

        required.Should().BeEmpty();
    }
}
