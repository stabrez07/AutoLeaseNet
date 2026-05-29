namespace AutoLeaseNet.Infrastructure.Outbox;

/// <summary>
/// Configuration bound from the <c>Outbox</c> section of <c>appsettings.json</c>.
/// Defaults are tuned for single-instance Phase-1 deployment; multi-instance Phase-2
/// will need a distributed lock to avoid double-publish (out of scope for this workstream).
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// Master switch. Defaults true. Tests typically set this false so endpoint
    /// fixtures don't have a background drain racing them.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between drain polls when the previous batch was empty.</summary>
    public int DrainIntervalSeconds { get; set; } = 5;

    /// <summary>Rows to pull per poll. Capped to keep one cycle bounded.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Stop touching a row once <see cref="Domain.Outbox.OutboxEvent.Attempts"/>
    /// reaches this. Operator can manually requeue by clearing the field.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;
}
