using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Webhooks;

/// <summary>
/// Body shape Tajeer POSTs to our registered webhook URL (Spec 03 §12.1).
/// <para>
/// Example payload:
/// <code>
/// {
///   "id": "notif_982374",
///   "timestamp": "2025-10-06T10:30:00",
///   "category": "contract",
///   "type": "contract.create",
///   "referenceId": "2569450000400015",
///   "message": "Contract 2569450000400015 is created."
/// }
/// </code>
/// </para>
/// </summary>
public sealed record TajeerWebhookPayload(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("timestamp")] string? Timestamp,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("referenceId")] string? ReferenceId,
    [property: JsonPropertyName("message")] string? Message);

/// <summary>
/// Tajeer event types we care about in Phase 1. Tajeer fires multiple variants of the
/// "issuance succeeded" event depending on contract sub-flow; we treat all of these as
/// the trigger for <c>Lease.MarkIssued</c>. Compare with case-insensitive equality.
/// </summary>
public static class TajeerWebhookEventTypes
{
    public const string ContractCreate = "contract.create";
    public const string ContractIssued = "contract.issued";
    public const string ContractIssue = "contract.issue";

    public static bool IsIssuanceCompletion(string? eventType) =>
        eventType is not null &&
        (string.Equals(eventType, ContractCreate, StringComparison.OrdinalIgnoreCase)
         || string.Equals(eventType, ContractIssued, StringComparison.OrdinalIgnoreCase)
         || string.Equals(eventType, ContractIssue, StringComparison.OrdinalIgnoreCase));
}
