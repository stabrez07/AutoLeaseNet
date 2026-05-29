using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Tajeer;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Tajeer;

/// <summary>
/// Spec 03 §7.2 — the canonical Tajeer status-code mapper. Exhaustively pins the
/// cases the spec defines plus the local-only Extended refinement. Inline switches
/// anywhere else in the codebase are a bug (Spec 03 §1 principle #10), so every
/// future consumer routes through these cases.
/// </summary>
public sealed class TajeerStatusMapperTests
{
    [Theory]
    [InlineData(1, null, null, LeaseStatus.PendingIssuance)]   // Tajeer "Saved" → local PendingIssuance
    [InlineData(4, null, null, LeaseStatus.Active)]            // Tajeer "Issued" → local Active
    [InlineData(3, null, null, LeaseStatus.Suspended)]         // permissive: Suspended w/o reason
    [InlineData(3, 1, null, LeaseStatus.Suspended)]            // Suspended + NonTrafficAccident
    [InlineData(3, 2, null, LeaseStatus.Suspended)]            // Suspended + FinancialClaims
    [InlineData(2, null, null, LeaseStatus.Closed)]            // permissive: Closed w/o reasons
    [InlineData(2, null, 1, LeaseStatus.Closed)]               // Closed + ContractPeriodExpiration
    [InlineData(2, null, 444, LeaseStatus.Closed)]             // Closed + ClosureDueToDamage
    [InlineData(5, null, null, LeaseStatus.Cancelled)]         // Tajeer Cancelled
    [InlineData(5, null, 10, LeaseStatus.Cancelled)]           // Cancelled + any sub-reason still maps
    public void FromTajeer_maps_every_documented_case(
        int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode, LeaseStatus expected)
    {
        TajeerStatusMapper.FromTajeer(contractStatusCode, suspensionReasonCode, closureReasonCode)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(0, null, null)]   // unknown status
    [InlineData(99, null, null)]  // out-of-range status
    [InlineData(1, 1, null)]      // Saved must not carry suspension
    [InlineData(4, null, 1)]      // Active must not carry closure
    [InlineData(1, null, 1)]      // Saved must not carry closure
    public void FromTajeer_throws_on_unrecognised_triple(
        int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode)
    {
        var act = () => TajeerStatusMapper.FromTajeer(contractStatusCode, suspensionReasonCode, closureReasonCode);
        act.Should().Throw<InvalidTajeerStatusException>()
            .Which.ContractStatusCode.Should().Be(contractStatusCode);
    }

    [Fact]
    public void ApplyLocalRefinements_promotes_Active_to_Extended_when_local_extension_count_is_positive()
    {
        TajeerStatusMapper.ApplyLocalRefinements(LeaseStatus.Active, localExtensionCount: 1)
            .Should().Be(LeaseStatus.Extended);
        TajeerStatusMapper.ApplyLocalRefinements(LeaseStatus.Active, localExtensionCount: 5)
            .Should().Be(LeaseStatus.Extended);
    }

    [Fact]
    public void ApplyLocalRefinements_leaves_Active_alone_when_no_extensions()
    {
        TajeerStatusMapper.ApplyLocalRefinements(LeaseStatus.Active, localExtensionCount: 0)
            .Should().Be(LeaseStatus.Active);
    }

    [Theory]
    [InlineData(LeaseStatus.PendingIssuance)]
    [InlineData(LeaseStatus.Suspended)]
    [InlineData(LeaseStatus.Closed)]
    [InlineData(LeaseStatus.Cancelled)]
    public void ApplyLocalRefinements_is_identity_for_non_Active_statuses(LeaseStatus status)
    {
        // Extension count is irrelevant for any status other than Active — the local refinement
        // only ever promotes Active → Extended, never demotes or re-routes anything else.
        TajeerStatusMapper.ApplyLocalRefinements(status, localExtensionCount: 7)
            .Should().Be(status);
    }
}
