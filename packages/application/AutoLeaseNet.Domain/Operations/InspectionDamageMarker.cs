using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// One sketch marker on the Tajeer vehicle-canvas (893 × 429 px) per Spec 01 §5.6.
/// Stored denormalized from <c>Inspection.SketchInfoJson</c> so spatial queries (e.g.
/// "damage in front-bumper region") can run without parsing JSON.
/// </summary>
public sealed class InspectionDamageMarker : Entity
{
    public const decimal CanvasWidth = 893m;
    public const decimal CanvasHeight = 429m;

    public Guid InspectionId { get; private set; }
    public DamageMarkerType Type { get; private set; }
    public decimal PositionX { get; private set; }
    public decimal PositionY { get; private set; }

    private InspectionDamageMarker() { }

    internal static InspectionDamageMarker Create(
        Guid inspectionId, Guid tenantId, DamageMarkerType type, decimal positionX, decimal positionY, DateTimeOffset nowUtc)
    {
        if (positionX < 0m || positionX > CanvasWidth)
            throw new ArgumentOutOfRangeException(nameof(positionX), positionX, $"PositionX must be in [0, {CanvasWidth}] (Tajeer canvas width).");
        if (positionY < 0m || positionY > CanvasHeight)
            throw new ArgumentOutOfRangeException(nameof(positionY), positionY, $"PositionY must be in [0, {CanvasHeight}] (Tajeer canvas height).");

        return new InspectionDamageMarker
        {
            InspectionId = inspectionId,
            TenantId = tenantId,
            Type = type,
            PositionX = positionX,
            PositionY = positionY,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
