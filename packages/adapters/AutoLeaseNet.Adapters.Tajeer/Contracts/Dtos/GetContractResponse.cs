using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.3 — read-only projection returned by <c>GetContract</c>.
/// Phase-1 ships the lean field set the reconciliation drift detector needs;
/// the richer projection (renter, vehicle, full payment summary) lands when a
/// consumer requires it (YAGNI for status drift).
/// <para>
/// Status mapping → local <c>Lease.Status</c> is done via
/// <see cref="Mappers.TajeerStatusMapper.FromTajeer"/> + the local
/// <c>ApplyLocalRefinements</c> overlay.
/// </para>
/// </summary>
public sealed record GetContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>1=Saved, 2=Closed, 3=Suspended, 4=Issued, 5=Cancelled (Spec 03 §7.1).</summary>
    [JsonPropertyName("contractStatusCode")] public required int ContractStatusCode { get; init; }

    /// <summary>Vendor suspension reason when status=3 (1=NonTrafficAccident, 2=FinancialClaims; Spec 03 §7.4).</summary>
    [JsonPropertyName("suspensionReasonCode")] public int? SuspensionReasonCode { get; init; }

    /// <summary>Main closure reason when status=2 (Spec 03 §7.3 — e.g. 1=Expiry, 2=Early, 444=Damage).</summary>
    [JsonPropertyName("closureReasonCode")] public int? ClosureReasonCode { get; init; }

    /// <summary>Sub closure reason when status=2 (Spec 03 §7.3 — e.g. 4=Agreement, 5=Accident, 10=Replacement).</summary>
    [JsonPropertyName("closureSubReasonCode")] public int? ClosureSubReasonCode { get; init; }

    /// <summary>Vendor-side extension count. Tajeer keeps Issued (4) even when extensions exist; this field disambiguates Active vs Extended for the local mirror.</summary>
    [JsonPropertyName("extensionCount")] public int? ExtensionCount { get; init; }

    /// <summary>Vendor-stamped last-update moment. Opaque string per Tajeer's date format quirks; parsing is the caller's concern.</summary>
    [JsonPropertyName("updatedAt")] public string? UpdatedAt { get; init; }
}
