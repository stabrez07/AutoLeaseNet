using System.ComponentModel.DataAnnotations;

namespace AutoLeaseNet.Adapters.Zatca.Configuration;

/// <summary>
/// Configuration bound from <c>Zatca:*</c> section. Per Spec 02 §4.5 / Spec 07.
///
/// <para>
/// Phase-1 scope (this PR) wires the shape only — the Real client stub returns
/// <c>zatca.not_yet_implemented</c>. Week-4 swaps that for UBL 2.1 + ECDSA + TLV-QR.
/// Default <see cref="Mode"/> intentionally stays <see cref="ZatcaMode.Real"/> so a
/// production deployment can't silently no-op against the InMemory fake — the explicit
/// <c>not_yet_implemented</c> failure makes "I forgot to flip Mode to InMemory in dev"
/// loudly visible.
/// </para>
/// </summary>
public sealed class ZatcaOptions
{
    public const string SectionName = "Zatca";

    /// <summary>
    /// Fatoorah gateway base URL. Sandbox / Production differ; defaulted per the
    /// CSID issued for the project (Spec 02 §4.5).
    /// </summary>
    [Required, Url]
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Sandbox vs Production. Phase-1 stays on Sandbox until UAT signoff per
    /// CLAUDE.md "What NOT to do without asking the user".
    /// </summary>
    public ZatcaEnvironment Environment { get; init; } = ZatcaEnvironment.Sandbox;

    /// <summary>
    /// Bearer / Basic token issued by the Fatoorah developer portal. Phase-2 moves this
    /// to <c>ICredentialProvider</c> keyed by TenantId; for now lives here as a plain
    /// string so the option binding can ValidateOnStart.
    /// </summary>
    [Required]
    public required string AuthorizationToken { get; init; }

    /// <summary>HTTP request timeout in seconds. Default 30s.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 30;

    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Selects which <see cref="IZatcaClient"/> implementation is bound at composition
    /// time. <see cref="ZatcaMode.InMemory"/> wires the deterministic fake; <see cref="ZatcaMode.Real"/>
    /// wires the HTTP-backed client (which currently returns a clear-error stub — see class summary).
    /// </summary>
    public ZatcaMode Mode { get; init; } = ZatcaMode.Real;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(TimeoutSeconds);
}

/// <summary>Pattern B mode switch — every adapter ships a sibling InMemory for tests / offline dev.</summary>
public enum ZatcaMode
{
    /// <summary>Real HTTP client talking to Fatoorah (currently stubbed; lights up in Week-4).</summary>
    Real = 0,

    /// <summary>Deterministic in-memory fake — no network calls.</summary>
    InMemory = 1,
}

/// <summary>Fatoorah environment selector. Maps to a different BaseUrl + CSID material.</summary>
public enum ZatcaEnvironment
{
    Sandbox = 0,
    Production = 1,
}

/// <summary>
/// Adapter-level outcome of a clearance submission. Mirrors the four states the Fatoorah
/// gateway returns on a synchronous Submit response — the broader saga-level state
/// machine (PROCESSING / NETWORK_ERROR / DEAD_LETTER per Spec 02 §4.5) is the
/// <c>ZatcaSubmission</c> aggregate's responsibility, not the adapter's.
/// </summary>
public enum ZatcaResultStatus
{
    /// <summary>2xx + cleared. The chain may advance.</summary>
    Cleared = 0,

    /// <summary>2xx + reported (B2C simplified path). The chain may advance.</summary>
    Reported = 1,

    /// <summary>2xx + cleared with vendor warnings. The chain may advance; warnings surfaced to operators.</summary>
    WarningCleared = 2,

    /// <summary>4xx-style validation failure. Chain MUST NOT advance (CLAUDE.md §6).</summary>
    Rejected = 3,
}
