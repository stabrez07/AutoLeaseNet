using System.ComponentModel.DataAnnotations;

namespace AutoLeaseNet.Adapters.Tajeer.Configuration;

/// <summary>
/// Configuration bound from "Tajeer" section of appsettings + Key Vault references.
/// Per Spec 03 §4.1.
///
/// Phase 1: AppId / AppKey / AuthorizationToken live here as plain strings (single-tenant
/// dev/staging). Phase 2 multi-tenant: secrets move to <c>ICredentialProvider</c> keyed by
/// TenantId; only <c>BaseUrl</c> / <c>BranchId</c> / <c>TimeoutSeconds</c> stay here.
/// </summary>
public sealed class TajeerOptions
{
    public const string SectionName = "Tajeer";

    /// <summary>Staging: https://tajeer-stg.api.elm.sa, Prod: https://tajeer.api.elm.sa</summary>
    [Required, Url]
    public required string BaseUrl { get; init; }

    /// <summary>Staging: https://tajeerstg.logisti.sa, Prod: https://tajeer.logisti.sa</summary>
    [Required, Url]
    public required string IssuanceUrlBase { get; init; }

    /// <summary>App-id header value issued by the Rabet portal.</summary>
    [Required]
    public required string AppId { get; init; }

    /// <summary>App-key header value issued by the Rabet portal.</summary>
    [Required]
    public required string AppKey { get; init; }

    /// <summary>
    /// Authorization header value generated via Tajeer portal → Users → API Registration.
    /// Already includes the scheme prefix (typically <c>"Basic …"</c>).
    /// </summary>
    [Required]
    public required string AuthorizationToken { get; init; }

    /// <summary>Default working/receive branch id used when a caller doesn't supply one.</summary>
    [Range(1, int.MaxValue)]
    public int BranchId { get; init; }

    /// <summary>HTTP request timeout in seconds. Default 30s matches Spec 03 §4.1.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Shared secret used to verify inbound webhook signatures.</summary>
    [Required]
    public required string WebhookSharedSecret { get; init; }

    public bool IsEnabled { get; init; } = true;
    public bool IsSandbox { get; init; }

    /// <summary>
    /// Adapter implementation to register for sub-clients like
    /// <see cref="Contracts.ITajeerContractClient"/>. <see cref="TajeerMode.Real"/> wires
    /// the HTTP-backed client; <see cref="TajeerMode.InMemory"/> wires the in-memory fake.
    /// Defaults to <see cref="TajeerMode.Real"/> so Production stays safe-by-default.
    /// </summary>
    public TajeerMode Mode { get; init; } = TajeerMode.Real;

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(TimeoutSeconds);
}

/// <summary>
/// Selects which <see cref="Contracts.ITajeerContractClient"/> implementation is bound at
/// composition time. See Spec 04 §8 — every Pattern B adapter ships a sibling InMemory
/// for tests and offline dev.
/// </summary>
public enum TajeerMode
{
    /// <summary>Real HTTP client talking to Tajeer Rabet (staging or prod).</summary>
    Real = 0,

    /// <summary>Deterministic in-memory fake — no network calls.</summary>
    InMemory = 1,
}
