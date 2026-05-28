using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.6 — <c>CloseContract</c> response. The <c>contractStatusCode</c> is
/// mapped to <see cref="AutoLeaseNet.Domain.Leases.LeaseStatus"/> by
/// <c>TajeerStatusMapper.FromTajeer</c> (Spec 03 §6.4).
/// </summary>
public sealed record CloseContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>Tajeer status code post-close. 2 = Closed (the only success value).</summary>
    [JsonPropertyName("contractStatusCode")] public required int ContractStatusCode { get; init; }

    /// <summary>Vendor-stamped close moment (UTC, Tajeer format).</summary>
    [JsonPropertyName("closedAt")] public string? ClosedAt { get; init; }

    /// <summary>Vendor-confirmed final paid amount (echo of request, or server-corrected).</summary>
    [JsonPropertyName("finalPaidAmount")] public decimal FinalPaidAmount { get; init; }
}
