// Phase-1 demo identity. The customer portal currently scopes everything to ONE
// hardcoded customer via X-Dev-Customer-Id; Phase 2 replaces this with real Entra
// External ID OIDC + the customer-id claim from the issued JWT.
//
// The id below is a B2C customer the Bogus seeder reliably produces (RandomSeed
// stable). If you change Seed:RandomSeed, run a quick query like:
//
//   sqlcmd -S . -d AutoLeaseNet_Dev -E -Q "
//     EXEC sp_set_session_context @key=N'TenantId', @value='a1a1a1a1-...', @read_only=1;
//     EXEC sp_set_session_context @key=N'UserType', @value=N'SYSTEM', @read_only=1;
//     SELECT TOP 1 c.Id FROM dbo.Customers c
//       JOIN dbo.Leases l ON l.CustomerId = c.Id
//      WHERE c.Type = 2 ORDER BY l.UpdatedAtUtc DESC"
//
// …and paste the resulting GUID below.

export const DEV_DEMO_CUSTOMER = {
  tenantId:
    process.env.NEXT_PUBLIC_DEV_TENANT_ID ??
    'a1a1a1a1-0001-0000-0000-000000000001',
  customerId:
    process.env.NEXT_PUBLIC_DEV_CUSTOMER_ID ??
    'cc368b8b-1f26-4b0b-a46d-495ab31a2dd8', // Driver-003 from seed, has a lease
  displayName: 'Driver-003',
  userType: 'EXTERNAL_INDIVIDUAL',
} as const
