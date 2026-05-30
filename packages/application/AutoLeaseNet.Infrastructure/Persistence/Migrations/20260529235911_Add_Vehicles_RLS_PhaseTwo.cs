using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Phase-2 of Day-9 RLS (workstream 2026-05-30-vehicles-rls-phase-2).
    ///
    /// <para>
    /// The original Day-9 migration parked <c>dbo.Vehicles</c> on
    /// <c>fn_TenancyPredicate(TenantId, NULL)</c> — external customers saw zero
    /// rows. Three handlers worked around it with a two-step lease→vehicle
    /// pattern under <c>SystemTenancyScope</c>. This migration introduces a
    /// dedicated predicate function for Vehicles that authorises an external
    /// customer iff they have (or ever had) a lease on the vehicle, and rewires
    /// the security policy to use it. The handlers then collapse to a single
    /// RLS-scoped query.
    /// </para>
    ///
    /// <para>
    /// The predicate intentionally does NOT filter by lease status. RLS answers
    /// "is the customer entitled to know this vehicle exists?". The handler
    /// keeps the "currently holding" business rule (Active/Extended/Suspended)
    /// in LINQ, which lets the lease-detail view show vehicle info for
    /// historical (Closed) leases without re-bypassing RLS.
    /// </para>
    ///
    /// <para>
    /// <b>Schema-binding implication:</b> <c>fn_VehiclesTenancyPredicate</c>
    /// references <c>dbo.Leases.(VehicleId, TenantId, CustomerId)</c> with
    /// SCHEMABINDING. Those columns become structurally pinned for as long as
    /// the function exists — drop them and this migration's Down() must run
    /// first. That's the intended coupling.
    /// </para>
    /// </remarks>
    public partial class Add_Vehicles_RLS_PhaseTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New predicate function. Mirrors fn_TenancyPredicate's UserType
            //    cascade, then for external users runs an EXISTS join against
            //    Leases on (VehicleId, TenantId, CustomerId).
            migrationBuilder.Sql(@"
CREATE FUNCTION dbo.fn_VehiclesTenancyPredicate
(
    @TenantId UNIQUEIDENTIFIER,
    @Id       UNIQUEIDENTIFIER
)
RETURNS TABLE WITH SCHEMABINDING
AS RETURN
    SELECT 1 AS allow
    WHERE
        -- Phase-1 webhook bootstrap (same clause + same retirement plan as
        -- fn_TenancyPredicate; once webhook URLs encode tenant we drop both).
        CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50)) = N'WEBHOOK_BOOTSTRAP'
        OR
        (
            @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER)
            AND
            (
                -- Internal staff + system processes see every vehicle in their tenant.
                CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50)) IN (N'INTERNAL_STAFF', N'SYSTEM')
                OR
                -- External user sees a vehicle iff they currently hold OR ever
                -- held a lease on it. Status filter intentionally NOT here —
                -- 'currently holding' is the handler's business rule, not RLS's.
                EXISTS (
                    SELECT 1
                    FROM dbo.Leases AS l
                    WHERE l.VehicleId  = @Id
                      AND l.TenantId   = @TenantId
                      AND l.CustomerId = CAST(SESSION_CONTEXT(N'CustomerId') AS UNIQUEIDENTIFIER)
                )
            )
        );
");

            // 2. Rewire dbo.TenancyPolicy's Vehicles predicates from
            //    fn_TenancyPredicate(TenantId, NULL) → fn_VehiclesTenancyPredicate(TenantId, Id).
            //    Use ALTER PREDICATE (in-place swap) rather than DROP+ADD because
            //    SQL Server's planner validates the post-state of a single ALTER
            //    SECURITY POLICY statement and reports duplicate-predicate errors
            //    when the same table appears in both DROP and ADD clauses.
            migrationBuilder.Sql(@"
ALTER SECURITY POLICY dbo.TenancyPolicy
    ALTER FILTER PREDICATE dbo.fn_VehiclesTenancyPredicate(TenantId, Id) ON dbo.Vehicles,
    ALTER BLOCK  PREDICATE dbo.fn_VehiclesTenancyPredicate(TenantId, Id) ON dbo.Vehicles AFTER INSERT,
    ALTER BLOCK  PREDICATE dbo.fn_VehiclesTenancyPredicate(TenantId, Id) ON dbo.Vehicles AFTER UPDATE;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: swap Vehicles back to the legacy NULL-CustomerId predicate
            // (still in-place via ALTER PREDICATE), then drop the new function.
            migrationBuilder.Sql(@"
ALTER SECURITY POLICY dbo.TenancyPolicy
    ALTER FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles,
    ALTER BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles AFTER INSERT,
    ALTER BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles AFTER UPDATE;
");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_VehiclesTenancyPredicate;");
        }
    }
}
