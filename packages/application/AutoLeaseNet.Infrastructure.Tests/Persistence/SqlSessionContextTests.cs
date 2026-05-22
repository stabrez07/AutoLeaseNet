using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// T2.3 — Integration test against a real SQL Server. Reads connection string from the
/// AUTOLEASENET_TEST_SQL env var, falling back to the local AutoLeaseNet_Dev DB with
/// Windows auth (per [[local-dev-infra]] memory: Docker isn't installed on this machine
/// so we use SQL Server 2019 Developer instead of the planned SQL Edge container).
///
/// Marked Trait("Category", "Integration") so CI skips it when no DB is available.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqlSessionContextTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AUTOLEASENET_TEST_SQL")
        ?? "Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";

    [Fact]
    public async Task SetTenantIdAsync_round_trips_via_SESSION_CONTEXT()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tenantId = Guid.NewGuid();
        await SqlSessionContext.SetTenantIdAsync(conn, tenantId);

        var readBack = await SqlSessionContext.GetTenantIdAsync(conn);
        readBack.Should().Be(tenantId, because: "SESSION_CONTEXT('TenantId') must reflect the value just set");
    }

    [Fact]
    public async Task SetTenantIdAsync_is_read_only_subsequent_set_throws()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        await SqlSessionContext.SetTenantIdAsync(conn, Guid.NewGuid());

        // sp_set_session_context with read_only=1 means subsequent sets on the same connection
        // fail with error 15664. RLS relies on this — app code must not be able to spoof tenancy.
        Func<Task> act = async () => await SqlSessionContext.SetTenantIdAsync(conn, Guid.NewGuid());

        var ex = await act.Should().ThrowAsync<SqlException>();
        ex.Which.Number.Should().Be(15664, because: "read_only=1 makes the key immutable for this session");
    }

    [Fact]
    public async Task SetTenancyAsync_sets_all_three_keys()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId, "INTERNAL_STAFF");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT
            CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER),
            CAST(SESSION_CONTEXT(N'CustomerId') AS UNIQUEIDENTIFIER),
            CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50))";
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        reader.GetGuid(0).Should().Be(tenantId);
        reader.GetGuid(1).Should().Be(customerId);
        reader.GetString(2).Should().Be("INTERNAL_STAFF");
    }

    [Fact]
    public async Task GetTenantIdAsync_returns_null_on_fresh_connection()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var tenantId = await SqlSessionContext.GetTenantIdAsync(conn);

        tenantId.Should().BeNull(because: "SESSION_CONTEXT is empty on a freshly opened connection");
    }
}
