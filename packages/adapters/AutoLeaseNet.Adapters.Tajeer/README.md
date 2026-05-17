# AutoLeaseNet.Adapters.Tajeer

> Tajeer (KSA Transport General Authority — unified vehicle leasing registry) — Pattern B adapter.

## Reference

- **Vendor docs**: Tajeer Integration Guide V9.7 (Elm, 18/12/2025)
- **Design doc**: [`docs/03-tajeer-adapter-design.md`](../../../docs/03-tajeer-adapter-design.md)
- **Onboarding**: [Rabet portal](https://tajeer.logisti.sa) for credentials
- **Support**: thd@logisti.sa · 19929

## Endpoints

- **Staging**: `https://tajeer-stg.api.elm.sa`
- **Production**: `https://tajeer.api.elm.sa`
- **Issuance URL**: `{IssuanceUrlBase}/#/public-contract/{contractNumber}/{token}`

## Known quirks

- Save Contract is async-ish — returns `contractNumber + token` but the contract isn't `ISSUED` until the renter completes on Tajeer's web page and Tajeer pushes a webhook.
- Per v9.4, OTP entry is now on the renter side via Tajeer's site (no longer handled by rental office).
- Saved-but-not-issued contracts are **auto-cancelled by Tajeer after 12 hours**.
- `200 OK` responses can include `errorKey` in the body — treat as business error regardless of HTTP status.
- Plate characters are transitioning: `أ → ا`, `ي → ى`. Use new chars on writes; prefer `newPlateNumber` on reads.
- `enduranceAmount` is frozen after first save — modifications return error code 316.
- Max 25 extensions per contract.
- The `addtionalServices` typo in the spec is intentional — match it on writes.

## Configuration

See [`Configuration/TajeerOptions.cs`](./Configuration/TajeerOptions.cs).

Per-tenant credentials in Key Vault:
- `tajeer-{tenantId}-app-id`
- `tajeer-{tenantId}-app-key`
- `tajeer-{tenantId}-authorization`

## Usage

```csharp
services.AddTajeer(builder.Configuration.GetSection("Tajeer"));

// In a handler:
var result = await tajeerClient.Contracts.SaveAsync(request, idempotencyKey, ct);
```

## Companion packages

- [`AutoLeaseNet.Adapters.Tajeer.InMemory`](../AutoLeaseNet.Adapters.Tajeer.InMemory/) — fake for tests/dev.
- `AutoLeaseNet.Adapters.Tajeer.Tests` — unit + contract snapshot + sandbox integration tests.
