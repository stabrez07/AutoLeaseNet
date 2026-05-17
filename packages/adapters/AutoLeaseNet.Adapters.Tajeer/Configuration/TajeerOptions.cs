namespace AutoLeaseNet.Adapters.Tajeer.Configuration;

/// <summary>
/// Configuration bound from "Tajeer" section of appsettings + Key Vault references.
/// Per doc 03 §4.1.
/// </summary>
public sealed class TajeerOptions
{
    public const string SectionName = "Tajeer";

    /// <summary>Staging: https://tajeer-stg.api.elm.sa, Prod: https://tajeer.api.elm.sa</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Staging: https://tajeerstg.logisti.sa, Prod: https://tajeer.logisti.sa</summary>
    public required string IssuanceUrlBase { get; init; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Shared secret used to verify inbound webhook signatures.</summary>
    public required string WebhookSharedSecret { get; init; }

    public bool IsEnabled { get; init; } = true;
    public bool IsSandbox { get; init; }
}
