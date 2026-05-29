# Retrospective — Customer Portal scaffold

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

A real, usable Customer Portal scaffold replacing the placeholder page:

**Backend (small slice)**:
- `GetMyLeasesQuery` + `MyLeaseDto` in `AutoLeaseNet.Application.Me`.
- `GetMyLeasesQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Trusts Day-9
  RLS to scope by CustomerId — no app-side `Where(l => l.CustomerId == …)`,
  by design.
- Endpoint `GET /api/v1/me/leases` in `MeEndpoints.cs` (new endpoints group
  for the customer-portal surface).
- 3 endpoint tests: anonymous → 401, internal staff missing CustomerId → 400
  `me.requires_customer_context`, external customer → 200 with lease list.
  (The RLS scoping itself stays proven in `RlsIsolationTests` against real SQL —
  EF InMemory has no RLS, so the endpoint tests can only verify the wiring.)

**Frontend (customer-portal)**:
- `tailwind.config.mjs` + `postcss.config.mjs` with the same brand palette as
  web-portal (so the two surfaces feel like one product).
- `app/globals.css` Tailwind directives + RTL font fallback.
- `app/layout.tsx` wraps `<AppShell>` with `<LocaleProvider>` mirroring
  web-portal.
- `lib/locale-provider.tsx` — cookie-backed locale state, `html dir` flip on
  AR. Direct copy of web-portal pattern; will migrate to next-intl in tandem
  once design.md lands.
- `lib/i18n.ts` — customer-shaped messages (Dashboard, My Leases, dev banner,
  status labels for 7 LeaseStatus values + the 99 SaveFailed code).
- `lib/dev-customer.ts` — Phase-1 hardcoded demo customer:
  `cc368b8b-1f26-4b0b-a46d-495ab31a2dd8` (Driver-003, B2C with a seeded
  lease). Env-overridable via `NEXT_PUBLIC_DEV_CUSTOMER_ID` /
  `NEXT_PUBLIC_DEV_TENANT_ID`. The constant carries a runnable SQL snippet in
  its comment so the next dev can re-derive it if the seed RandomSeed
  changes.
- `lib/bff-client.ts` — typed client always sending
  `X-Dev-User-Type=EXTERNAL_INDIVIDUAL` + `X-Dev-Customer-Id` together (no
  partial state). One method today: `getMyLeases()`.
- `components/app-shell.tsx` — header with Dashboard / My Leases nav, AR/EN
  toggle, signed-in-as ribbon (showing the demo customer name), amber dev
  banner.
- `components/ui.tsx` — PageHeader / Card / StatCard / Spinner / ErrorBox /
  Badge primitives + a `statusTone(status)` helper so Dashboard + Leases
  table agree visually on Active=green, Suspended=amber, Closed=slate, etc.
- `app/page.tsx` — Dashboard: greeting + 3 stat cards (total / active /
  closed) computed client-side from `/me/leases` + CTA link to leases page.
- `app/leases/page.tsx` — table of leases: contract #, status badge, dates,
  rent SAR. Loading / error / empty states all wired.

## Adjacent fix included

While verifying the build, I found the **web-portal** `lib/i18n.ts` had the
same latent type-narrowing bug (`messagesEn` was `as const`, so the
literal-typed `Messages` type made AR translations un-assignable).
`pnpm typecheck` and `pnpm build` were failing on web-portal locally, but the
JS CI job uses `continue-on-error: true` on every step (per the comment
"apps are skeletal until design.md from user lands"), so the failure went
undetected. Removing `as const` from both portals' `messagesEn` makes both
build cleanly and the JS CI gate becomes meaningful when
`continue-on-error: true` is eventually removed.

## What went well

- The web-portal scaffold from PR #2 was an excellent template. Copy →
  customer-shape → done. Two hours end-to-end including the backend slice.
- The Day-9 + Day-20 hardening sprint just landed — being able to write a
  handler that says "trust the DB-side RLS for CustomerId scoping" instead of
  layering an app-side WHERE was the right kind of payback.
- The `MeEndpointTests` shape is honest about what InMemory can and can't
  prove. Avoiding an over-strong assertion that would have needed real SQL
  saved time and keeps the test useful.
- The dev-customer.ts approach (hardcoded id with documented re-derivation
  steps) is pragmatic for Phase-1 dev and the swap point for Entra External
  ID Phase-2 is one file.
- Both portals now build cleanly locally — surprise side-benefit of doing the
  customer-portal scaffold the same way and finding the shared bug.

## What surprised me

- **`continue-on-error: true` on every JS CI step** meant the web-portal
  build had been silently failing — possibly since the AR translations were
  added (PR #2). Worth a follow-up: drop `continue-on-error` from the
  typecheck and build steps now that two portals actually build cleanly. Lint
  + test can stay best-effort until the test infra lands.
- **Namespace collision** — my first stab put the query in
  `AutoLeaseNet.Application.Customer`, which collides with
  `AutoLeaseNet.Domain.Customers.Customer` (the aggregate type) used inside
  `SaveContractCommandHandler.cs`. Renamed to `.Me` to match the URL. Worth
  noting: any future "domain-name overlap with aggregate-name" should default
  to the URL-shaped name (`/me/...` → `.Me`).

## What I'd do differently

- **Shared test factory base** is now THREE workstreams overdue (called out
  in both Outbox and Reconciliation retros). The MeFactory is yet another
  copy of the same `ConfigureWebHost` pattern. Next workstream should land
  `BffTestHostDefaults.GetConfigDictionary()` returning the common keys.

## Numbers

- Files added (backend): 4 (`MyLeasesQuery.cs`, `MyLeasesQueryHandler.cs`,
  `MeEndpoints.cs`, `MeEndpointTests.cs`).
- Files added (frontend): 9 (`tailwind.config.mjs`, `postcss.config.mjs`,
  `app/layout.tsx`, `app/globals.css`, `lib/i18n.ts`,
  `lib/locale-provider.tsx`, `lib/dev-customer.ts`, `lib/bff-client.ts`,
  `components/app-shell.tsx`, `components/ui.tsx`, `app/page.tsx`,
  `app/leases/page.tsx`).
- Files modified: 3 (Program.cs adds `MapMeEndpoints`,
  customer-portal `app/page.tsx` rewrite, web-portal `lib/i18n.ts` `as const` fix).
- Plus: plan + retro.
- Tests: 276 → **279** default (+3 MeEndpoint).
- Both portals build green: web-portal (8 routes) + customer-portal (3 routes).
- Total elapsed: ~90 min.

## Hand-off

Phase-1 hardening sprint done. Demo-unblocking now has its first frontend
slice. Carry-forward picklist:

1. **ZATCA adapter (slice 1)** — Week-4 critical path; still zero code.
2. **`ITajeerContractClient.GetAsync`** — turns the reconciliation stub into
   a real drift detector. Small (~½ day).
3. **Vehicle Replacement Saga** — `IncidentReportedDomainEvent` subscriber.
4. **Customer Portal — My Vehicles** — needs an `/api/v1/me/vehicles`
   endpoint that scopes via the customer's leases (since RLS on Vehicles is
   internal-only by Day-9 design).
5. **Customer Portal — Lease detail page** — drill-in from the leases table.
6. **`BffTestHostDefaults` shared config helper** — third request from the
   retros. Prevents the next cross-cutting feature being a 10-file sweep.
7. **Drop `continue-on-error: true` from JS CI typecheck + build steps** —
   now that both portals build cleanly.
8. **Always Encrypted on PII** — gated on Azure Key Vault.

Each its own PR per the cadence.
