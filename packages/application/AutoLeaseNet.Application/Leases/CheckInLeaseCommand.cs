using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Day-19 check-in saga (local slice). Ops returns the vehicle, records the CHECK_IN
/// inspection, closes the Lease, and frees the Vehicle — all in one transactional
/// command. Tajeer <c>CalculateContractPayment</c> + <c>Close Contract</c> calls are
/// deferred to the saga's vendor-commit step (next workstream).
/// </summary>
public sealed record CheckInLeaseCommand : IRequest<CheckInLeaseCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid LeaseId { get; init; }

    // ── CHECK_IN inspection fields (mirror StartInspectionInput) ─────────────
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

    public string? Notes { get; init; }
    public string? SketchInfoJson { get; init; }
    public string? DamagesObserved { get; init; }
    public string? ReturnConditionNotes { get; init; }

    // ── Closure ───────────────────────────────────────────────────────────────
    /// <summary>Tajeer closure main reason code (Spec 03 §7.3).</summary>
    public required int ClosureMainReasonCode { get; init; }
    public int? ClosureSubReasonCode { get; init; }
}

public sealed record CheckInLeaseCommandResult(
    bool Success,
    Guid? LeaseId,
    Guid? InspectionId,
    string? LeaseStatus,
    string? ErrorCode,
    string? ErrorMessage);
