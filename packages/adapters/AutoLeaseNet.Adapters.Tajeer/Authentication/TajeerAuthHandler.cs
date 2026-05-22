using AutoLeaseNet.Adapters.Tajeer.Configuration;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Adapters.Tajeer.Authentication;

/// <summary>
/// DelegatingHandler that injects the three Tajeer authentication headers (per V9.7 spec
/// "Headers" section) on every outbound request:
/// <list type="bullet">
/// <item><c>App-id</c>           — issued by Rabet portal</item>
/// <item><c>App-key</c>          — issued by Rabet portal</item>
/// <item><c>Authorization</c>    — generated via Tajeer portal → Users → API Registration (already includes scheme prefix)</item>
/// </list>
/// Headers are added with <c>TryAddWithoutValidation</c> because Tajeer rejects the
/// auto-formatted ASCII validation in <see cref="HttpRequestHeaders"/>.
/// </summary>
public sealed class TajeerAuthHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<TajeerOptions> _options;

    public TajeerAuthHandler(IOptionsMonitor<TajeerOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read fresh on every call so token rotation via IOptionsMonitor is picked up
        // without restarting the host.
        var options = _options.CurrentValue;

        // TryAddWithoutValidation: Tajeer's headers don't conform to the strict ASCII
        // validation that .NET applies via Headers.Add(); we set the raw value as-is.
        SetOrReplace(request.Headers, "App-id", options.AppId);
        SetOrReplace(request.Headers, "App-key", options.AppKey);
        SetOrReplace(request.Headers, "Authorization", options.AuthorizationToken);

        return base.SendAsync(request, cancellationToken);
    }

    private static void SetOrReplace(System.Net.Http.Headers.HttpRequestHeaders headers, string name, string value)
    {
        if (headers.Contains(name))
        {
            headers.Remove(name);
        }
        headers.TryAddWithoutValidation(name, value);
    }
}
