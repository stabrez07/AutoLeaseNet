# Week 1 — Tajeer Staging Smoke Runbook

**Single-session script that closes the 7 remaining Week-1 boxes.** Run when you have
~45 minutes uninterrupted with Tajeer Rabet staging credentials + an ngrok account.

| Box | Closes |
|---|---|
| T3.7 | Paste PII-masked branches response into notes.md Day 3 section |
| T5.7 | First real Save against Tajeer Rabet staging |
| T5.8 | Paste sanitized SaveContract request/response into notes.md Day 5 section |
| T6.7 | ngrok tunnel + register webhook URL with Tajeer staging |
| T6.8 | End-to-end smoke (POST save → wait for real webhook → assert Lease.Active) |
| T6.9 | Flip `Tajeer:Webhook:LogOnly = false` |
| T7.8 | Walk the Done-criteria list + capture video / screenshots |

---

## 0. Pre-flight (10 min)

### 0.1 Credentials in hand

| Item | Where |
|---|---|
| Tajeer `App-id` | Rabet portal → Users → API Registration |
| Tajeer `App-key` | Same place |
| Tajeer `Authorization` (Basic …) | Same place, includes `Basic ` prefix |
| Tajeer branch id (integer) | Lookup via `branches` endpoint or Rabet portal |
| Tajeer operator id (long) | Branch detail |
| Tajeer webhook shared secret | Self-chosen string you'll register with Tajeer in Step 4 |
| Staging Vehicle id + RentPolicy id + PaymentMethod code | Step 1 will discover these |

### 0.2 ngrok account
- Sign up at [ngrok.com](https://ngrok.com) (free tier is fine for this session).
- Install: `winget install Ngrok.Ngrok` or download the zip.
- Auth: `ngrok config add-authtoken <your-token>` (one time).

### 0.3 Repo state check
```pwsh
cd C:\Users\Administrator\Desktop\AutoLeaseNet
git status              # should say "working tree clean" on main at 88e67ad or later
git log --oneline -1    # confirm head
dotnet build AutoLeaseNet.sln --nologo  # should be 0 warnings, 0 errors
```

### 0.4 SQL state check
```pwsh
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT 'Branches' AS t, COUNT(*) AS n FROM Branches UNION ALL SELECT 'Customers', COUNT(*) FROM Customers UNION ALL SELECT 'Leases', COUNT(*) FROM Leases"
```
Expected (from the seed): `Branches=3`, `Customers=20`, `Leases=10`.

If empty: just run the BFF once (Step 3.2) — the seeder will populate on first start.

### 0.5 Kill any leftover BFF process
```pwsh
try { Get-Process -Name AutoLeaseNet.Bff -ErrorAction Stop | Stop-Process -Force; "killed" } catch { "none running" }
```

---

## 1. T3.7 — Tajeer branches lookup smoke (5 min)

This proves auth headers + Polly pipeline work against real Tajeer before we attempt SaveContract.

### 1.1 Drop creds into the smoke-test user-secrets store

```pwsh
cd packages\adapters\AutoLeaseNet.Adapters.Tajeer.Tests
dotnet user-secrets set "Tajeer:AppId"              "<app-id>"
dotnet user-secrets set "Tajeer:AppKey"             "<app-key>"
dotnet user-secrets set "Tajeer:AuthorizationToken" "Basic <base64>"
dotnet user-secrets set "Tajeer:BranchId"           "<your branch>"
dotnet user-secrets set "Tajeer:WebhookSharedSecret" "<webhook-secret>"
cd ..\..\..
```

### 1.2 Run only the smoke test

```pwsh
dotnet test packages\adapters\AutoLeaseNet.Adapters.Tajeer.Tests --filter Category=Smoke
```

**Expected output (tail):**
```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: ...
Branch count: <N>
First branch: id=<int> code=<str> nameEn=<str> city=<str> active=True
```

### 1.3 If it fails with HTTP 401 / 403

The most common cause is `Authorization` header missing the `Basic ` prefix or a copy-paste extra whitespace. Re-set the secret:

```pwsh
cd packages\adapters\AutoLeaseNet.Adapters.Tajeer.Tests
dotnet user-secrets list
# Confirm "Tajeer:AuthorizationToken = Basic abc..." not just "abc..."
```

### 1.4 Capture for T3.7

Mask any sensitive fields (`licenseNumber` → keep last 4) and paste into [notes.md §"T3.7 placeholder"](./notes.md). The masked template is already in the file under Day 3.

---

## 2. T5.7 / T5.8 — First real SaveContract via the BFF (15 min)

### 2.1 Drop creds into the BFF user-secrets store

```pwsh
cd services\bff
dotnet user-secrets set "Tajeer:AppId"              "<staging-app-id>"
dotnet user-secrets set "Tajeer:AppKey"             "<staging-app-key>"
dotnet user-secrets set "Tajeer:AuthorizationToken" "Basic <staging-base64>"
dotnet user-secrets set "Tajeer:BranchId"           "<your branch>"
dotnet user-secrets set "Tajeer:WebhookSharedSecret" "<webhook-secret>"
dotnet user-secrets set "Tajeer:Mode"               "Real"
cd ..\..
```

> ⚠️ `Tajeer:Mode` defaults to `Real` if missing — but be explicit so a later
> `appsettings.Development.json` change can't silently demote the BFF to InMemory.

### 2.2 Run the BFF

```pwsh
dotnet run --project services\bff\AutoLeaseNet.Bff.csproj
```

Wait for `Application started. Press Ctrl+C to shut down.` Then leave this terminal open and switch to another.

### 2.3 Discover seeded ids the form will need

```pwsh
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 1 Id, DisplayName FROM Customers WHERE Type=2 ORDER BY CreatedAtUtc"
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 1 Id, PlateNumber FROM Vehicles WHERE Status=1"
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 1 Id, PersonNameEn FROM Drivers WHERE Status=1"
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 1 Id, Code FROM RentPolicies"
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 1 Id, Code FROM Branches"
```

Copy each `Id` for the next step.

### 2.4 POST the SaveContract

In a second PowerShell window:

```pwsh
$customerId = "<paste-customer-id>"
$vehicleId  = "<paste-vehicle-id>"
$driverId   = "<paste-driver-id>"
$policyId   = "<paste-rentpolicy-id>"
$branchId   = "<paste-branch-id>"
$tenantId   = "a1a1a1a1-0001-0000-0000-000000000001"  # seed tenant

$body = @{
  customerId        = $customerId
  vehicleId         = $vehicleId
  primaryDriverId   = $driverId
  rentPolicyId      = $policyId
  workingBranchId   = $branchId
  receiveBranchId   = $branchId
  returnBranchId    = $branchId
  contractStartUtc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
  contractEndUtc    = (Get-Date).AddDays(2).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
  contractTypeCode  = 1
  allowedKmPerDay   = 300
  rentAmount        = 200
  paidAmount        = 50
  paymentMethodCode = 1
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod `
  -Uri    "https://localhost:5001/api/v1/dev/save-contract" `
  -Method POST `
  -Headers @{
    "X-Dev-Tenant-Id" = $tenantId
    "X-Dev-User-Type" = "InternalStaff"
    "Idempotency-Key" = ([Guid]::NewGuid().ToString("N"))
    "Content-Type"    = "application/json"
  } `
  -Body $body `
  -SkipCertificateCheck

$response
```

**Expected:** `202 Accepted` with `leaseId`, `tajeerContractNumber`, `issuanceUrl`. The `issuanceUrl` points at `tajeerstg.logisti.sa/#/public-contract/{number}/{token}`.

### 2.5 Verify the row landed locally

```pwsh
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TOP 5 TajeerContractNumber, Status, CreatedAtUtc FROM Leases ORDER BY CreatedAtUtc DESC"
```

The new row's `Status` should be `2` (= `LeaseStatus.PendingIssuance`).

### 2.6 If POST returns 422

The handler validates 5 gates. The most common 422s on first run:
- `lease.vehicle.not_available` — the seeded vehicle was already reserved by a previous attempt. Pick a different vehicle id.
- `lease.driver.license_expired` — pick a different driver, or seed updates the LicenseExpiryDate.
- `lease.customer.not_active` — shouldn't happen on freshly seeded data; check that you queried Customer not from the wrong type filter.

### 2.7 If POST returns 503

`Tajeer:Mode` is `Real` and Tajeer is unreachable. Verify:
- BFF log shows `Tajeer ... resilience pipeline` retry attempts.
- `ping tajeer-stg.api.elm.sa` works from this machine.
- The 503 body should contain `tajeer.network` or `tajeer.timeout`.

### 2.8 Capture for T5.8

Mask renter mobile/idNumber per `PiiMasking.Mask(...)` rules, then paste the request + response into [notes.md §"T5.8 placeholder"](./notes.md). Template already in the file under Day 5.

---

## 3. T6.7 — ngrok tunnel + register webhook (5 min)

### 3.1 Start ngrok

In a third PowerShell window:

```pwsh
ngrok http https://localhost:5001 --host-header="localhost:5001"
```

Copy the `Forwarding` URL (e.g. `https://abc123.ngrok.io`). The webhook URL Tajeer will POST to is:

```
https://abc123.ngrok.io/api/v1/webhooks/tajeer
```

### 3.2 Register the URL with Tajeer

Use Tajeer's `POST /api/webhooks/register` endpoint (see Spec 03 §6.16) OR the Rabet portal UI:
- Notification URL: `https://abc123.ngrok.io/api/v1/webhooks/tajeer`
- Shared secret: the value you set in step 2.1 as `Tajeer:WebhookSharedSecret`

### 3.3 Verify Tajeer sends a test event

Tajeer typically fires a `webhook.registered` test event immediately on registration. Watch the BFF console for:

```
info: TajeerWebhook[6004 or similar]
      Tajeer webhook id=<id> type=webhook.registered ...
```

If you see `401 Unauthorized` in the ngrok terminal, the shared secret doesn't match. Re-register or correct user-secrets + re-run BFF.

### 3.4 Capture the URL for the notes
Paste the ngrok URL + the timestamp + the test-event response into a new section in notes.md under "Day 6 — T6.7 ngrok registration".

---

## 4. T6.8 — End-to-end smoke (5 min)

The lease from Step 2 is in `PendingIssuance`. Now drive the renter completion to fire the issuance webhook.

### 4.1 Open the issuance URL in a browser

Paste the `issuanceUrl` from Step 2.4 into a browser. Tajeer's hosted page asks the renter to confirm (Saudi National OTP or test bypass on staging).

Complete the form. Within ~30 seconds Tajeer will POST to the registered webhook.

### 4.2 Watch the BFF console

Expected log sequence:
```
info: AspNetCore       POST /api/v1/webhooks/tajeer
info: TajeerWebhook    received id=<tajeer-event-id> type=contract.create referenceId=<contract-number>
info: SaveContract[5003] Lease <id> saved in PendingIssuance ...  <-- from earlier
info: LeaseIssuedSms[7004] LeaseIssued SMS sent for Lease <id> using template lease_issued_ar ...
```

### 4.3 Verify SQL state

```pwsh
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT TajeerContractNumber, Status, IssuedAtUtc FROM Leases WHERE TajeerContractNumber=<your-number>"
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -I -Q "SELECT ExternalEventId, EventType, SignatureValid, ProcessedAtUtc FROM WebhookLogs ORDER BY ReceivedAtUtc DESC"
```

Expected:
- `Leases.Status = 3` (Active) and `IssuedAtUtc` populated.
- `WebhookLogs` row with `SignatureValid=1` and `ProcessedAtUtc` populated.

### 4.4 If the webhook arrives but signature is invalid

Check `WebhookLogs.SignatureValid`. If `0`, the shared secret you registered with Tajeer doesn't match the one in BFF user-secrets. Either re-register OR update user-secrets + restart BFF.

While `Tajeer:Webhook:LogOnly = true` (default), the row still persists for inspection.

---

## 5. T6.9 — Flip LogOnly = false (1 min)

Once Step 4 succeeds with `SignatureValid=1`:

```pwsh
cd services\bff
dotnet user-secrets set "Tajeer:Webhook:LogOnly" "false"
cd ..\..
```

Restart the BFF. Re-run Steps 2 + 4 to confirm a real second webhook still flips a fresh Lease to Active.

If you now see a `401 Unauthorized` on a webhook attempt, Tajeer changed the secret on their side OR the signature header is missing — investigate before declaring done.

---

## 6. T7.8 — Walk the Done-criteria checklist + record artifacts (5 min)

Open [plan.md §7 Done criteria](./plan.md#7-done-criteria) and tick each box:

- [ ] BFF starts; `/health/liveness` returns 200.
- [ ] `POST /api/v1/dev/save-contract` against real Tajeer staging returns 202 with non-empty `ContractNumber` + `IssuanceUrl`.
- [ ] Tajeer webhook arrives → signature verified → `Lease` row updated to `Active` → `LeaseIssuedDomainEvent` fires → InMemory SMS captured (visible in BFF logs).
- [ ] Audit/log row written; PII masked in all log output:
      ```pwsh
      # Should find NO raw Iqama / national-id numbers in last 500 BFF log lines:
      Select-String -Pattern "\b\d{10}\b" -Path .\services\bff\logs\*.log -Context 0,0
      ```
- [ ] `dotnet build -warnaserror` and `pnpm build` both green.
- [ ] `dotnet test --settings .runsettings` 153/153 green.
- [ ] Integration test exercising the happy path is in `Adapters.Tajeer.Tests`/`Bff.Tests` (already in place via Day-7 E2E test).
- [ ] notes.md captures the raw Tajeer staging request/response (PII masked).

### Recording

Capture screenshots / a short Loom of:
1. The terminal showing the BFF console log sequence (POST → webhook → SMS).
2. The browser showing the issuance URL flow.
3. The sqlcmd query showing `Status=3` + `IssuedAtUtc` populated.
4. The ngrok dashboard showing the inbound webhook hit.

Drop them in a new `screenshots/` subfolder under the workstream so the retrospective can reference them.

---

## 7. After the session — commit + close

```pwsh
git add Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/notes.md `
        Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/plan.md `
        Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/STAGING-SMOKE.md `
        Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/screenshots/

git commit -m "docs(staging): close Week 1 manual ops — T3.7, T5.7/8, T6.7/8/9, T7.8 (PII-masked artifacts)"
git push origin main
```

Update the `week-1-status` memory: replace the "manual-ops pending" line with "Week 1 fully closed YYYY-MM-DD; all done-criteria boxes ticked."

---

## Troubleshooting cheat sheet

| Symptom | Likely cause | Fix |
|---|---|---|
| Smoke test (`Category=Smoke`) early-returns | `Tajeer:AppId` not in user-secrets for the test project | Re-run Step 1.1 in the right folder |
| `dotnet user-secrets set` exits 0 but `list` is empty | UserSecretsId missing in csproj | Already set on both projects; check `<UserSecretsId>` tag |
| BFF `dotnet run` shows `Tajeer:Mode = InMemory` | `appsettings.Development.json` overrides user-secrets when key is present in both | Either remove `Tajeer:Mode` from appsettings or accept user-secrets last-write-wins |
| BFF starts but seeder writes nothing | Customers already exist for that tenant; seeder is idempotent | `DELETE FROM Customers` (cascade not configured, will fail if Leases reference) OR drop + recreate `AutoLeaseNet_Dev` |
| Save returns 503 with `tajeer.network` | Outbound to `tajeer-stg.api.elm.sa` blocked | Check VPN, firewall, ping |
| Webhook arrives at ngrok but BFF returns 404 | URL path wrong | Confirm path is exactly `/api/v1/webhooks/tajeer` (trailing slash optional) |
| Webhook returns 401 even with right secret | Signature whitespace mismatch — secret-key header has stray newline | Re-register URL OR re-set `Tajeer:WebhookSharedSecret` |
| SMS log line says "Customer has no mobile" | The seeded Customer linked to the Lease has `Mobile = null` | Pick a Customer with mobile (every B2C in the seed has one) |
| Webhook fires twice → log shows `duplicate-ignored` second time | Tajeer retried during latency; dedup worked | This is the success path, not a problem |

---

**Estimated total time**: 45 minutes for a clean run; 90 minutes if you hit one or two snags. The runbook is reusable — when Tajeer credentials rotate or you need to re-prove the system end-to-end, just re-run Steps 0–6.
