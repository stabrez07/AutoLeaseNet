using AutoLeaseNet.Domain.Operations;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Operations;

/// <summary>
/// Inspection aggregate state-machine invariants per Spec 02 §4.6 and the field /
/// canvas-bounds contract per Spec 01 §5.6. The aggregate must:
///   - start in IN_PROGRESS;
///   - allow photos + damage markers only while IN_PROGRESS;
///   - validate marker coords against Tajeer's 893 × 429 canvas;
///   - raise InspectionCompletedDomainEvent exactly once on Complete;
///   - be idempotent on same-state re-entry (defends replays);
///   - reject illegal transitions (Complete from Abandoned, Abandon from Completed).
/// </summary>
public sealed class InspectionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly Guid VehicleId = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000010");
    private static readonly Guid LeaseId = Guid.Parse("c3c3c3c3-0000-0000-0000-000000000020");
    private static readonly Guid PerformedByUserId = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");

    /// <summary>Default helper leaves <c>LeaseId</c> null so LinkToLease tests can drive it explicitly.</summary>
    private static Inspection NewInProgress(InspectionType type = InspectionType.CheckOut, Guid? leaseId = null) =>
        Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId,
            VehicleId = VehicleId,
            LeaseId = leaseId,
            Type = type,
            PerformedByUserId = PerformedByUserId,
            OdometerKm = 42_000,
            FuelLevel = FuelLevel.ThreeQuarter,
            NowUtc = T0,
        });

    [Fact]
    public void Start_returns_aggregate_in_IN_PROGRESS_with_captured_metadata()
    {
        var i = NewInProgress(leaseId: LeaseId);

        i.Status.Should().Be(InspectionStatus.InProgress);
        i.Type.Should().Be(InspectionType.CheckOut);
        i.TenantId.Should().Be(TenantId);
        i.VehicleId.Should().Be(VehicleId);
        i.LeaseId.Should().Be(LeaseId);
        i.PerformedByUserId.Should().Be(PerformedByUserId);
        i.PerformedAtUtc.Should().Be(T0);
        i.OdometerKm.Should().Be(42_000);
        i.FuelLevel.Should().Be(FuelLevel.ThreeQuarter);
        i.CompletedAtUtc.Should().BeNull();
        i.AbandonedAtUtc.Should().BeNull();
        i.Photos.Should().BeEmpty();
        i.DamageMarkers.Should().BeEmpty();
        i.DomainEvents.Should().BeEmpty(because: "no event fires until Complete");
    }

    [Fact]
    public void Start_rejects_empty_TenantId_and_VehicleId()
    {
        var bad = () => Inspection.Start(new StartInspectionInput
        {
            TenantId = Guid.Empty,
            VehicleId = VehicleId,
            Type = InspectionType.CheckOut,
            PerformedByUserId = PerformedByUserId,
            OdometerKm = 1,
            FuelLevel = FuelLevel.Full,
            NowUtc = T0,
        });
        bad.Should().Throw<ArgumentException>();

        var bad2 = () => Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId,
            VehicleId = Guid.Empty,
            Type = InspectionType.CheckOut,
            PerformedByUserId = PerformedByUserId,
            OdometerKm = 1,
            FuelLevel = FuelLevel.Full,
            NowUtc = T0,
        });
        bad2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_rejects_negative_odometer()
    {
        var bad = () => Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId,
            VehicleId = VehicleId,
            Type = InspectionType.CheckOut,
            PerformedByUserId = PerformedByUserId,
            OdometerKm = -1,
            FuelLevel = FuelLevel.Full,
            NowUtc = T0,
        });
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddPhoto_in_progress_appends_with_sequence()
    {
        var i = NewInProgress();

        i.AddPhoto("https://blob/abc/photo-1.jpg", sequence: 1, T0.AddSeconds(10));
        i.AddPhoto("https://blob/abc/photo-2.jpg", sequence: 2, T0.AddSeconds(20));

        i.Photos.Should().HaveCount(2);
        i.Photos.Select(p => p.Sequence).Should().Equal(1, 2);
        i.Photos.Select(p => p.BlobUri).Should().Equal(
            "https://blob/abc/photo-1.jpg",
            "https://blob/abc/photo-2.jpg");
    }

    [Fact]
    public void AddPhoto_rejects_empty_blob_uri()
    {
        var i = NewInProgress();

        var bad = () => i.AddPhoto("", sequence: 1, T0.AddSeconds(10));

        bad.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddPhoto_blocked_after_Complete()
    {
        var i = NewInProgress();
        i.Complete(T0.AddMinutes(5));

        var bad = () => i.AddPhoto("https://blob/x.jpg", sequence: 1, T0.AddMinutes(6));

        bad.Should().Throw<InvalidOperationException>(because: "COMPLETED inspections are immutable");
    }

    [Fact]
    public void AddDamageMarker_in_progress_appends_within_canvas_bounds()
    {
        var i = NewInProgress();

        i.AddDamageMarker(DamageMarkerType.SmallScratch, positionX: 100.5m, positionY: 50.25m, T0.AddSeconds(10));
        i.AddDamageMarker(DamageMarkerType.DeepScratch, positionX: 0m, positionY: 0m, T0.AddSeconds(20));
        i.AddDamageMarker(DamageMarkerType.BendInBody, positionX: 893m, positionY: 429m, T0.AddSeconds(30));

        i.DamageMarkers.Should().HaveCount(3);
        i.DamageMarkers.Select(m => m.Type).Should().Equal(
            DamageMarkerType.SmallScratch, DamageMarkerType.DeepScratch, DamageMarkerType.BendInBody);
    }

    [Theory]
    [InlineData(-0.1, 100)]
    [InlineData(893.1, 100)]
    [InlineData(100, -0.1)]
    [InlineData(100, 429.1)]
    public void AddDamageMarker_rejects_coords_outside_Tajeer_canvas(double x, double y)
    {
        var i = NewInProgress();

        var bad = () => i.AddDamageMarker(DamageMarkerType.SmallScratch, (decimal)x, (decimal)y, T0.AddSeconds(10));

        bad.Should().Throw<ArgumentOutOfRangeException>(because: "Tajeer canvas is 893 × 429 — markers outside corrupt the sketch");
    }

    [Fact]
    public void Complete_advances_to_COMPLETED_captures_timestamp_and_raises_event()
    {
        var i = NewInProgress();

        i.Complete(T0.AddMinutes(5));

        i.Status.Should().Be(InspectionStatus.Completed);
        i.CompletedAtUtc.Should().Be(T0.AddMinutes(5));
        i.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InspectionCompletedDomainEvent>()
            .Which.InspectionId.Should().Be(i.Id);
    }

    [Fact]
    public void Complete_on_already_COMPLETED_is_idempotent_and_does_not_double_raise()
    {
        var i = NewInProgress();
        i.Complete(T0.AddMinutes(5));
        var firstTs = i.CompletedAtUtc;
        i.ClearDomainEvents();

        i.Complete(T0.AddMinutes(10));

        i.CompletedAtUtc.Should().Be(firstTs, because: "subsequent Complete must not move the timestamp");
        i.DomainEvents.Should().BeEmpty(because: "no event on idempotent re-entry");
    }

    [Fact]
    public void Complete_from_ABANDONED_throws()
    {
        var i = NewInProgress();
        i.Abandon("user cancelled", T0.AddMinutes(2));

        var bad = () => i.Complete(T0.AddMinutes(5));

        bad.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Abandon_captures_reason_and_timestamp()
    {
        var i = NewInProgress();

        i.Abandon("offline-mobile 24h timeout", T0.AddHours(24));

        i.Status.Should().Be(InspectionStatus.Abandoned);
        i.AbandonedAtUtc.Should().Be(T0.AddHours(24));
        i.AbandonedReason.Should().Be("offline-mobile 24h timeout");
    }

    [Fact]
    public void Abandon_on_COMPLETED_throws()
    {
        var i = NewInProgress();
        i.Complete(T0.AddMinutes(5));

        var bad = () => i.Abandon("too late", T0.AddMinutes(10));

        bad.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Abandon_on_ABANDONED_is_idempotent()
    {
        var i = NewInProgress();
        i.Abandon("first", T0.AddMinutes(2));
        var firstTs = i.AbandonedAtUtc;
        var firstReason = i.AbandonedReason;

        i.Abandon("second", T0.AddMinutes(10));

        i.AbandonedAtUtc.Should().Be(firstTs);
        i.AbandonedReason.Should().Be(firstReason);
    }

    // ─── LinkToLease (Day 18 — check-out saga slice) ────────────────────────

    [Fact]
    public void LinkToLease_on_completed_CHECK_OUT_with_no_existing_link_sets_LeaseId_and_audit_timestamp()
    {
        var i = NewInProgress(InspectionType.CheckOut);
        i.Complete(T0.AddMinutes(5));
        var newLeaseId = Guid.Parse("e5e5e5e5-0000-0000-0000-000000000040");

        i.LinkToLease(newLeaseId, T0.AddMinutes(10));

        i.LeaseId.Should().Be(newLeaseId);
        i.LeaseLinkedAtUtc.Should().Be(T0.AddMinutes(10));
    }

    [Fact]
    public void LinkToLease_on_completed_PRE_DELIVERY_works_too()
    {
        var i = NewInProgress(InspectionType.PreDelivery);
        i.Complete(T0.AddMinutes(5));
        var newLeaseId = Guid.Parse("e5e5e5e5-0000-0000-0000-000000000041");

        i.LinkToLease(newLeaseId, T0.AddMinutes(10));

        i.LeaseId.Should().Be(newLeaseId);
    }

    [Fact]
    public void LinkToLease_with_same_LeaseId_is_idempotent_no_op()
    {
        var i = Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId, VehicleId = VehicleId, LeaseId = LeaseId,
            Type = InspectionType.CheckOut, PerformedByUserId = PerformedByUserId,
            OdometerKm = 1, FuelLevel = FuelLevel.Full, NowUtc = T0,
        });
        i.Complete(T0.AddMinutes(5));
        var originallyLinkedAt = i.LeaseLinkedAtUtc;

        i.LinkToLease(LeaseId, T0.AddMinutes(10));

        i.LeaseId.Should().Be(LeaseId);
        i.LeaseLinkedAtUtc.Should().Be(originallyLinkedAt, because: "re-linking to the same Lease must not move the audit timestamp");
    }

    [Fact]
    public void LinkToLease_rejects_when_already_linked_to_a_different_Lease()
    {
        var i = NewInProgress(InspectionType.CheckOut);
        i.Complete(T0.AddMinutes(5));
        var firstLeaseId = Guid.Parse("e5e5e5e5-0000-0000-0000-000000000050");
        var secondLeaseId = Guid.Parse("e5e5e5e5-0000-0000-0000-000000000051");
        i.LinkToLease(firstLeaseId, T0.AddMinutes(10));

        var bad = () => i.LinkToLease(secondLeaseId, T0.AddMinutes(15));

        bad.Should().Throw<InvalidOperationException>(because: "the link is permanent once set");
    }

    [Fact]
    public void LinkToLease_rejects_when_status_is_not_COMPLETED()
    {
        var inProgress = NewInProgress(InspectionType.CheckOut);
        var bad1 = () => inProgress.LinkToLease(Guid.NewGuid(), T0.AddMinutes(10));
        bad1.Should().Throw<InvalidOperationException>();

        var abandoned = NewInProgress(InspectionType.CheckOut);
        abandoned.Abandon("cancelled", T0.AddMinutes(2));
        var bad2 = () => abandoned.LinkToLease(Guid.NewGuid(), T0.AddMinutes(10));
        bad2.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkToLease_rejects_when_Type_is_not_a_check_out_or_check_in_kind()
    {
        var i = NewInProgress(InspectionType.Periodic);
        i.Complete(T0.AddMinutes(5));

        var bad = () => i.LinkToLease(Guid.NewGuid(), T0.AddMinutes(10));

        bad.Should().Throw<InvalidOperationException>(because: "only CheckOut/PreDelivery/CheckIn inspections justify a Lease state transition");
    }

    [Fact]
    public void LinkToLease_accepts_CheckIn_for_Day_19_close_saga()
    {
        var i = NewInProgress(InspectionType.CheckIn);
        i.Complete(T0.AddMinutes(5));
        var leaseId = Guid.Parse("e5e5e5e5-0000-0000-0000-000000000060");

        i.LinkToLease(leaseId, T0.AddMinutes(10));

        i.LeaseId.Should().Be(leaseId);
        i.LeaseLinkedAtUtc.Should().Be(T0.AddMinutes(10));
    }

    [Fact]
    public void LinkToLease_rejects_empty_LeaseId()
    {
        var i = NewInProgress(InspectionType.CheckOut);
        i.Complete(T0.AddMinutes(5));

        var bad = () => i.LinkToLease(Guid.Empty, T0.AddMinutes(10));

        bad.Should().Throw<ArgumentException>();
    }
}
