namespace AutoLeaseNet.Adapters.Tajeer.Configuration;

/// <summary>
/// Tunables for the Tajeer webhook receiver. Bound from <c>Tajeer:Webhook</c>.
///
/// <para>
/// <see cref="LogOnly"/> defaults to <c>true</c> for the first staging round-trip per
/// Day 6 T6.4: invalid signatures log a warning but still ACK 200 + persist the row.
/// Once the first real Tajeer webhook is observed with a valid signature (T6.8), flip
/// to <c>false</c> so signature failures reject (401).
/// </para>
///
/// <para>
/// <see cref="ExpectedSource"/> is the constant we stamp on every persisted
/// <c>WebhookLog.Source</c> column — keeps dedup unique-index keys consistent regardless
/// of which sub-endpoint receives.
/// </para>
/// </summary>
public sealed class TajeerWebhookOptions
{
    public const string SectionName = "Tajeer:Webhook";

    /// <summary>
    /// When <c>true</c> the receiver accepts requests even with an invalid / missing
    /// secret-key header (signature failure logged + persisted with
    /// <c>WebhookLog.SignatureValid = false</c>). Default <c>true</c> until the first
    /// real webhook is observed against staging.
    /// </summary>
    public bool LogOnly { get; init; } = true;

    /// <summary>Vendor tag persisted on <c>WebhookLog.Source</c>.</summary>
    public string ExpectedSource { get; init; } = "TAJEER";
}
