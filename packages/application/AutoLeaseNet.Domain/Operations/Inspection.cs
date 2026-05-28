using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Inspection aggregate root (E-Check). Carries every BI field from Spec 01 §5.6 —
/// the condition TINYINT columns are deliberately exhaustive so a future operations
/// report doesn't have to retro-fit columns onto historical rows. Lifecycle per
/// Spec 02 §4.6: <see cref="InspectionStatus.InProgress"/> is the only mutable state;
/// once <see cref="InspectionStatus.Completed"/> the aggregate is immutable and
/// satisfies the Lease invariants in Spec 01 §invariant 2/3.
///
/// <para>
/// Photos and damage markers are append-only collections owned by this aggregate.
/// The aggregate enforces canvas-bounds + non-empty-uri at the entry point so the
/// child entities' invariants can't be violated through a back-door.
/// </para>
/// </summary>
public sealed class Inspection : Entity
{
    // ─── References ─────────────────────────────────────────────────────────
    public Guid VehicleId { get; private set; }
    /// <summary>Null for PRE_DELIVERY (no contract exists yet) and PERIODIC (regulatory).</summary>
    public Guid? LeaseId { get; private set; }

    // ─── Classification ─────────────────────────────────────────────────────
    public InspectionType Type { get; private set; }
    public InspectionStatus Status { get; private set; }

    // ─── Who / when ─────────────────────────────────────────────────────────
    public Guid PerformedByUserId { get; private set; }
    public DateTimeOffset PerformedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? AbandonedAtUtc { get; private set; }
    public string? AbandonedReason { get; private set; }
    /// <summary>Audit timestamp for when <see cref="LinkToLease"/> first set <see cref="LeaseId"/>.</summary>
    public DateTimeOffset? LeaseLinkedAtUtc { get; private set; }

    // ─── Mandatory state snapshot ───────────────────────────────────────────
    public int OdometerKm { get; private set; }
    public FuelLevel FuelLevel { get; private set; }

    // ─── Vehicle condition TINYINTs (Tajeer lookup codes; null = not assessed) ─
    public byte? AcCondition { get; private set; }
    public byte? RadioStereoCondition { get; private set; }
    public byte? ScreenCondition { get; private set; }
    public byte? SpeedometerCondition { get; private set; }
    public byte? KeysCondition { get; private set; }
    public byte? CarSeatsCondition { get; private set; }
    public byte? SafetyTriangleCondition { get; private set; }
    public byte? FireExtinguisherCondition { get; private set; }
    public byte? FirstAidKitCondition { get; private set; }
    public byte? SpareTireToolsCondition { get; private set; }
    public byte? TiresCondition { get; private set; }
    public byte? SpareTireCondition { get; private set; }

    // ─── Free-form ──────────────────────────────────────────────────────────
    public string? Other1 { get; private set; }
    public string? Other2 { get; private set; }
    /// <summary>Max 130 chars enforced by Tajeer — trimmed by the EF mapping.</summary>
    public string? Notes { get; private set; }
    /// <summary>Tajeer-format raw sketch JSON (the markers below are denormalized from it).</summary>
    public string? SketchInfoJson { get; private set; }
    public string? RenterSignatureBlobUri { get; private set; }

    // ─── Child collections (append-only while IN_PROGRESS) ──────────────────
    private readonly List<InspectionPhoto> _photos = new();
    public IReadOnlyCollection<InspectionPhoto> Photos => _photos.AsReadOnly();

    private readonly List<InspectionDamageMarker> _damageMarkers = new();
    public IReadOnlyCollection<InspectionDamageMarker> DamageMarkers => _damageMarkers.AsReadOnly();

    private Inspection() { }

    public static Inspection Start(StartInspectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        if (input.VehicleId == Guid.Empty) throw new ArgumentException("VehicleId required.", nameof(input));
        if (input.PerformedByUserId == Guid.Empty) throw new ArgumentException("PerformedByUserId required.", nameof(input));
        ArgumentOutOfRangeException.ThrowIfNegative(input.OdometerKm);

        return new Inspection
        {
            TenantId = input.TenantId,
            VehicleId = input.VehicleId,
            LeaseId = input.LeaseId,
            Type = input.Type,
            Status = InspectionStatus.InProgress,
            PerformedByUserId = input.PerformedByUserId,
            PerformedAtUtc = input.NowUtc,
            OdometerKm = input.OdometerKm,
            FuelLevel = input.FuelLevel,
            AcCondition = input.AcCondition,
            RadioStereoCondition = input.RadioStereoCondition,
            ScreenCondition = input.ScreenCondition,
            SpeedometerCondition = input.SpeedometerCondition,
            KeysCondition = input.KeysCondition,
            CarSeatsCondition = input.CarSeatsCondition,
            SafetyTriangleCondition = input.SafetyTriangleCondition,
            FireExtinguisherCondition = input.FireExtinguisherCondition,
            FirstAidKitCondition = input.FirstAidKitCondition,
            SpareTireToolsCondition = input.SpareTireToolsCondition,
            TiresCondition = input.TiresCondition,
            SpareTireCondition = input.SpareTireCondition,
            Other1 = input.Other1,
            Other2 = input.Other2,
            Notes = input.Notes,
            SketchInfoJson = input.SketchInfoJson,
            RenterSignatureBlobUri = input.RenterSignatureBlobUri,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void AddPhoto(string blobUri, int sequence, DateTimeOffset nowUtc)
    {
        EnsureMutable(nameof(AddPhoto));
        _photos.Add(InspectionPhoto.Create(Id, TenantId, blobUri, sequence, nowUtc));
        UpdatedAtUtc = nowUtc;
    }

    public void AddDamageMarker(DamageMarkerType type, decimal positionX, decimal positionY, DateTimeOffset nowUtc)
    {
        EnsureMutable(nameof(AddDamageMarker));
        _damageMarkers.Add(InspectionDamageMarker.Create(Id, TenantId, type, positionX, positionY, nowUtc));
        UpdatedAtUtc = nowUtc;
    }

    public void Complete(DateTimeOffset nowUtc)
    {
        if (Status == InspectionStatus.Completed) return; // idempotent replay
        if (Status != InspectionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete Inspection {Id} from status {Status}.");

        Status = InspectionStatus.Completed;
        CompletedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        RaiseDomainEvent(new InspectionCompletedDomainEvent(
            InspectionId: Id,
            TenantId: TenantId,
            VehicleId: VehicleId,
            LeaseId: LeaseId,
            Type: Type,
            PerformedAtUtc: PerformedAtUtc,
            CompletedAtUtc: nowUtc));
    }

    /// <summary>
    /// Day-18 check-out saga: link a COMPLETED <see cref="InspectionType.CheckOut"/> or
    /// <see cref="InspectionType.PreDelivery"/> inspection to the Lease it justifies.
    /// Idempotent on re-link to the same Lease; rejects re-link to a different Lease
    /// (the link is permanent once set — that's the invariant the receipt of CHECK_OUT
    /// at issuance time enforces per Spec 01 §invariant 2).
    /// </summary>
    public void LinkToLease(Guid leaseId, DateTimeOffset nowUtc)
    {
        if (leaseId == Guid.Empty)
            throw new ArgumentException("LeaseId required.", nameof(leaseId));
        if (LeaseId == leaseId) return; // idempotent re-entry
        if (LeaseId is not null)
            throw new InvalidOperationException(
                $"Inspection {Id} is already linked to Lease {LeaseId}; cannot re-link to {leaseId}.");
        if (Status != InspectionStatus.Completed)
            throw new InvalidOperationException(
                $"Cannot link Inspection {Id} to a Lease: status is {Status}; only COMPLETED inspections gate Lease.MarkIssued.");
        if (Type != InspectionType.CheckOut && Type != InspectionType.PreDelivery)
            throw new InvalidOperationException(
                $"Cannot link Inspection {Id} to a Lease: Type is {Type}; only CheckOut + PreDelivery qualify.");

        LeaseId = leaseId;
        LeaseLinkedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Abandon(string reason, DateTimeOffset nowUtc)
    {
        if (Status == InspectionStatus.Abandoned) return; // idempotent replay
        if (Status != InspectionStatus.InProgress)
            throw new InvalidOperationException($"Cannot abandon Inspection {Id} from status {Status}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = InspectionStatus.Abandoned;
        AbandonedAtUtc = nowUtc;
        AbandonedReason = reason;
        UpdatedAtUtc = nowUtc;
    }

    private void EnsureMutable(string operation)
    {
        if (Status != InspectionStatus.InProgress)
            throw new InvalidOperationException($"Cannot {operation} on Inspection {Id}: status is {Status} (only InProgress is mutable).");
    }
}

/// <summary>
/// Constructor input for <see cref="Inspection.Start"/>. Required fields gate the
/// happy path; all condition TINYINTs default to null so a sparse PRE_DELIVERY
/// inspection (no driver, no full sketch) is legal at the domain layer.
/// </summary>
public sealed record StartInspectionInput
{
    public required Guid TenantId { get; init; }
    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required InspectionType Type { get; init; }
    public required Guid PerformedByUserId { get; init; }
    public required int OdometerKm { get; init; }
    public required FuelLevel FuelLevel { get; init; }

    public byte? AcCondition { get; init; }
    public byte? RadioStereoCondition { get; init; }
    public byte? ScreenCondition { get; init; }
    public byte? SpeedometerCondition { get; init; }
    public byte? KeysCondition { get; init; }
    public byte? CarSeatsCondition { get; init; }
    public byte? SafetyTriangleCondition { get; init; }
    public byte? FireExtinguisherCondition { get; init; }
    public byte? FirstAidKitCondition { get; init; }
    public byte? SpareTireToolsCondition { get; init; }
    public byte? TiresCondition { get; init; }
    public byte? SpareTireCondition { get; init; }

    public string? Other1 { get; init; }
    public string? Other2 { get; init; }
    public string? Notes { get; init; }
    public string? SketchInfoJson { get; init; }
    public string? RenterSignatureBlobUri { get; init; }

    public required DateTimeOffset NowUtc { get; init; }
}
