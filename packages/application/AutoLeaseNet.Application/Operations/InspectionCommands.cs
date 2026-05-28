using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Operations;

/// <summary>
/// Commands for the <see cref="Inspection"/> aggregate. The handlers all share the
/// same repository + tenant resolution, so we keep them co-located in
/// <c>InspectionCommandHandlers.cs</c> rather than scattering one file per command.
/// <para>
/// Idempotency-Key plumbed through every state-changing command for the same reason
/// as SaveContract: a network retry must not double-insert / double-mutate. Start
/// uses it to dedup creation; the others are also gated through the same store so a
/// replay returns the same result envelope without re-executing the domain method
/// (the aggregate methods are themselves idempotent, but skipping the SaveChanges
/// round-trip saves DB load on noisy clients).
/// </para>
/// </summary>
public sealed record StartInspectionCommand : IRequest<InspectionCommandResult>
{
    public required string IdempotencyKey { get; init; }

    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required InspectionType Type { get; init; }
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

    /// <summary>Optional initial photo URIs (BFF accepts pre-computed blob URIs only — upload flow lands later).</summary>
    public IReadOnlyList<string>? InitialPhotos { get; init; }

    /// <summary>Optional initial damage markers, denormalized from the sketch JSON if the client already parsed it.</summary>
    public IReadOnlyList<InitialDamageMarker>? InitialDamageMarkers { get; init; }
}

public sealed record InitialDamageMarker(DamageMarkerType Type, decimal PositionX, decimal PositionY);

public sealed record AddInspectionPhotoCommand(
    string IdempotencyKey,
    Guid InspectionId,
    string BlobUri,
    int Sequence) : IRequest<InspectionCommandResult>;

public sealed record AddDamageMarkerCommand(
    string IdempotencyKey,
    Guid InspectionId,
    DamageMarkerType Type,
    decimal PositionX,
    decimal PositionY) : IRequest<InspectionCommandResult>;

public sealed record CompleteInspectionCommand(
    string IdempotencyKey,
    Guid InspectionId) : IRequest<InspectionCommandResult>;

public sealed record AbandonInspectionCommand(
    string IdempotencyKey,
    Guid InspectionId,
    string Reason) : IRequest<InspectionCommandResult>;

/// <summary>
/// Result envelope shared by every Inspection command. Success carries the aggregate
/// id and current status; failure carries a stable error code (e.g.
/// <c>inspection.not_found</c>, <c>inspection.immutable</c>) for the BFF to translate.
/// </summary>
public sealed record InspectionCommandResult(
    bool Success,
    Guid? InspectionId,
    InspectionStatus? Status,
    string? ErrorCode,
    string? ErrorMessage);
