using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Day-20 saga step. Push a Lease's <c>ContractEndUtc</c> forward via Tajeer
/// <c>ExtendContract</c> + local <c>Lease.IncrementExtension</c>. Tajeer-first
/// ordering matches the Day-19 close saga (see
/// <see href="../../../../Plans/workstreams/2026-05-28-day-20-extend-suspend/plan.md">workstream plan</see>).
/// </summary>
public sealed record ExtendLeaseCommand : IRequest<ExtendLeaseCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid LeaseId { get; init; }

    /// <summary>New contract end (UTC). Must be strictly after the current <c>ContractEndUtc</c>.</summary>
    public required DateTimeOffset NewContractEndUtc { get; init; }

    /// <summary>Tajeer extension reason code (optional — defaults applied vendor-side).</summary>
    public int? ExtensionReasonCode { get; init; }

    /// <summary>Caller-declared additional charges (cleaning, top-up, etc.).</summary>
    public decimal? AdditionalCharges { get; init; }

    /// <summary>Payment method for the additional charges (Tajeer payment-method code).</summary>
    public int? PaymentMethodCode { get; init; }
}

public sealed record ExtendLeaseCommandResult(
    bool Success,
    Guid? LeaseId,
    string? LeaseStatus,
    DateTimeOffset? NewContractEndUtc,
    int? ExtensionCount,
    ExtensionChargeBreakdown? Charges,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record ExtensionChargeBreakdown(decimal TotalDue, decimal VatAmount, decimal GrandTotal);
