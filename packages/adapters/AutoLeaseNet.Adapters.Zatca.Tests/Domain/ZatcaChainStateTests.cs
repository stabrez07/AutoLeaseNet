using AutoLeaseNet.Domain.Zatca;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Zatca.Tests.Domain;

/// <summary>
/// Pins the <see cref="ZatcaChainState"/> behavioural contract. The cross-aggregate
/// invariant — "only advance on a CLEARED / REPORTED / WARNING_CLEARED result" — is the
/// saga's job (Spec 02 §6.6); these tests prove the entity itself behaves predictably
/// for any caller that does invoke <c>AdvanceTo</c>.
/// </summary>
public sealed class ZatcaChainStateTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset T0 = new(2026, 5, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForNewTenant_starts_with_no_cleared_hash()
    {
        var state = ZatcaChainState.ForNewTenant(TenantId, T0);

        state.TenantId.Should().Be(TenantId);
        state.LastClearedInvoiceHash.Should().BeNull();
        state.LastClearedAtUtc.Should().BeNull();
        state.CreatedAtUtc.Should().Be(T0);
        state.UpdatedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void AdvanceTo_sets_hash_and_timestamp_and_updates_audit()
    {
        var state = ZatcaChainState.ForNewTenant(TenantId, T0);
        var advanceAt = T0.AddMinutes(5);

        state.AdvanceTo("hash-A", advanceAt);

        state.LastClearedInvoiceHash.Should().Be("hash-A");
        state.LastClearedAtUtc.Should().Be(advanceAt);
        state.UpdatedAtUtc.Should().Be(advanceAt);
    }

    [Fact]
    public void AdvanceTo_called_twice_keeps_the_latest_only()
    {
        var state = ZatcaChainState.ForNewTenant(TenantId, T0);
        state.AdvanceTo("hash-A", T0.AddMinutes(5));

        state.AdvanceTo("hash-B", T0.AddMinutes(7));

        state.LastClearedInvoiceHash.Should().Be("hash-B");
        state.LastClearedAtUtc.Should().Be(T0.AddMinutes(7));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AdvanceTo_rejects_empty_hash(string? blank)
    {
        var state = ZatcaChainState.ForNewTenant(TenantId, T0);

        Action act = () => state.AdvanceTo(blank!, T0.AddMinutes(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reset_clears_chain_back_to_initial_state()
    {
        var state = ZatcaChainState.ForNewTenant(TenantId, T0);
        state.AdvanceTo("hash-A", T0.AddMinutes(5));

        state.Reset(T0.AddMinutes(10));

        state.LastClearedInvoiceHash.Should().BeNull();
        state.LastClearedAtUtc.Should().BeNull();
        state.UpdatedAtUtc.Should().Be(T0.AddMinutes(10));
    }
}
