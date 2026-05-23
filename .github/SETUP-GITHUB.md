# GitHub Setup — L.G1 / L.G2 / L.G3

Loop-back tasks from the Week 1 plan, scoped to a single owner-action runbook.

| Task                                                                          | Status                                          | Owner |
| ----------------------------------------------------------------------------- | ----------------------------------------------- | ----- |
| **L.G1** GitHub Actions `ci.yml` (restore → build -warnaserror → test → pnpm) | ✅ Done (in `.github/workflows/ci.yml`)         | —     |
| **L.G2** Branch protection on `main` (CI green + 1 review required)           | ⛔ Blocked on **GitHub Pro** OR **public repo** | You   |
| **L.G3** CI secrets (Tajeer creds, ZATCA CSID, Azure deploy SP)               | ⛔ Blocked on same                              | You   |

## Why L.G2 / L.G3 are blocked today

`stabrez07/AutoLeaseNet` is a **private repo on the free GitHub plan**. Per [GitHub docs](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches), branch-protection rules and the repository-secrets management API on private repos require **GitHub Pro** (or the repo being public). The CLI confirms this:

```pwsh
gh api repos/stabrez07/AutoLeaseNet/branches/main/protection
# {"message":"Upgrade to GitHub Pro or make this repository public to enable this feature.","status":"403"}
```

You have two paths. **Recommended: Option B (public repo) — $0 cost, immediate unblock.** Reasoning:

- Phase-1 vendor surface in `Adapters.Tajeer/` is built off the **public** Tajeer V9.7 spec; no proprietary contract leaks.
- All secrets (Tajeer creds, ZATCA CSID, webhook shared secret) live in `dotnet user-secrets` + GitHub Secrets — never in source.
- `services/bff/appsettings.json` and every committed sample have been audited (see Option B checklist below).
- We can revert to private + Pro later if a vendor NDA constrains us; the cutover is one `gh repo edit` command.

Pick one then run the matching section below.

---

## Option A — Upgrade to GitHub Pro ($4/month)

Choose only if vendor NDA or business policy mandates a private repo. Otherwise prefer Option B (recommended).

1. Visit [github.com/settings/billing/plans](https://github.com/settings/billing/plans) → upgrade to Pro.
2. Once active, run the §"Apply L.G2 + L.G3" commands below.

## Option B — Make the repo public **← recommended**

$0 cost, immediate. Pre-flight checklist (all verified — re-confirm before flipping):

- `services/bff/appsettings.json` — no real credentials tracked.
- `packages/adapters/AutoLeaseNet.Adapters.Tajeer/Webhooks/WebhookSignatureValidator.cs` — constant-time compare, secret never logged.
- Every committed JSON sample uses seed contract numbers `1_000_000_001+seq`, never a real Tajeer number.
- `.env` / `appsettings.Development.json` / user-secrets are gitignored.

```pwsh
gh repo edit stabrez07/AutoLeaseNet --visibility public --accept-visibility-change-consequences
```

After the flip, run §"Apply L.G2" and §"Apply L.G3" below.

---

## Apply L.G2 — branch protection

Run after either Option A or B unblocks the API:

```pwsh
gh api -X PUT repos/stabrez07/AutoLeaseNet/branches/main/protection `
  -F required_status_checks.strict=true `
  -F required_status_checks.contexts[]=".NET (build -warnaserror + test)" `
  -F enforce_admins=true `
  -F required_pull_request_reviews.required_approving_review_count=1 `
  -F required_pull_request_reviews.dismiss_stale_reviews=true `
  -F restrictions=
```

What this enforces:

- Every `main` change must come through a PR.
- The `.NET (build -warnaserror + test)` job from `ci.yml` must be green before merge.
- One approving review required; stale reviews dismissed on new commits.
- Admins are NOT exempt (so accidental local `git push origin main` still bounces).

To verify:

```pwsh
gh api repos/stabrez07/AutoLeaseNet/branches/main/protection --jq '{required_status_checks,required_pull_request_reviews,enforce_admins}'
```

---

## Apply L.G3 — repository secrets

Five secrets the `tajeer-staging-smoke` job in `ci.yml` reads. Set each via `gh secret set` (it prompts for the value so the secret never lands in your shell history):

```pwsh
gh secret set TAJEER_APPID          -R stabrez07/AutoLeaseNet
gh secret set TAJEER_APPKEY         -R stabrez07/AutoLeaseNet
gh secret set TAJEER_AUTHORIZATION  -R stabrez07/AutoLeaseNet   # value includes "Basic " prefix
gh secret set TAJEER_BRANCHID       -R stabrez07/AutoLeaseNet
gh secret set TAJEER_WEBHOOKSECRET  -R stabrez07/AutoLeaseNet
```

Confirm:

```pwsh
gh secret list -R stabrez07/AutoLeaseNet
```

Expected output: 5 rows with `Updated YYYY-MM-DD`.

### Future secrets (Phase 2+)

Add when the corresponding loop-back unblocks:

```pwsh
# When Azure subscription lands (L.A1-A4):
gh secret set AZURE_CLIENT_ID         -R stabrez07/AutoLeaseNet
gh secret set AZURE_TENANT_ID         -R stabrez07/AutoLeaseNet
gh secret set AZURE_SUBSCRIPTION_ID   -R stabrez07/AutoLeaseNet

# When ZATCA Phase-2 production cert lands (Week 4):
gh secret set ZATCA_CSID              -R stabrez07/AutoLeaseNet
gh secret set ZATCA_CSID_PRIVATE_KEY  -R stabrez07/AutoLeaseNet

# When Unifonic sandbox approves the sender id (L.U2):
gh secret set UNIFONIC_APP_SID        -R stabrez07/AutoLeaseNet
gh secret set UNIFONIC_SENDER_ID      -R stabrez07/AutoLeaseNet
```

---

## Verify the first CI run after this commit

```pwsh
gh run watch -R stabrez07/AutoLeaseNet
# Or:
gh run list -R stabrez07/AutoLeaseNet --limit 3
```

Expected on the first push of this commit:

- **`.NET (build -warnaserror + test)`** → ✅ (153 tests pass)
- **`JS (lint + typecheck + test + build) — best-effort until UI lands`** → ✅ (steps run with `continue-on-error: true`)
- **`Tajeer staging smoke (Category=Smoke)`** → ✅ if `secrets.TAJEER_APPKEY` is set OR ✅ skipped clean (the smoke test early-returns when `Tajeer:AppId` env var is missing). On a free private repo without L.G3 applied, the secret will be empty and the smoke test will silently pass.

---

## Done criteria for L.G1/L.G2/L.G3

- [x] **L.G1** `ci.yml` exists, validates as YAML, builds .NET with WarnAsError, runs all 153 tests with `--settings .runsettings`, uploads test results as an artifact.
- [ ] **L.G2** `main` requires CI green + 1 approval; admins not exempt; status check `.NET (build -warnaserror + test)` is required.
- [ ] **L.G3** All 5 Tajeer secrets present in repo settings; `tajeer-staging-smoke` job runs and exits 0 (whether by real smoke or by graceful skip).
