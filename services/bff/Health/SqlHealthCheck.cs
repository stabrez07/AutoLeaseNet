using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AutoLeaseNet.Bff.Health;

/// <summary>
/// Readiness health check: opens a connection to the primary SQL database. Returns
/// Healthy on success, Unhealthy on any SqlException / timeout. Used to gate the
/// <c>/health/readiness</c> endpoint so traffic isn't routed to a pod that can't talk
/// to its DB.
/// </summary>
public sealed class SqlHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly string? _connectionString = configuration.GetConnectionString("AutoLeaseNet");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy("ConnectionStrings:AutoLeaseNet not configured");
        }

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 2;
            _ = await cmd.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQL Server reachable");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("SQL Server unreachable", ex);
        }
    }
}
