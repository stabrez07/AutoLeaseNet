using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Day-19 check-in saga. Ops returns the vehicle, the BFF calls Tajeer's
/// <c>CalculateContractPayment</c> for the money preview, then <c>CloseContract</c> for
/// the vendor commit, then mirrors the result locally (Lease → Closed, Vehicle →
/// Available, CHECK_IN inspection completed). See
/// <see href="../../../../Plans/workstreams/2026-05-28-tajeer-close-saga/plan.md">workstream plan</see>.
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

    /// <summary>Caller-declared extra-km overage; null = let Tajeer compute from contract allowance.</summary>
    public int? ExtraKm { get; init; }

    /// <summary>Caller-declared additional charges (damages, refuelling, cleaning, etc.).</summary>
    public decimal? AdditionalCharges { get; init; }

    /// <summary>Discount applied at close (server-validated by Tajeer).</summary>
    public decimal? DiscountAmount { get; init; }

    /// <summary>What ops collected at the counter — passed to Tajeer's CloseContract as <c>finalPaidAmount</c>.</summary>
    public decimal? FinalPaidAmount { get; init; }
}

public sealed record CheckInLeaseCommandResult(
    bool Success,
    Guid? LeaseId,
    Guid? InspectionId,
    string? LeaseStatus,
    string? ErrorCode,
    string? ErrorMessage,
    CheckInPaymentBreakdown? Payment = null);

/// <summary>
/// Internal projection of Tajeer's <c>CalculatePayment</c> + <c>CloseContract</c> responses
/// — surfaced to the BFF so the ops UI can show the breakdown next to the close
/// confirmation. All amounts in SAR.
/// </summary>
public sealed record CheckInPaymentBreakdown(
    decimal RentAmount,
    decimal PaidAmount,
    decimal LateHoursFee,
    decimal ExtraKmFee,
    decimal DamagesFee,
    decimal DiscountAmount,
    decimal TotalDue,
    decimal VatAmount,
    decimal GrandTotal,
    decimal FinalPaidAmount);
