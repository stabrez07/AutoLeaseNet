# Customer Portal

External self-service application for:
- **B2B fleet administrators** (corporate customers managing multiple vehicles)
- **B2C retail lessees** (individual customers with one vehicle)
- **Drivers** (employees of corporate customers, mobile-first)

## Status

Placeholder — minimal Next.js scaffold. Real screens to be built per [Phase 1 plan](../../Plans/02-phase-1-mvp-week-by-week.md) once `design.md` is provided.

## Auth

Phase 1: Entra External ID with email + SMS OTP (via Unifonic). Nafath federation deferred to Phase 3.

## Run

```bash
pnpm --filter @autoleasenet/customer-portal dev
# → http://localhost:3001
```
