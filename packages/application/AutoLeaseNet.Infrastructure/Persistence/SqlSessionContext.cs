using Microsoft.Data.SqlClient;

namespace AutoLeaseNet.Infrastructure.Persistence;

/// <summary>
/// Helpers for setting and reading SQL Server <c>SESSION_CONTEXT</c> values. Used by the
/// BFF tenancy middleware to scope every query in the current request to its tenant —
/// the value is then read by the database's Row-Level Security predicates (Spec 01 §3.4).
///
/// <c>SESSION_CONTEXT</c> values are connection-scoped, so they reset when the connection
/// returns to the pool. The middleware sets them on the connection before each request
/// runs and trusts the pool to issue a fresh-context connection on the next request.
/// </summary>
public static class SqlSessionContext
{
    public const string TenantIdKey = "TenantId";
    public const string CustomerIdKey = "CustomerId";
    public const string UserTypeKey = "UserType";

    /// <summary>
    /// Set the <c>TenantId</c> session context for an open connection. Sets <c>@read_only=1</c>
    /// so app code can't change it mid-request — RLS depends on it being trustworthy.
    /// </summary>
    public static async Task SetTenantIdAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await SetGuidAsync(connection, TenantIdKey, tenantId, ct);
    }

    /// <summary>
    /// Set per-request tenancy session context (tenant + optional customer + user type).
    /// Use this from middleware once per request.
    /// </summary>
    public static async Task SetTenancyAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid? customerId,
        string userType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(userType);

        await SetGuidAsync(connection, TenantIdKey, tenantId, ct);
        if (customerId.HasValue)
        {
            await SetGuidAsync(connection, CustomerIdKey, customerId.Value, ct);
        }
        await SetStringAsync(connection, UserTypeKey, userType, ct);
    }

    /// <summary>Read the <c>TenantId</c> session context value, if any.</summary>
    public static async Task<Guid?> GetTenantIdAsync(SqlConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT CAST(SESSION_CONTEXT(N'{TenantIdKey}') AS UNIQUEIDENTIFIER)";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task SetGuidAsync(
        SqlConnection connection,
        string key,
        Guid value,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=@key, @value=@value, @read_only=1";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SetStringAsync(
        SqlConnection connection,
        string key,
        string value,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=@key, @value=@value, @read_only=1";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
