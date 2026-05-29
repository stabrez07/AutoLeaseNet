using AutoLeaseNet.Adapters.Zatca.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Bff.Health;

/// <summary>
/// Readiness signal for the ZATCA adapter. Per Spec 04 §7 every adapter exposes an
/// <see cref="IHealthCheck"/>; Phase-1 doesn't yet have a real Fatoorah ping so the
/// check reports based on the configured <see cref="ZatcaMode"/>:
///
/// <list type="bullet">
///   <item><see cref="ZatcaMode.InMemory"/> → <c>Healthy</c> — the fake is always ready.</item>
///   <item><see cref="ZatcaMode.Real"/> → <c>Degraded</c> — the real client is a stub
///         (returns <c>zatca.not_yet_implemented</c>); marking degraded keeps the readiness
///         probe green for the rest of the BFF while making it visible that ZATCA isn't
///         production-wired.</item>
/// </list>
///
/// Week-4 swaps the Real branch for a real Fatoorah <c>GET /status</c> probe.
/// </summary>
public sealed class ZatcaHealthCheck(IOptionsMonitor<ZatcaOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var mode = options.CurrentValue.Mode;
        return Task.FromResult(mode == ZatcaMode.InMemory
            ? HealthCheckResult.Healthy("ZATCA in-memory adapter wired")
            : HealthCheckResult.Degraded("ZATCA real client is a Phase-1 stub — Week-4 workstream lights up real Fatoorah clearance"));
    }
}
