using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLeaseNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Day-9 RLS workstream (2026-05-29). Adds:
    ///   1. <c>dbo.fn_TenancyPredicate(TenantId, CustomerId)</c> — see Spec 01 §3.4. The
    ///      predicate honours a <c>WEBHOOK_BOOTSTRAP</c> user-type override for the
    ///      anonymous Tajeer webhook receiver's cross-tenant lookup; that override is
    ///      Phase-1 tech debt and will be retired when webhook URLs encode tenant.
    ///   2. <c>dbo.TenancyPolicy</c> — applies FILTER + BLOCK_AFTER_INSERT +
    ///      BLOCK_AFTER_UPDATE predicates to nine aggregate-root tables. Excluded:
    ///        - <c>WebhookLogs</c>: arrives anonymous.
    ///        - <c>InspectionPhotos</c> / <c>InspectionDamageMarkers</c>: children that
    ///          today lack a <c>TenantId</c> column; loaded only via aggregate root
    ///          (Phase-2 backfill task).
    ///        - <c>__EFMigrationsHistory</c>: system.
    ///
    /// <para>Companion code: <c>TenancyConnectionInterceptor</c> writes
    /// <c>SESSION_CONTEXT('TenantId'|'CustomerId'|'UserType')</c> on every connection
    /// open. <c>SystemTenancyScope</c> overrides claim-derived tenancy for the demo
    /// seeder and the webhook receiver.</para>
    /// </remarks>
    public partial class Add_RLS_TenancyPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Predicate function — schema-bound so the policy below can depend on it.
            migrationBuilder.Sql(@"
CREATE FUNCTION dbo.fn_TenancyPredicate
(
    @TenantId   UNIQUEIDENTIFIER,
    @CustomerId UNIQUEIDENTIFIER
)
RETURNS TABLE WITH SCHEMABINDING
AS RETURN
    SELECT 1 AS allow
    WHERE
        -- Phase-1 webhook bootstrap override: anonymous Tajeer webhook handler runs
        -- a cross-tenant Lease lookup by contract number before any tenant is known.
        -- Phase-2 retires this clause when webhook URLs encode tenant.
        CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50)) = N'WEBHOOK_BOOTSTRAP'
        OR
        (
            @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER)
            AND
            (
                -- Internal staff + system processes see every row in their tenant.
                CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50)) IN (N'INTERNAL_STAFF', N'SYSTEM')
                OR
                -- External users (fleet admin / driver / individual) see only their customer's rows.
                @CustomerId = CAST(SESSION_CONTEXT(N'CustomerId') AS UNIQUEIDENTIFIER)
            )
        );
");

            // 2. Security policy applied to all nine aggregate-root tables.
            //    - Leases + Drivers carry an explicit CustomerId column.
            //    - Customers self-scope: its Id IS the CustomerId an external user matches.
            //    - Vehicles + Branches + RentPolicies + ExtendedCoverages + Inspections +
            //      Incidents pass NULL: external users see no rows (internal-only data in Phase 1).
            //      Phase 2 follow-ups: add CustomerId-derived predicates where the customer
            //      portal needs read access (e.g. ""my assigned vehicles"").
            migrationBuilder.Sql(@"
CREATE SECURITY POLICY dbo.TenancyPolicy
    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Leases,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Leases AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Leases AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.Customers,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.Customers AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, Id) ON dbo.Customers AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Vehicles AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Drivers,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Drivers AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, CustomerId) ON dbo.Drivers AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Branches,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Branches AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Branches AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.RentPolicies,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.RentPolicies AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.RentPolicies AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ExtendedCoverages,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ExtendedCoverages AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.ExtendedCoverages AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Inspections,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Inspections AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Inspections AFTER UPDATE,

    ADD FILTER PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Incidents,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Incidents AFTER INSERT,
    ADD BLOCK  PREDICATE dbo.fn_TenancyPredicate(TenantId, NULL) ON dbo.Incidents AFTER UPDATE
WITH (STATE = ON);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Order matters: drop the policy before the function it references.
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS dbo.TenancyPolicy;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_TenancyPredicate;");
        }
    }
}
