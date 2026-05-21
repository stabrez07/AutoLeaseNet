namespace AutoLeaseNet.Adapters.Common.Credentials;

/// <summary>
/// Abstraction over secret resolution. Production implementations resolve from Azure Key Vault;
/// development implementations resolve from <c>appsettings.Development.json</c> / user-secrets /
/// environment variables.
///
/// Adapters never read configuration directly for credentials — they take this interface so the
/// resolver can be swapped per environment.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Resolve a global secret by name (e.g. webhook shared key, ZATCA signing cert).
    /// Returns <c>null</c> if not found — callers decide whether that's fatal.
    /// </summary>
    Task<string?> GetAsync(string secretName, CancellationToken ct = default);

    /// <summary>
    /// Resolve a per-tenant secret. Used for vendor credentials that vary per leasing company
    /// (each tenant has its own Tajeer Rabet creds, ZATCA CSID, etc.).
    /// </summary>
    /// <param name="adapter">Adapter short name (e.g. <c>tajeer</c>, <c>zatca</c>).</param>
    /// <param name="tenantId">Tenant (leasing company) identifier.</param>
    /// <param name="secretKey">Secret name within the adapter (e.g. <c>app-key</c>, <c>authorization</c>).</param>
    Task<string?> GetTenantSecretAsync(
        string adapter,
        Guid tenantId,
        string secretKey,
        CancellationToken ct = default);
}
