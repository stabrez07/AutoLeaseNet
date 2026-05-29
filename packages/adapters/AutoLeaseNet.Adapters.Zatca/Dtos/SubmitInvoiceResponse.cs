using AutoLeaseNet.Adapters.Zatca.Configuration;

namespace AutoLeaseNet.Adapters.Zatca.Dtos;

/// <summary>
/// Adapter-level response from a clearance / reporting submission. The richer
/// state machine (PROCESSING / NETWORK_ERROR / DEAD_LETTER per Spec 02 §4.5) is the
/// <c>ZatcaSubmission</c> aggregate's responsibility, not the adapter's — the adapter
/// only surfaces the four synchronous outcomes ZATCA returns on the Submit call.
/// </summary>
/// <param name="Uuid">Echoed application UUID — used to correlate against the original request.</param>
/// <param name="Status">Adapter-level outcome (Cleared / Reported / WarningCleared / Rejected).</param>
/// <param name="ClearedAtUtc">
/// UTC instant ZATCA confirmed clearance. Set for Cleared / Reported / WarningCleared; null for Rejected.
/// </param>
/// <param name="Warnings">
/// Vendor warning codes (non-fatal). Non-empty for WarningCleared; usually empty for
/// straight Cleared. Surfaced for operator review.
/// </param>
public sealed record SubmitInvoiceResponse(
    Guid Uuid,
    ZatcaResultStatus Status,
    DateTimeOffset? ClearedAtUtc,
    IReadOnlyList<string> Warnings);
