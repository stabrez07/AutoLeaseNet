using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// End-to-end proof that the <c>Add_RLS_TenancyPolicy</c> migration's security policy
/// actually filters cross-tenant rows at the database engine. Inserts one Customer row
/// under each of two synthetic tenants via raw ADO.NET — bypassing EF + repositories so
/// nothing in the app stack can "help" by injecting an extra TenantId WHERE — then
/// re-opens connections under each tenant's SESSION_CONTEXT and asserts that only
/// the tenant's own row is visible.
///
/// <para>Marked <c>Category=Integration</c> so CI skips it (no SQL Server on Linux
/// runners). Local dev runs it via the same conn-string fallback the
/// <see cref="SqlSessionContextTests"/> uses.</para>
///
/// <para>If this test ever fails, the security model is broken. Do not silence it.</para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class RlsIsolationTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AUTOLEASENET_TEST_SQL")
        ?? "Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";

    // Two synthetic tenants distinct from the real seed tenant
    // (a1a1a1a1-0001-0000-0000-000000000001).
    private static readonly Guid TenantA = Guid.Parse("aaaa1111-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbb2222-0000-0000-0000-000000000002");

    private readonly Guid _customerA = Guid.NewGuid();
    private readonly Guid _customerB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        // Insert one minimal Customer per tenant. We use SYSTEM context for each insert
        // so the BLOCK_AFTER_INSERT predicate accepts both rows.
        await InsertCustomerAsync(TenantA, _customerA, "Tenant-A Customer");
        await InsertCustomerAsync(TenantB, _customerB, "Tenant-B Customer");
    }

    public async Task DisposeAsync()
    {
        // Cleanup via raw DELETE — must run under SYSTEM context for either tenant to
        // satisfy BLOCK_AFTER_UPDATE. Two passes, one per tenant.
        await DeleteCustomerAsync(TenantA, _customerA);
        await DeleteCustomerAsync(TenantB, _customerB);
    }

    [Fact]
    public async Task Tenant_A_session_sees_only_tenant_A_rows()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, customerId: null, userType: "SYSTEM");

        var visibleIds = await SelectSeededCustomerIdsAsync(conn);

        visibleIds.Should().Contain(_customerA, because: "Tenant-A's row matches the predicate");
        visibleIds.Should().NotContain(_customerB, because: "RLS must hide Tenant-B's row from Tenant-A");
    }

    [Fact]
    public async Task Tenant_B_session_sees_only_tenant_B_rows()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantB, customerId: null, userType: "SYSTEM");

        var visibleIds = await SelectSeededCustomerIdsAsync(conn);

        visibleIds.Should().Contain(_customerB);
        visibleIds.Should().NotContain(_customerA);
    }

    [Fact]
    public async Task Connection_with_no_session_context_sees_neither_row()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        // Deliberately NOT calling SetTenancyAsync. The predicate's TenantId comparison
        // against a NULL session context evaluates to UNKNOWN → row hidden.

        var visibleIds = await SelectSeededCustomerIdsAsync(conn);

        visibleIds.Should().NotContain(_customerA);
        visibleIds.Should().NotContain(_customerB,
            because: "RLS must fail closed when no tenancy is in scope");
    }

    [Fact]
    public async Task Webhook_bootstrap_user_type_sees_rows_across_tenants()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        // The Tajeer webhook receiver runs under this UserType for its cross-tenant
        // Lease lookup. The predicate's first clause must let both tenants' rows through.
        await SqlSessionContext.SetTenancyAsync(conn, Guid.Empty, customerId: null, userType: "WEBHOOK_BOOTSTRAP");

        var visibleIds = await SelectSeededCustomerIdsAsync(conn);

        visibleIds.Should().Contain(_customerA);
        visibleIds.Should().Contain(_customerB);
    }

    [Fact]
    public async Task Tenant_A_session_cannot_insert_a_row_for_tenant_B()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, customerId: null, userType: "SYSTEM");

        Func<Task> attemptCrossTenantInsert = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO dbo.Customers
    (Id, TenantId, Type, Status, PreferredLanguage, DisplayName, KycVerified, PiiOptedOut, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, 1, 1, 1, @displayName, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());";
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@tenantId", TenantB);
            cmd.Parameters.AddWithValue("@displayName", "Cross-tenant attack");
            await cmd.ExecuteNonQueryAsync();
        };

        // SQL Server raises error 33504 (or similar) when a BLOCK predicate refuses
        // an INSERT/UPDATE. Either way the operation must throw, NOT silently succeed.
        await attemptCrossTenantInsert.Should().ThrowAsync<SqlException>(
            because: "BLOCK AFTER INSERT predicate must refuse a Tenant-B row under Tenant-A session");
    }

    // ---------- helpers ----------

    private static async Task InsertCustomerAsync(Guid tenantId, Guid customerId, string displayName)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.Customers
    (Id, TenantId, Type, Status, PreferredLanguage, DisplayName, KycVerified, PiiOptedOut, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, 1, 1, 1, @displayName, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());";
        cmd.Parameters.AddWithValue("@id", customerId);
        cmd.Parameters.AddWithValue("@tenantId", tenantId);
        cmd.Parameters.AddWithValue("@displayName", displayName);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DeleteCustomerAsync(Guid tenantId, Guid customerId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM dbo.Customers WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", customerId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<Guid>> SelectSeededCustomerIdsAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM dbo.Customers WHERE Id IN (@idA, @idB);";
        cmd.Parameters.AddWithValue("@idA", _customerA);
        cmd.Parameters.AddWithValue("@idB", _customerB);

        var ids = new HashSet<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }
}
