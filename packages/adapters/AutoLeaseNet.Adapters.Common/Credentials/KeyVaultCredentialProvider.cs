using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;

namespace AutoLeaseNet.Adapters.Common.Credentials;

/// <summary>
/// Resolves per-tenant secrets from Azure Key Vault with 1h in-process cache.
/// Per Spec 03 §4.2 and Spec 04 §6.3 — every adapter that needs per-tenant credentials uses this
/// in production. For dev / tests, swap in <see cref="DevEnvironmentCredentialProvider"/>.
///
/// Production wiring deferred until the Azure landing zone is provisioned (see Plan 05).
/// </summary>
public sealed class KeyVaultCredentialProvider(Uri keyVaultUri, IMemoryCache cache) : ICredentialProvider
{
    private readonly SecretClient _secretClient = new(keyVaultUri, new DefaultAzureCredential());
    private readonly IMemoryCache _cache = cache;

    public async Task<string?> GetAsync(string secretName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        return await _cache.GetOrCreateAsync($"kv:{secretName}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: ct);
            return secret.Value.Value;
        });
    }

    public Task<string?> GetTenantSecretAsync(
        string adapter,
        Guid tenantId,
        string secretKey,
        CancellationToken ct = default)
        => GetAsync($"{adapter}-{tenantId}-{secretKey}", ct);
}
