using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;

namespace AutoLeaseNet.Adapters.Common.Credentials;

/// <summary>
/// Resolves per-tenant secrets from Azure Key Vault with 1h in-process cache.
/// Per doc 03 §4.2 and doc 04 §6.3 — every adapter that needs per-tenant credentials uses this.
/// </summary>
public sealed class KeyVaultCredentialProvider
{
    private readonly SecretClient _secretClient;
    private readonly IMemoryCache _cache;

    public KeyVaultCredentialProvider(Uri keyVaultUri, IMemoryCache cache)
    {
        _secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());
        _cache = cache;
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync($"kv:{secretName}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: ct);
            return secret.Value.Value;
        }))!;
    }

    public Task<string> GetTenantSecretAsync(string adapter, Guid tenantId, string secretKey, CancellationToken ct)
        => GetSecretAsync($"{adapter}-{tenantId}-{secretKey}", ct);
}
