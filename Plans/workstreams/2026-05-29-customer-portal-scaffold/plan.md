# Customer Portal scaffold

**Date**: 2026-05-29
**Branch**: `feat/customer-portal-scaffold`
**PR**: TBD

## Why this, why now

Phase-1 demo gate. The web-portal is scaffolded; the customer-portal is one
placeholder file (`app/page.tsx` says "Real UI awaits design.md from the user").
Plan 02 Week-3 done-criteria explicitly says "Customer Portal shows customer's
leases + vehicles + invoices" — without it, the demo can't tell the customer-
side story.

User has explicitly waived the `design.md` gate ("currently there is no ui so I
can't"). Same approach as the web-portal: best-effort skeletal scaffold mirroring
the patterns the user has already accepted (Tailwind + brand palette +
LocaleProvider + AR/EN + RTL + typed BFF client). Real UI iterates later.

## Scope (this PR)

**Backend slice (small)**:
- `GetMyLeasesQuery` + `MyLeaseDto` in Application.
- `GetMyLeasesQueryHandler` in Infrastructure.
- New endpoint `GET /api/v1/me/leases` returning the **current customer's**
  leases. Internally relies on the Day-9 RLS predicate: when the request is an
  EXTERNAL user (`X-Dev-User-Type=EXTERNAL_INDIVIDUAL` + `X-Dev-Customer-Id`),
  the predicate restricts visibility to that customer's rows. The endpoint
  applies NO app-side `WHERE CustomerId == …` filter — RLS is the contract.
- Endpoint test: external user sees only their own leases; INTERNAL_STAFF sees
  everything (tenant-scoped); the endpoint requires auth.

**Frontend slice (Customer Portal)**:
- `apps/customer-portal/`:
  - `tailwind.config.mjs` + `postcss.config.mjs` (mirror web-portal).
  - `app/layout.tsx` + `app/globals.css` (Tailwind + brand palette).
  - `lib/locale-provider.tsx` (copy from web-portal, same AsyncLocal-style
    cookie-backed locale state).
  - `lib/i18n.ts` — customer-portal-shaped messages (Dashboard, My Leases,
    Sign In stub).
  - `lib/dev-customer.ts` — Phase-1 hardcoded demo customer id
    (`CC368B8B-1F26-4B0B-A46D-495AB31A2DD8` — a seeded B2C customer with a
    lease) + tenant id. Documents the future-Entra-External-ID flow.
  - `lib/bff-client.ts` — typed client with EXTERNAL headers
    (`X-Dev-User-Type=EXTERNAL_INDIVIDUAL`, `X-Dev-Customer-Id=<demo>`). One
    method today: `getMyLeases()`.
  - `components/app-shell.tsx` — simple header: brand + Dashboard / My Leases
    + AR/EN toggle.
  - `components/ui.tsx` — copy of web-portal's PageHeader / Card / StatCard /
    Spinner / ErrorBox / Badge so future pages reuse the same primitives.
  - `app/page.tsx` — dashboard: greeting + 3 stat cards (active leases,
    closed leases, total leases) computed client-side from `/me/leases`.
  - `app/leases/page.tsx` — table of leases with status badge, contract
    dates, Tajeer contract number.

## NOT in scope (defer)

- ❌ **Real authentication** — Entra External ID is Phase 2. Today the demo
  customer is hardcoded via `lib/dev-customer.ts` and the BFF receives
  `X-Dev-Customer-Id` via the dev-jwt stub. Production swap is one DI change.
- ❌ **"My Vehicles" page** — Vehicles RLS predicate excludes external users
  by design (Day-9 plan §"Tables in scope"). Need either a per-customer view
  derived from leases, or the planned `CustomerCanRead` view. Defer to a
  follow-up PR.
- ❌ **"My Invoices" page** — Invoice aggregate doesn't exist yet (Week 4).
- ❌ **Lease detail page** — list view first.
- ❌ **PII display protections** — fine for Phase-1 demo (Always Encrypted
  hasn't landed yet anyway).
- ❌ **next-intl migration** — same call as the web-portal: keep the flat
  cookie-based locale until `design.md` arrives; migration is mechanical
  later.
- ❌ **Tests for the frontend** — no test infra exists for either portal yet
  (true even pre-this-PR). Adding Vitest + RTL is its own workstream.

## Tasks (RED → GREEN)

1. **Plan** (this file).
2. **Backend `MyLeaseDto` + `GetMyLeasesQuery`** in
   `Application/Leases/Queries/`.
3. **`GetMyLeasesQueryHandler`** in `Infrastructure/Leases/Queries/` (handler
   pattern already established for lookup queries).
4. **Endpoint** `GET /api/v1/me/leases` in `services/bff/Endpoints/MeEndpoints.cs`
   + map in `Program.cs`.
5. **Endpoint test** with EXTERNAL_INDIVIDUAL header set to a seeded customer.
6. **Frontend config** — tailwind + postcss + layout + globals.
7. **Frontend lib** — locale-provider, i18n, dev-customer, bff-client.
8. **Frontend components** — app-shell, ui primitives.
9. **Frontend pages** — dashboard, leases.
10. **Build** — `pnpm --filter @autoleasenet/customer-portal build` succeeds.
11. **Full test suite** — `dotnet test` stays ≥ 276 green + new endpoint test.
12. **Update `ai_context.md`** + retrospective.
13. **Commit + PR + squash-merge.**

## Design notes

### Why no app-side CustomerId filter?

The handler reads the current user's `ITenantContext.CustomerId`. With RLS
enabled, including a `Where(l => l.CustomerId == ctx.CustomerId)` adds nothing
that the DB isn't already enforcing — and risks drift if the predicate ever
changes. Defense-in-depth ladder is intentionally:

1. Repository query passes `TenantId` (always).
2. Connection interceptor sets SESSION_CONTEXT.
3. RLS predicate filters rows.

For "me" endpoints specifically, #2 + #3 carry the CustomerId scope. Phase-2
Entra External ID swap doesn't change this.

### Demo customer choice

Picked `CC368B8B-1F26-4B0B-A46D-495AB31A2DD8` ("Driver-003") because it has a
seeded lease attached. The deterministic seed (RandomSeed=20260524) guarantees
this id is stable across rebuilds. If the seed config changes the id, the
constant in `dev-customer.ts` is the single update site.

## Risks

- **Seeded id stability** — if `Seed:RandomSeed` ever changes, the hardcoded
  demo customer id breaks. Mitigation: documented as a single-line update in
  `dev-customer.ts`; a future bootstrap could fetch the first B2C from a dev
  endpoint instead.
- **RLS empty result on misconfigured headers** — if the customer-portal sends
  `INTERNAL_STAFF` by accident (no `X-Dev-Customer-Id`), the predicate
  degrades to "internal user with no scope" which would return all leases
  in the tenant. Mitigation: bff-client unconditionally sets
  `X-Dev-User-Type=EXTERNAL_INDIVIDUAL` + `X-Dev-Customer-Id` together; no
  partial state.

## Definition of done

- [ ] All tasks complete.
- [ ] `pnpm --filter @autoleasenet/customer-portal build` succeeds.
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` green (≥ 277).
- [ ] Manual smoke: customer-portal at `:3001` renders dashboard with lease
      count and the leases table for the demo customer.
- [ ] `ai_context.md` updated.
- [ ] Retrospective written.
- [ ] PR squash-merged.
