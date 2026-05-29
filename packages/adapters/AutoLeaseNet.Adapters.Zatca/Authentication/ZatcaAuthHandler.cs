using AutoLeaseNet.Adapters.Zatca.Configuration;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Zatca.Authentication;

/// <summary>
/// Delegating handler that attaches the Fatoorah <c>Authorization</c> header to every
/// outbound request from the named <c>"zatca"</c> <see cref="HttpClient"/>. Mirrors the
/// Tajeer auth-handler pattern.
///
/// <para>
/// Phase-1 reads the token from <see cref="ZatcaOptions.AuthorizationToken"/> as-is
/// (already prefixed with the scheme — typically <c>"Bearer …"</c> or <c>"Basic …"</c>).
/// Phase-2 moves credential resolution to <c>ICredentialProvider</c> keyed by TenantId,
/// matching the same migration path the Tajeer adapter will take.
/// </para>
/// </summary>
public sealed class ZatcaAuthHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<ZatcaOptions> _options;

    public ZatcaAuthHandler(IOptionsMonitor<ZatcaOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = _options.CurrentValue.AuthorizationToken;
        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization is null)
        {
            // Token already carries the scheme prefix (Spec 02 §4.5 / Fatoorah portal).
            // Parse so HttpClient surfaces auth correctly through the resilience pipeline.
            request.Headers.TryAddWithoutValidation("Authorization", token);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
