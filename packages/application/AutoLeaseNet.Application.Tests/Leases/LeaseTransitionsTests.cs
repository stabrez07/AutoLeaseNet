using AutoLeaseNet.Domain.Leases;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Leases;

/// <summary>
/// A1 — Lease aggregate state-transition invariants. The webhook + saga are the only
/// legitimate writers in production, but the rules they enforce live on the entity.
/// </summary>
public sealed class LeaseTransitionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 24, 9, 0, 0, TimeSpan.Zero);

    private static Lease NewPending() => Lease.CreatePending(new CreatePendingInput
    {
        TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        TajeerContractNumber = 12345,
        IssuanceUrl = "https://x/y",
        ContractTypeCode = 1,
        ContractStartUtc = T0,
        ContractEndUtc = T0.AddDays(2),
        RentAmount = 200m,
        PaymentMethodCode = 1,
        NowUtc = T0,
    });

    [Fact]
    public void CreatePending_sets_PendingIssuance_and_SavedAtUtc()
    {
        var lease = NewPending();

        lease.Status.Should().Be(LeaseStatus.PendingIssuance);
        lease.SavedAtUtc.Should().Be(T0);
        lease.IssuedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkIssued_advances_to_Active_and_captures_issuance_snapshot()
    {
        var lease = NewPending();

        lease.MarkIssued(startKm: 42_000, startFuelLevelCode: 4, conditionNotes: "Clean", T0.AddMinutes(15));

        lease.Status.Should().Be(LeaseStatus.Active);
        lease.IssuedAtUtc.Should().Be(T0.AddMinutes(15));
        lease.StartKm.Should().Be(42_000);
        lease.StartFuelLevelCode.Should().Be(4);
        lease.IssuanceConditionNotes.Should().Be("Clean");
    }

    [Fact]
    public void MarkIssued_is_idempotent_on_already_Active()
    {
        var lease = NewPending();
        lease.MarkIssued(1, 4, null, T0.AddMinutes(5));
        var firstIssued = lease.IssuedAtUtc;

        lease.MarkIssued(2, 5, "ignored", T0.AddHours(1));

        lease.Status.Should().Be(LeaseStatus.Active);
        lease.IssuedAtUtc.Should().Be(firstIssued, because: "re-entry is a no-op replay defence");
        lease.StartKm.Should().Be(1, because: "first call wins; replays must not overwrite the snapshot");
    }

    [Fact]
    public void IncrementExtension_moves_Active_to_Extended_and_pushes_end_date()
    {
        var lease = NewPending();
        lease.MarkIssued(0, 4, null, T0.AddMinutes(1));

        lease.IncrementExtension(newEndUtc: T0.AddDays(5), nowUtc: T0.AddDays(1));

        lease.Status.Should().Be(LeaseStatus.Extended);
        lease.ContractEndUtc.Should().Be(T0.AddDays(5));
        lease.ExtensionCount.Should().Be(1);
    }

    [Fact]
    public void MarkSuspended_then_MarkResumed_restores_Active_when_no_extension()
    {
        var lease = NewPending();
        lease.MarkIssued(0, 4, null, T0.AddMinutes(1));

        lease.MarkSuspended(suspensionReasonCode: 2, T0.AddHours(1));
        lease.Status.Should().Be(LeaseStatus.Suspended);
        lease.SuspensionReasonCode.Should().Be(2);

        lease.MarkResumed(T0.AddHours(2));
        lease.Status.Should().Be(LeaseStatus.Active);
        lease.SuspensionReasonCode.Should().BeNull();
    }

    [Fact]
    public void MarkSuspended_then_MarkResumed_restores_Extended_when_extension_count_positive()
    {
        var lease = NewPending();
        lease.MarkIssued(0, 4, null, T0.AddMinutes(1));
        lease.IncrementExtension(T0.AddDays(5), T0.AddDays(1));

        lease.MarkSuspended(1, T0.AddDays(1).AddHours(1));
        lease.MarkResumed(T0.AddDays(1).AddHours(2));

        lease.Status.Should().Be(LeaseStatus.Extended);
    }

    [Fact]
    public void MarkClosed_captures_return_snapshot_and_actual_return()
    {
        var lease = NewPending();
        lease.MarkIssued(50_000, 4, null, T0.AddMinutes(1));

        lease.MarkClosed(
            closureMainReasonCode: 1,
            closureSubReasonCode: null,
            endKm: 50_300,
            returnFuelLevelCode: 3,
            returnConditionNotes: "Front bumper scratch",
            damagesObserved: "Minor scratch passenger side",
            nowUtc: T0.AddDays(2));

        lease.Status.Should().Be(LeaseStatus.Closed);
        lease.EndKm.Should().Be(50_300);
        lease.ReturnFuelLevelCode.Should().Be(3);
        lease.DamagesObserved.Should().Be("Minor scratch passenger side");
        lease.ActualReturnUtc.Should().Be(T0.AddDays(2));
        lease.ClosureMainReasonCode.Should().Be(1);
    }

    [Fact]
    public void MarkCancelled_only_works_from_PendingIssuance()
    {
        var lease = NewPending();

        lease.MarkCancelled("Renter changed plans", T0.AddHours(1));

        lease.Status.Should().Be(LeaseStatus.Cancelled);
        lease.CancellationReason.Should().Be("Renter changed plans");
        lease.CancelledAtUtc.Should().Be(T0.AddHours(1));
    }

    [Fact]
    public void MarkCancelled_after_Issued_throws()
    {
        var lease = NewPending();
        lease.MarkIssued(0, 4, null, T0.AddMinutes(1));

        var act = () => lease.MarkCancelled("nope", T0.AddHours(1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cancel*Active*");
    }

    [Fact]
    public void MarkExpired_only_works_from_PendingIssuance()
    {
        var lease = NewPending();

        lease.MarkExpired(T0.AddHours(13));

        lease.Status.Should().Be(LeaseStatus.ExpiredDraft);
        lease.ExpiredAtUtc.Should().Be(T0.AddHours(13));
    }

    [Fact]
    public void RecordSaveFailure_sets_SaveFailed_and_persists_vendor_errorKey()
    {
        var lease = NewPending();

        lease.RecordSaveFailure("server.error.renter.mobile.invalid", T0.AddSeconds(2));

        lease.Status.Should().Be(LeaseStatus.SaveFailed);
        lease.SaveFailureReason.Should().Be("server.error.renter.mobile.invalid");
    }
}
