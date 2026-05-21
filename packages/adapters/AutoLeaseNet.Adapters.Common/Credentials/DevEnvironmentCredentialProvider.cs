using Microsoft.Extensions.Configuration;

namespace AutoLeaseNet.Adapters.Common.Credentials;

/// <summary>
/// Dev-time credential provider backed by <see cref="IConfiguration"/> (appsettings.Development.json,
/// user-secrets, environment variables — whichever the host has chained in).
///
/// Lookup keys:
/// - Global secrets:      <c>Secrets:{name}</c>
/// - Per-tenant secrets:  <c>Tenants:{tenantId}:{adapter}:{secretKey}</c>
///
/// In production, replace this with a Key Vault–backed implementation once the Azure landing
/// zone is provisioned (see Plan 05 dependency checklist).
/// </summary>
public sealed class DevEnvironmentCredentialProvider(IConfiguration configuration) : ICredentialProvider
{
    private readonly IConfiguration _configuration = configuration
        ?? throw new ArgumentNullException(nameof(configuration));

    public Task<string?> GetAsync(string secretName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        return Task.FromResult(_configuration[$"Secrets:{secretName}"]);
    }

    public Task<string?> GetTenantSecretAsync(
        string adapter,
        Guid tenantId,
        string secretKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return Task.FromResult(_configuration[$"Tenants:{tenantId}:{adapter}:{secretKey}"]);
    }
}
