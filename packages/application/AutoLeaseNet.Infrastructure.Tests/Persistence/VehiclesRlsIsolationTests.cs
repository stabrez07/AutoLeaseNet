using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// Phase-2 RLS proof: verifies <c>dbo.fn_VehiclesTenancyPredicate</c> +
/// <c>dbo.TenancyPolicy</c>'s Vehicles predicates filter rows correctly for
/// each user-type / lease-status combination. Counterpart to
/// <see cref="RlsIsolationTests"/> which covers the original
/// <c>fn_TenancyPredicate</c>.
///
/// <para>
/// Setup is raw ADO.NET under <c>SYSTEM</c> context so nothing in the EF /
/// repository stack can shape the rows. Each test then re-opens a connection
/// under the user-type it's asserting and reads <c>dbo.Vehicles</c> directly.
/// </para>
///
/// <para>
/// Marked <c>Category=Integration</c> — CI Linux runners skip this; local dev
/// uses <c>AUTOLEASENET_TEST_SQL</c> or the integrated-security fallback.
/// </para>
///
/// <para>
/// If this file ever turns red the customer-portal trust boundary on the
/// vehicle aggregate is broken. Do not silence; fix the migration.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class VehiclesRlsIsolationTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AUTOLEASENET_TEST_SQL")
        ?? "Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";

    // Two synthetic tenants distinct from the seed tenant + the
    // RlsIsolationTests tenants so this suite can run alongside them.
    private static readonly Guid TenantA = Guid.Parse("cccc3333-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("dddd4444-0000-0000-0000-000000000002");

    // Tenant A inhabitants:
    //   _customerA holds an Active lease on _vehicleActive
    //   _customerA's PREVIOUS lease on _vehicleHistoric was Closed
    //   _vehicleOrphan has no lease at all — staff sees it, _customerA does not
    //   _customerOther has no leases — sees nothing under Tenant A
    private readonly Guid _customerA = Guid.NewGuid();
    private readonly Guid _customerOther = Guid.NewGuid();
    private readonly Guid _vehicleActive = Guid.NewGuid();
    private readonly Guid _vehicleHistoric = Guid.NewGuid();
    private readonly Guid _vehicleOrphan = Guid.NewGuid();

    // Tenant B: one vehicle + one customer with an active lease on it.
    // Used to prove cross-tenant isolation — Tenant A's customer must never
    // see Tenant B's vehicle even though they have the same UserType.
    private readonly Guid _customerB = Guid.NewGuid();
    private readonly Guid _vehicleB = Guid.NewGuid();

    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await InsertCustomerAsync(TenantA, _customerA, "Tenant-A Customer A");
        await InsertCustomerAsync(TenantA, _customerOther, "Tenant-A Customer Other");
        await InsertCustomerAsync(TenantB, _customerB, "Tenant-B Customer B");

        await InsertVehicleAsync(TenantA, _vehicleActive, plate: "9001", letters: "أ ب ج", branchId: _branchA);
        await InsertVehicleAsync(TenantA, _vehicleHistoric, plate: "9002", letters: "د هـ و", branchId: _branchA);
        await InsertVehicleAsync(TenantA, _vehicleOrphan, plate: "9003", letters: "ز ح ط", branchId: _branchA);
        await InsertVehicleAsync(TenantB, _vehicleB, plate: "9004", letters: "ي ك ل", branchId: _branchB);

        // statusActive = 3, statusClosed = 6 per LeaseStatus enum.
        await InsertLeaseAsync(TenantA, _customerA, _vehicleActive, status: 3);
        await InsertLeaseAsync(TenantA, _customerA, _vehicleHistoric, status: 6);
        await InsertLeaseAsync(TenantB, _customerB, _vehicleB, status: 3);
    }

    public async Task DisposeAsync()
    {
        await DeleteLeasesForVehicleAsync(TenantA, _vehicleActive);
        await DeleteLeasesForVehicleAsync(TenantA, _vehicleHistoric);
        await DeleteLeasesForVehicleAsync(TenantB, _vehicleB);

        await DeleteVehicleAsync(TenantA, _vehicleActive);
        await DeleteVehicleAsync(TenantA, _vehicleHistoric);
        await DeleteVehicleAsync(TenantA, _vehicleOrphan);
        await DeleteVehicleAsync(TenantB, _vehicleB);

        await DeleteCustomerAsync(TenantA, _customerA);
        await DeleteCustomerAsync(TenantA, _customerOther);
        await DeleteCustomerAsync(TenantB, _customerB);
    }

    [Fact]
    public async Task External_customer_sees_vehicle_with_active_lease()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, _customerA, userType: "EXTERNAL_INDIVIDUAL");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().Contain(_vehicleActive,
            because: "external customer holds an Active lease on this vehicle");
    }

    [Fact]
    public async Task External_customer_sees_vehicle_with_only_closed_lease()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, _customerA, userType: "EXTERNAL_INDIVIDUAL");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().Contain(_vehicleHistoric,
            because: "RLS grants visibility for any lease the customer ever held; " +
                     "the 'currently holding' filter is the handler's business rule, not RLS's");
    }

    [Fact]
    public async Task External_customer_does_not_see_orphan_vehicle()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, _customerA, userType: "EXTERNAL_INDIVIDUAL");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().NotContain(_vehicleOrphan,
            because: "RLS predicate requires EXISTS(lease for caller's customer) and none exists");
    }

    [Fact]
    public async Task External_customer_does_not_see_other_tenant_vehicle()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, _customerA, userType: "EXTERNAL_INDIVIDUAL");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().NotContain(_vehicleB,
            because: "TenantId-clause excludes other tenants regardless of lease shape");
    }

    [Fact]
    public async Task External_customer_with_no_leases_sees_zero_vehicles_in_tenant()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, _customerOther, userType: "EXTERNAL_INDIVIDUAL");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().NotContain(_vehicleActive);
        visible.Should().NotContain(_vehicleHistoric);
        visible.Should().NotContain(_vehicleOrphan);
    }

    [Fact]
    public async Task Internal_staff_sees_all_vehicles_in_tenant()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, customerId: null, userType: "INTERNAL_STAFF");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().Contain(_vehicleActive);
        visible.Should().Contain(_vehicleHistoric);
        visible.Should().Contain(_vehicleOrphan,
            because: "internal staff branch of the predicate sees every row in their tenant");
        visible.Should().NotContain(_vehicleB,
            because: "internal staff bypass is tenant-scoped, not cross-tenant");
    }

    [Fact]
    public async Task System_context_sees_all_vehicles_in_tenant()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, TenantA, customerId: null, userType: "SYSTEM");

        var visible = await SelectVehicleIdsAsync(conn);

        visible.Should().Contain(_vehicleActive);
        visible.Should().Contain(_vehicleOrphan);
        visible.Should().NotContain(_vehicleB);
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

    private static async Task InsertVehicleAsync(Guid tenantId, Guid vehicleId, string plate, string letters, Guid branchId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.Vehicles
    (Id, TenantId, Status, PlateNumber, PlateLetters, PlateTypeCode, Vin,
     Make, Model, ModelYear, FuelType, TransmissionType, BodyType, Seats,
     OwnerBranchId, CurrentBranchId, CurrentKm, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, 1, @plate, @letters, 1, @vin,
     'Toyota', 'Camry', 2024, 1, 1, 1, 5,
     @branchId, @branchId, 10000, SYSUTCDATETIME(), SYSUTCDATETIME());";
        cmd.Parameters.AddWithValue("@id", vehicleId);
        cmd.Parameters.AddWithValue("@tenantId", tenantId);
        cmd.Parameters.AddWithValue("@plate", plate);
        cmd.Parameters.AddWithValue("@letters", letters);
        cmd.Parameters.AddWithValue("@vin", $"VIN-{vehicleId:N}".Substring(0, 32));
        cmd.Parameters.AddWithValue("@branchId", branchId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertLeaseAsync(Guid tenantId, Guid customerId, Guid vehicleId, int status)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.Leases
    (Id, TenantId, CustomerId, VehicleId, ContractTypeCode,
     ContractStartUtc, ContractEndUtc, RentAmount, Status,
     UnlimitedKm, AllowedKmPerHour, AllowedKmPerDay, AllowedLateHours,
     ExtensionCount, PiiOptedOut, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, @customerId, @vehicleId, 1,
     SYSUTCDATETIME(), DATEADD(day, 10, SYSUTCDATETIME()), 200, @status,
     0, 0, 300, 0,
     0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@tenantId", tenantId);
        cmd.Parameters.AddWithValue("@customerId", customerId);
        cmd.Parameters.AddWithValue("@vehicleId", vehicleId);
        cmd.Parameters.AddWithValue("@status", status);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DeleteVehicleAsync(Guid tenantId, Guid vehicleId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM dbo.Vehicles WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", vehicleId);
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

    private static async Task DeleteLeasesForVehicleAsync(Guid tenantId, Guid vehicleId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await SqlSessionContext.SetTenancyAsync(conn, tenantId, customerId: null, userType: "SYSTEM");
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM dbo.Leases WHERE VehicleId = @vehicleId;";
        cmd.Parameters.AddWithValue("@vehicleId", vehicleId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<Guid>> SelectVehicleIdsAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id FROM dbo.Vehicles
WHERE Id IN (@a, @b, @c, @d);";
        cmd.Parameters.AddWithValue("@a", _vehicleActive);
        cmd.Parameters.AddWithValue("@b", _vehicleHistoric);
        cmd.Parameters.AddWithValue("@c", _vehicleOrphan);
        cmd.Parameters.AddWithValue("@d", _vehicleB);

        var ids = new HashSet<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }
}
