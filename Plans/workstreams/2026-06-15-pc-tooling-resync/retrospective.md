# Retrospective: New-PC Developer Tooling Resync (2026-06-15)

## What happened

Ran a focused verification pass against a freshly-cloned (or freshly-opened) checkout on
a new Windows PC. Goal was to surface every gap between what the repo requires and what
was actually installed, and produce a concrete action list so any new contributor can
reach `dotnet build ✅` and `pnpm bff` in one sitting.

---

## Findings summary

### Tooling gaps (manual installs required)

| Tool | Required | Installed | Action |
|------|----------|-----------|--------|
| .NET SDK | `>=8.0.206` (latestMajor) | `10.0.301` ✅ | None — already satisfies rollForward |
| Node.js | `>=20.11.0` | **Not installed** | Install via `winget install OpenJS.NodeJS.LTS` or NVM for Windows |
| pnpm | `9.0.0` | **Not installed** | `npm install -g pnpm@9.0.0` (after Node) |
| Docker Desktop | any recent | **Not installed** | Install from https://www.docker.com/products/docker-desktop/ |
| `dotnet-ef` global tool | >=8 | **Not installed** | `dotnet tool install --global dotnet-ef` |
| BFF user secrets | see plan.md | **Not set up** | Run the `dotnet user-secrets set ...` commands in plan.md |

### Build issues found and resolved

**CA1873 analyzer regression** — surfaced because this PC has .NET SDK `10.0.301` while
`global.json` was originally authored against SDK `8.x`. SDK 10's Roslyn analyzer enforces
CA1873 ("Evaluate expensive argument only when logging is enabled") more aggressively.

Two handler files passed `.ToString()` eagerly on enum values into `[LoggerMessage]`-attributed
partial methods. Fixed by promoting parameter types from `string` to the concrete enum type,
letting the source generator handle lazy stringification (the correct pattern per CLAUDE.md §10
"LoggerMessage source generators everywhere").

Files changed:
- `packages/application/AutoLeaseNet.Application/Operations/IncidentCommandHandlers.cs`
- `packages/application/AutoLeaseNet.Application/Operations/InspectionCommandHandlers.cs`

### Build issue NOT resolved (carry-forward blocker)

**Duplicate EF migration class** in branch `feat/day23-approval-saga`:
- `20260607172013_Add_Quotation_Aggregate.cs` (merged to main in PR #32)
- `20260607174509_Add_Quotation_Aggregate.cs` (added on feature branch — same class name)

Compiler error: CS0111 "Type already defines a member" on `Up`, `Down`, `BuildTargetModel`.
This is a branch-level authoring error and must be resolved by the developer as part of
completing `feat/day23-approval-saga`. Options:
1. **Delete `20260607174509_*`** and inline the schema difference (column widths, `EnsureSchema`)
   into the existing PR migration if it hasn't been applied to any environment yet.
2. **Rename the newer migration** to `Add_ApprovalTier_ColumnFix` (new timestamp, new name,
   new class) if the original already ran on staging.

---

## What went well

- `dotnet restore` ran to completion cleanly on the first try (NuGet feeds accessible).
- `.NET SDK 10` satisfies `rollForward: latestMajor` in `global.json` without any config
  change.
- `compose/.env.example` is comprehensive and clearly maps to every BFF config key.
- The `[LoggerMessage]` pattern is already consistent everywhere else in the codebase;
  the two CA1873 failures were straightforward to fix.

## What could be better

- **Node/pnpm/Docker absent with no CI gating on dev machine**: the `engine-strict=true`
  in `.npmrc` correctly enforces the engine range, but only once pnpm is installed.
  Consider adding a `scripts/check-tools.ps1` (or Makefile target) that a dev can run
  once to verify everything in one shot.
- **No `appsettings.Development.json` in the repo**: by design (avoids leaking credentials),
  but the gap between "clone and try to run BFF" and "BFF actually starts" is entirely
  undocumented in the existing README. The user-secrets key list in `plan.md` of this
  workstream fills that gap; consider promoting it to `README.md` or a `CONTRIBUTING.md`.
- **Duplicate migration** indicates the feature branch was rebased/amended after the
  original migration landed in `main`, and the developer forgot to remove the old file.
  Adding a CI lint step that checks for duplicate EF migration class names would catch
  this at PR time.

## Carry-forward actions

| Priority | Action | Owner |
|----------|--------|-------|
| 🔴 HIGH | Resolve duplicate migration in `feat/day23-approval-saga` | dev |
| 🔴 HIGH | Install Node >=20.11.0 + pnpm 9.0.0 + Docker Desktop | dev |
| 🟡 MED | Set up BFF user secrets (see plan.md key list) | dev |
| 🟡 MED | Install `dotnet-ef` global tool | dev |
| 🟢 LOW | Promote user-secrets quick-start block to `README.md` or `CONTRIBUTING.md` | future PR |
| 🟢 LOW | Add `scripts/check-tools.ps1` — one-shot dev environment verifier | future PR |
