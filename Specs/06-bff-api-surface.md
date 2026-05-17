# 06 — BFF API Surface (OpenAPI)

**Status**: Draft v0.1
**Phase**: Foundation
**Owner**: Architecture
**Depends on**: [01](./01-multi-tenancy-and-domain-model.md), [02](./02-state-machines-and-sagas.md), [04](./04-integration-architecture.md), [05](./05-monorepo-layout-and-build-system.md)
**Last updated**: 2026-05-17

---

## 1. Purpose

Define the REST API contract between the portals (Next.js apps) and the BFF (.NET service). This document locks:

1. **API conventions** — resource shape, naming, errors, pagination, idempotency.
2. **Authentication & authorization** — JWT validation, claims, permission attribute.
3. **Endpoint catalog** — every Phase 1 endpoint by resource, with summary table.
4. **Key endpoint definitions** — full request/response shapes for the critical few.
5. **Webhook receiver contracts** — what we accept from Tajeer (and later ZATCA, D365).
6. **Versioning** — how we evolve without breaking clients.
7. **OpenAPI file structure** — where the spec lives, how it's generated/validated.

The single source of truth is `packages/contracts/openapi.yaml`. Frontend generates TypeScript types from it; backend asserts the spec matches the implementation via tests.

---

## 2. Principles

| # | Principle | Rationale |
|---|---|---|
| 1 | **REST resource-oriented** | Standard verbs (GET/POST/PUT/PATCH/DELETE), plural nouns, predictable URL structure. |
| 2 | **JSON-only request/response** (except file uploads = multipart) | Consistent parsing; no XML to deal with. |
| 3 | **`camelCase` for JSON properties** | Aligns with JS/TS consumers. C# DTOs use `JsonPropertyName` to convert. |
| 4 | **ISO 8601 dates, UTC** | `2026-05-17T14:30:00Z`. Timezone conversion is presentation-layer. Hijri dates derived in BFF or UI, never stored in API. |
| 5 | **Money as `{ "amount": "1234.56", "currency": "SAR" }`** | String to preserve precision; explicit currency. |
| 6 | **Pagination via `?page=N&pageSize=M` (Phase 1) → cursor (Phase 2)** | Offset is simpler; switch to cursor when result sets exceed 10k. |
| 7 | **Errors as RFC 7807 Problem Details + our extensions** | Standard, machine-readable; supports localized messages. |
| 8 | **Idempotency via `Idempotency-Key` header on POST/PUT** | Client-supplied UUID; cached response for 24h. |
| 9 | **Versioning via URL path: `/api/v1/...`** | Simpler than header negotiation; easy to operate. |
| 10 | **Locale via `Accept-Language: ar` or `en`** | Error messages and any human-readable fields returned in requested language. |
| 11 | **No "god" endpoints** | A resource = a URL. Don't pile RPC-style operations onto one URL. Sub-resources for state transitions (`POST /leases/{id}/extend`). |
| 12 | **Async operations return 202 + status URL** | Long-running operations (Save Contract → Tajeer roundtrip) are async; client polls or subscribes via SignalR. |

---

## 3. Authentication & authorization

### 3.1 Token validation

- Bearer JWT in `Authorization: Bearer <token>` header.
- Issuer:
  - **Internal staff** → Entra ID corporate tenant.
  - **External users (B2B fleet admin, B2C lessee, driver)** → Entra External ID (CIAM tenant).
- Audience: `api://superplexity-bff`.
- Signature validated against IdP's JWKS (cached 1h).
- `exp` enforced; clock skew 30s.

### 3.2 Claims required on every request

Set by IdP on token issuance:

| Claim | Type | Required for | Notes |
|---|---|---|---|
| `sub` | string | All | Stable user ID at IdP |
| `tenant_id` | UUID | All | Resolves to a `Tenant` row |
| `user_type` | string | All | `INTERNAL_STAFF`, `EXTERNAL_FLEET_ADMIN`, `EXTERNAL_DRIVER`, `EXTERNAL_INDIVIDUAL`, `SYSTEM` |
| `customer_id` | UUID | External users only | Scopes data access |
| `branch_ids` | array<UUID> | Internal staff with branch restriction | Empty/missing = all branches |
| `roles` | array<string> | All | Role codes for RBAC |
| `permissions` | array<string> | All | Fine-grained permissions: `lease:create`, `quote:approve:tier-1`, etc. |

### 3.3 Tenancy middleware

`TenancyMiddleware` (BFF) reads JWT claims and sets SQL `SESSION_CONTEXT` per request (see doc 01 §3.4–3.5). No app code touches `TenantId` directly; RLS handles row scoping.

### 3.4 Permission enforcement

```csharp
app.MapPost("/api/v1/leases", [RequirePermission("lease:create")]
    async (CreateLeaseRequest req, ILeaseService svc, CancellationToken ct) => /* ... */);
```

`RequirePermissionAttribute` is an `AuthorizationPolicy` that checks the user's `permissions` claim contains the required permission. Returns 403 if not.

### 3.5 Permission catalog (Phase 1)

```
auth:read                       View own profile
customer:read                   View customers
customer:create / update        Create/edit customers
vehicle:read                    View vehicles
vehicle:update                  Edit vehicle metadata
vehicle:prepare                 Mark vehicle as ready
driver:read / create / update   Driver management
quote:read / create / update    Quotation management
quote:approve:tier-1            Tier 1 approver (junior manager)
quote:approve:tier-2            Tier 2 approver (senior manager)
quote:approve:tier-3            Tier 3 approver (director)
quote:send                      Send quote to customer
lease:read                      View leases
lease:create                    Save new contract → Tajeer
lease:extend / suspend / close / cancel
inspection:create               Perform E-Check
incident:read / create / update
service:read / create
invoice:read                    View invoices
invoice:generate                Trigger invoice creation
invoice:submit-zatca            Force ZATCA submission
payment:create                  Record a payment
document:upload / read
webhook:tajeer:receive          Internal — only used by adapter
admin:users                     Manage users
admin:approval-tiers            Manage approval config
admin:lookups                   Force-refresh Tajeer lookups
```

---

## 4. Common patterns

### 4.1 Success envelope

For single-resource responses:

```json
{
  "id": "8d2f3...uuid",
  "...": "domain fields"
}
```

No `data:` wrapper. Resource is the response body.

### 4.2 List/pagination envelope

```json
{
  "items": [ /* ... resource objects ... */ ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 137,
  "totalPages": 3
}
```

Query params: `?page=1&pageSize=50&sort=createdAt:desc&filter[status]=ACTIVE`.

Default `pageSize=20`, max `pageSize=100`.

### 4.3 Error envelope (RFC 7807 + extensions)

```json
{
  "type": "https://superplexity.io/errors/tajeer/business-error",
  "title": "License has expired",
  "status": 400,
  "detail": "Driver license expired on 2025-09-15. Cannot create contract.",
  "instance": "/api/v1/leases",
  "traceId": "00-abc...-def...-01",

  "errorCode": "TAJEER_LICENSE_EXPIRED",
  "errorCategory": "BusinessRule",
  "fieldErrors": [
    { "field": "primaryDriver.licenseExpiryDate", "code": "EXPIRED", "message": "License expired" }
  ],
  "localizedMessages": {
    "ar": "رخصة القيادة منتهية الصلاحية",
    "en": "Driver license has expired"
  }
}
```

HTTP status codes used:

| Code | Meaning |
|---|---|
| 200 | OK |
| 201 | Created — with `Location` header pointing to new resource |
| 202 | Accepted — async operation in progress; check `Location` for status URL |
| 204 | No Content — successful delete or void-returning operation |
| 400 | Bad request — validation, business rule violation |
| 401 | Unauthorized — missing/invalid token |
| 403 | Forbidden — insufficient permission |
| 404 | Not found |
| 409 | Conflict — version mismatch, vehicle already reserved |
| 422 | Unprocessable Entity — idempotency key reused with different body |
| 429 | Too Many Requests — rate limit |
| 500 | Internal server error |
| 502 | Bad Gateway — Tajeer/ZATCA returned error |
| 503 | Service Unavailable — downstream circuit breaker open |
| 504 | Gateway Timeout — Tajeer/ZATCA timeout |

### 4.4 Idempotency

```
POST /api/v1/leases
Idempotency-Key: 7f3e5...uuid
```

- Required for `POST` on state-changing resources (`leases`, `inspections`, `payments`, `invoices`, etc.).
- Cached for 24h in Redis.
- Same key + same body → cached response.
- Same key + different body → `422 Unprocessable Entity`.

### 4.5 Optimistic concurrency

```
GET /api/v1/leases/{id}
→ ETag: "v17"

PUT /api/v1/leases/{id}
If-Match: "v17"
→ 412 Precondition Failed if version differs
```

ETag value = `Lease.RowVersion` (SQL Server `ROWVERSION` column) base64-encoded.

### 4.6 File upload

`multipart/form-data` for documents and photos:

```
POST /api/v1/inspections/{id}/photos
Content-Type: multipart/form-data
Authorization: Bearer ...

--boundary
Content-Disposition: form-data; name="metadata"
Content-Type: application/json

{ "sequence": 1, "description": "Front damage" }
--boundary
Content-Disposition: form-data; name="file"; filename="photo.jpg"
Content-Type: image/jpeg

<binary>
--boundary--
```

Max single-upload: 10 MB. Larger files: chunked upload (Phase 2 if needed).

### 4.7 Async operations

```
POST /api/v1/leases
Idempotency-Key: ...

→ 202 Accepted
   Location: /api/v1/leases/{id}/status

GET /api/v1/leases/{id}/status
→ {
    "leaseId": "...",
    "status": "PENDING_ISSUANCE",
    "tajeerContractNumber": 2100211102671,
    "issuanceUrl": "https://tajeerstg.logisti.sa/#/public-contract/...",
    "smsSentAt": "2026-05-17T14:30:01Z",
    "expiresAt": "2026-05-18T02:30:01Z"
   }
```

Optionally, frontend subscribes to SignalR hub `/hubs/leases/{id}` for push updates.

### 4.8 Filtering

```
GET /api/v1/leases?filter[status]=ACTIVE&filter[customerId]=xxx&filter[createdAt:gte]=2026-01-01
```

Operators supported in Phase 1: `eq` (default), `ne`, `gt`, `gte`, `lt`, `lte`, `in` (comma-separated values).

### 4.9 Sorting

```
GET /api/v1/leases?sort=createdAt:desc,status:asc
```

Multi-field allowed; max 3 fields.

### 4.10 Field selection (Phase 2)

```
GET /api/v1/leases?fields=id,status,vehicle.plateNumber
```

Skip for Phase 1 — return full resource by default.

---

## 5. Endpoint catalog (Phase 1)

### 5.1 Auth & Profile

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/auth/me` | Current user profile + claims | (authenticated) |
| POST | `/api/v1/auth/logout` | Server-side session invalidation if any | (authenticated) |
| POST | `/api/v1/auth/switch-tenant` | Switch active tenant for users in multiple | (authenticated) |

### 5.2 Customers

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/customers` | List (filterable) | `customer:read` |
| GET | `/api/v1/customers/{id}` | Get one | `customer:read` |
| POST | `/api/v1/customers` | Create | `customer:create` |
| PUT | `/api/v1/customers/{id}` | Update | `customer:update` |
| GET | `/api/v1/customers/{id}/vehicles` | Customer's fleet | `vehicle:read` |
| GET | `/api/v1/customers/{id}/drivers` | Customer's drivers | `driver:read` |
| GET | `/api/v1/customers/{id}/leases` | Customer's leases | `lease:read` |
| GET | `/api/v1/customers/{id}/invoices` | Customer's invoices | `invoice:read` |

### 5.3 Vehicles

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/vehicles` | List (filterable by status, branch, customer, plate) | `vehicle:read` |
| GET | `/api/v1/vehicles/{id}` | Get vehicle 360° | `vehicle:read` |
| POST | `/api/v1/vehicles/search-by-plate` | Find vehicle by plate (Tajeer-style) | `vehicle:read` |
| PUT | `/api/v1/vehicles/{id}` | Update metadata | `vehicle:update` |
| POST | `/api/v1/vehicles/{id}/prep` | Start preparation | `vehicle:prepare` |
| PUT | `/api/v1/vehicles/{id}/prep/{prepId}` | Update prep record | `vehicle:prepare` |
| POST | `/api/v1/vehicles/{id}/prep/{prepId}/complete` | Mark prep done → status=READY | `vehicle:prepare` |
| GET | `/api/v1/vehicles/{id}/status-history` | Audit trail | `vehicle:read` |
| POST | `/api/v1/vehicles/{id}/attach-to-customer` | Assign to customer | `vehicle:update` |

### 5.4 Drivers

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/drivers` | List | `driver:read` |
| GET | `/api/v1/drivers/{id}` | Get one | `driver:read` |
| POST | `/api/v1/drivers` | Create | `driver:create` |
| PUT | `/api/v1/drivers/{id}` | Update | `driver:update` |
| POST | `/api/v1/drivers/{id}/validate` | Pre-flight check before assignment (license expiry, etc.) | `driver:read` |
| POST | `/api/v1/drivers/{id}/suspend` | Suspend | `driver:update` |

### 5.5 Quotations

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/quotations` | List | `quote:read` |
| GET | `/api/v1/quotations/{id}` | Get one + approval chain | `quote:read` |
| POST | `/api/v1/quotations` | Create (DRAFT) | `quote:create` |
| PUT | `/api/v1/quotations/{id}` | Edit (DRAFT only) | `quote:update` |
| POST | `/api/v1/quotations/{id}/lines` | Add line | `quote:update` |
| PUT | `/api/v1/quotations/{id}/lines/{lineId}` | Update line | `quote:update` |
| DELETE | `/api/v1/quotations/{id}/lines/{lineId}` | Remove line | `quote:update` |
| POST | `/api/v1/quotations/{id}/submit` | Submit for approval | `quote:create` |
| POST | `/api/v1/quotations/{id}/withdraw` | Withdraw | `quote:update` |
| POST | `/api/v1/quotations/{id}/send` | Send to customer (after APPROVED) | `quote:send` |
| GET | `/api/v1/quotations/{id}/pdf` | Generated quote PDF | `quote:read` |

### 5.6 Approvals (the workflow engine surface)

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/approvals/pending` | Current user's pending approvals across all quote/discount/etc. | (authenticated) |
| GET | `/api/v1/approvals/{id}` | Approval detail | (authenticated) |
| POST | `/api/v1/approvals/{id}/decide` | Approve or reject | `quote:approve:tier-N` (tier-aware) |
| POST | `/api/v1/approvals/{id}/reassign` | Reassign to another user (admin only) | `admin:approvals` |

### 5.7 Leases (the heart)

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/leases` | List | `lease:read` |
| GET | `/api/v1/leases/{id}` | Get one + events | `lease:read` |
| POST | `/api/v1/leases/save` | Save contract → Tajeer (async) | `lease:create` |
| GET | `/api/v1/leases/{id}/status` | Status polling endpoint for async save | `lease:read` |
| POST | `/api/v1/leases/{id}/issuance-link/resend` | Re-dispatch SMS link | `lease:create` |
| POST | `/api/v1/leases/{id}/extend` | Extend (async to Tajeer) | `lease:extend` |
| POST | `/api/v1/leases/{id}/suspend` | Suspend (async) | `lease:suspend` |
| POST | `/api/v1/leases/{id}/close` | Close with check-in (async) | `lease:close` |
| POST | `/api/v1/leases/{id}/cancel` | Cancel (pre-issuance) | `lease:cancel` |
| GET | `/api/v1/leases/{id}/pdf?type=full|summary` | Tajeer-rendered PDF | `lease:read` |
| POST | `/api/v1/leases/{id}/calculate-payment` | Preview damages / fees before close | `lease:read` |
| GET | `/api/v1/leases/{id}/events` | Lease audit trail | `lease:read` |

### 5.8 Inspections (E-Check)

| Method | Path | Purpose | Permission |
|---|---|---|---|
| POST | `/api/v1/inspections` | Create (CHECK_OUT, CHECK_IN, etc.) | `inspection:create` |
| GET | `/api/v1/inspections/{id}` | Get one | `inspection:create` |
| PUT | `/api/v1/inspections/{id}` | Update (only while IN_PROGRESS) | `inspection:create` |
| POST | `/api/v1/inspections/{id}/photos` | Upload photo | `inspection:create` |
| POST | `/api/v1/inspections/{id}/complete` | Mark COMPLETED + lock | `inspection:create` |
| GET | `/api/v1/inspections/{id}/photos/{photoId}` | Get photo blob URL (SAS) | `inspection:create` |

### 5.9 Incidents

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/incidents` | List | `incident:read` |
| GET | `/api/v1/incidents/{id}` | Get one | `incident:read` |
| POST | `/api/v1/incidents` | Report incident | `incident:create` |
| PUT | `/api/v1/incidents/{id}` | Update | `incident:update` |
| POST | `/api/v1/incidents/{id}/trigger-replacement` | Start vehicle replacement saga | `lease:create` |

### 5.10 Service bookings

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/service-bookings` | List | `service:read` |
| POST | `/api/v1/service-bookings` | Schedule | `service:create` |
| PUT | `/api/v1/service-bookings/{id}` | Update | `service:create` |
| POST | `/api/v1/service-bookings/{id}/start` | Mark IN_PROGRESS | `service:create` |
| POST | `/api/v1/service-bookings/{id}/complete` | Mark COMPLETED | `service:create` |
| POST | `/api/v1/service-bookings/{id}/cancel` | Cancel | `service:create` |

### 5.11 Invoices & payments

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/invoices` | List | `invoice:read` |
| GET | `/api/v1/invoices/{id}` | Get one | `invoice:read` |
| POST | `/api/v1/invoices` | Generate invoice (e.g. manual) | `invoice:generate` |
| GET | `/api/v1/invoices/{id}/pdf` | Generated PDF | `invoice:read` |
| GET | `/api/v1/invoices/{id}/ubl-xml` | ZATCA-signed XML | `invoice:read` |
| POST | `/api/v1/invoices/{id}/submit-zatca` | Force re-submission to ZATCA | `invoice:submit-zatca` |
| GET | `/api/v1/invoices/{id}/zatca-status` | Submission status + ZATCA UUID | `invoice:read` |
| POST | `/api/v1/invoices/{id}/void` | Void (DRAFT only) | `invoice:generate` |
| GET | `/api/v1/payments` | List | `payment:create` |
| POST | `/api/v1/payments` | Record | `payment:create` |

### 5.12 Documents

| Method | Path | Purpose | Permission |
|---|---|---|---|
| POST | `/api/v1/documents` | Upload (multipart) | `document:upload` |
| GET | `/api/v1/documents/{id}` | Get metadata + signed URL | `document:read` |
| DELETE | `/api/v1/documents/{id}` | Soft-delete | `document:upload` |

### 5.13 Lookups (cached from Tajeer)

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET | `/api/v1/lookups/rent-policies` | Active rent policies | `lease:read` |
| GET | `/api/v1/lookups/branches` | Tenant's branches | `vehicle:read` |
| GET | `/api/v1/lookups/extended-coverages` | Extended coverage options | `lease:read` |
| GET | `/api/v1/lookups/payment-methods` | | `lease:read` |
| GET | `/api/v1/lookups/closure-reasons` | | `lease:close` |
| GET | `/api/v1/lookups/{type}` | Generic Tajeer lookup (id-type, fuel-type, etc.) | `lease:read` |
| POST | `/api/v1/lookups/refresh` | Force re-fetch from Tajeer (admin) | `admin:lookups` |

### 5.14 Webhooks (inbound from external systems)

| Method | Path | Purpose | Auth |
|---|---|---|---|
| POST | `/api/v1/webhooks/tajeer` | Tajeer event notifications | `secret-key` header |
| POST | `/api/v1/webhooks/zatca` | Reserved — ZATCA may add webhooks | (TBD) |
| POST | `/api/v1/webhooks/payments/{provider}` | Phase 2 payment gateway callbacks | provider signature |
| POST | `/api/v1/webhooks/d365/{system}` | Phase 2 D365 → us notifications | shared secret |

### 5.15 Health & ops

| Method | Path | Purpose | Auth |
|---|---|---|---|
| GET | `/health/liveness` | App alive | none |
| GET | `/health/readiness` | App ready (DB + Redis reachable) | none |
| GET | `/health/integrations` | All integration health checks (auth required) | `admin:health` |
| GET | `/metrics` | Prometheus-format metrics | none (internal scrape) |

### 5.16 Admin (settings)

| Method | Path | Purpose | Permission |
|---|---|---|---|
| GET / POST / PUT / DELETE | `/api/v1/admin/users` | User management | `admin:users` |
| GET / PUT | `/api/v1/admin/approval-tiers` | Approval threshold config | `admin:approval-tiers` |
| GET / PUT | `/api/v1/admin/rent-policies` | Local rent policy management | `admin:lookups` |

---

## 6. Key endpoint definitions (the critical ones)

### 6.1 `POST /api/v1/leases/save` — the heart

**Request**

```http
POST /api/v1/leases/save
Authorization: Bearer <token>
Idempotency-Key: 7f3e5...uuid
Accept-Language: ar
Content-Type: application/json

{
  "customerId": "uuid",
  "vehicleId": "uuid",
  "primaryDriverId": "uuid",
  "contractType": "DAILY",
  "contractStartUtc": "2026-05-18T07:00:00Z",
  "contractEndUtc": "2026-05-25T07:00:00Z",
  "workingBranchId": "uuid",
  "receiveBranchId": "uuid",
  "returnBranchId": "uuid",
  "rentPolicyId": "uuid",
  "extendedCoverageId": "uuid|null",
  "extraDriverId": "uuid|null",
  "allowedKmPerDay": 250,
  "allowedKmPerHour": null,
  "unlimitedKm": false,
  "allowedLateHours": 3,
  "payment": {
    "rentDayCost": { "amount": "150.00", "currency": "SAR" },
    "extraKmCost": { "amount": "0.50", "currency": "SAR" },
    "fullFuelCost": { "amount": "200.00", "currency": "SAR" },
    "additionalCoverageCost": { "amount": "30.00", "currency": "SAR" },
    "vehicleTransferCost": { "amount": "0.00", "currency": "SAR" },
    "discount": "10.00",
    "paid": { "amount": "500.00", "currency": "SAR" },
    "paymentMethodCode": 1,
    "enduranceAmount": { "amount": "1000.00", "currency": "SAR" }
  },
  "checkOutInspectionId": "uuid",
  "authorization": {
    "type": "INTERNAL",
    "externalCountries": null,
    "endUtc": null
  }
}
```

**Response — 202 Accepted (async)**

```http
HTTP/1.1 202 Accepted
Location: /api/v1/leases/9f8e7...uuid/status

{
  "leaseId": "9f8e7...uuid",
  "status": "DRAFT",
  "statusUrl": "/api/v1/leases/9f8e7...uuid/status"
}
```

**Response — 400 (business error from Tajeer)**

```json
{
  "type": "https://superplexity.io/errors/tajeer/business-error",
  "title": "Vehicle has an active contract",
  "status": 400,
  "detail": "Cannot save contract — vehicle already has an active or pending contract.",
  "errorCode": "TAJEER_CURRENT_ACTIVE_CONTRACT_EXIST",
  "errorCategory": "Conflict",
  "localizedMessages": {
    "ar": "يوجد عقد ساري حالياً على هذه المركبة",
    "en": "An active contract already exists for this vehicle"
  }
}
```

### 6.2 `GET /api/v1/leases/{id}/status` — async status polling

```json
{
  "leaseId": "9f8e7...uuid",
  "status": "PENDING_ISSUANCE",
  "tajeerContractNumber": 2100211102671,
  "tajeerToken": "f43df0eb-50f6-4625-a32d-97696a08a7db",
  "issuanceUrl": "https://tajeerstg.logisti.sa/#/public-contract/2100211102671/f43df0eb-50f6-4625-a32d-97696a08a7db",
  "smsSent": {
    "to": "+966581823199",
    "sentAt": "2026-05-17T14:30:01Z",
    "providerMessageId": "u_msg_xyz"
  },
  "expiresAt": "2026-05-18T02:30:01Z",
  "payment": {
    "main": { "paid": "89.65", "remaining": "748.70", "total": "838.35", "vat": "109.35" },
    "other": { "paid": "10.35", "remaining": "0.00", "total": "10.35", "vat": "1.35" },
    "totals": { "paid": "100.00", "remaining": "748.70", "total": "848.70", "vat": "110.70" }
  }
}
```

### 6.3 `POST /api/v1/inspections` — E-Check creation

```http
POST /api/v1/inspections
Authorization: Bearer <token>
Idempotency-Key: ...
Content-Type: application/json

{
  "vehicleId": "uuid",
  "leaseId": "uuid|null",
  "type": "CHECK_OUT",
  "odometerKm": 18250,
  "fuelLevel": "FULL",
  "ac": "EXCELLENT",
  "radioStereo": "EXCELLENT",
  "screen": "GOOD",
  "speedometer": "WORKING",
  "carSeats": "CLEAN",
  "tires": "EXCELLENT",
  "spareTire": "GOOD",
  "spareTireTools": "AVAILABLE",
  "firstAidKit": "AVAILABLE",
  "keys": "WORKING",
  "fireExtinguisher": "AVAILABLE",
  "safetyTriangle": "AVAILABLE",
  "other1": null,
  "other2": null,
  "notes": "Customer notified about minor scratch on rear bumper.",
  "damageMarkers": [
    { "type": "SMALL_SCRATCH", "x": 769.83, "y": 119.62 },
    { "type": "BEND_IN_BODY", "x": 151.83, "y": 312.62 }
  ]
}
```

Response:

```json
{
  "id": "uuid",
  "vehicleId": "uuid",
  "leaseId": "uuid",
  "type": "CHECK_OUT",
  "status": "IN_PROGRESS",
  "performedAtUtc": "2026-05-17T07:15:23Z",
  "performedByUserId": "uuid",
  "photoUploadUrl": "/api/v1/inspections/{id}/photos"
}
```

### 6.4 `POST /api/v1/webhooks/tajeer` — inbound webhook receiver

```http
POST /api/v1/webhooks/tajeer
secret-key: <shared-secret>
Content-Type: application/json

{
  "id": "notif_982374",
  "timestamp": "2026-05-17T14:35:00",
  "category": "contract",
  "type": "contract.create",
  "referenceId": "2569450000400015",
  "message": "Contract 2569450000400015 is created."
}
```

Response: always `200 OK` (after dedup + persist). Processing is async.

### 6.5 `POST /api/v1/quotations/{id}/submit` — submit for approval

```http
POST /api/v1/quotations/{id}/submit
Authorization: Bearer <token>
Idempotency-Key: ...

(no body)
```

Response:

```json
{
  "id": "uuid",
  "status": "PENDING_APPROVAL",
  "totalSar": "125000.00",
  "approvalChain": [
    { "tierLevel": 1, "requiredRole": "sales-manager", "status": "PENDING", "assignedUserId": null },
    { "tierLevel": 2, "requiredRole": "regional-director", "status": "PENDING", "assignedUserId": null }
  ]
}
```

### 6.6 `POST /api/v1/approvals/{id}/decide`

```http
POST /api/v1/approvals/{id}/decide
Authorization: Bearer <token>
Idempotency-Key: ...
Content-Type: application/json

{
  "decision": "APPROVED",
  "comment": "Looks good, approved."
}
```

Response:

```json
{
  "approvalId": "uuid",
  "quotationId": "uuid",
  "tierLevel": 1,
  "status": "APPROVED",
  "decidedAt": "2026-05-17T15:00:00Z",
  "quotationStatus": "PENDING_APPROVAL",   // still pending tier 2
  "nextTier": { "tierLevel": 2, "requiredRole": "regional-director" }
}
```

---

## 7. Versioning

- All endpoints under `/api/v1/`.
- Adding fields to responses = non-breaking; adding optional fields to requests = non-breaking.
- Removing fields, changing types, changing required-ness = breaking → new version path.
- New version (`/api/v2/`) coexists with v1 for at least 6 months before deprecation.
- Deprecated endpoints return `Sunset` header with retirement date.

---

## 8. OpenAPI authoring

### 8.1 Where

`packages/contracts/openapi.yaml` — manually authored, single file (split when >5k lines).

### 8.2 Frontend consumption

```bash
pnpm --filter @superplexity/contracts generate
```

Generates:
- `packages/contracts/generated/schema.d.ts` (via `openapi-typescript`)
- `packages/contracts/generated/client.ts` (via `openapi-fetch`)

Both portals import from `@superplexity/contracts` for typed API calls.

### 8.3 Backend validation

In CI:

```bash
dotnet test --filter "Category=ContractCheck"
```

Test reads OpenAPI spec, calls each documented endpoint with example payloads, asserts implementation matches.

Tools:
- [`Microsoft.OpenApi.NET`](https://github.com/microsoft/OpenAPI.NET) for spec parsing.
- Snapshot tests (Verify.NET) against `swagger.json` produced by Swashbuckle to detect drift.

### 8.4 Skeleton

```yaml
openapi: 3.1.0
info:
  title: AutoLeaseNet BFF API
  version: 1.0.0
  description: Backend-for-Frontend serving the Web Portal and Customer Portal.
servers:
  - url: https://api.superplexity.io/api/v1
    description: Production
  - url: https://api-staging.superplexity.io/api/v1
    description: Staging
  - url: http://localhost:5000/api/v1
    description: Local dev
security:
  - BearerAuth: []
components:
  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
  schemas:
    Money:
      type: object
      required: [amount, currency]
      properties:
        amount: { type: string, pattern: "^[0-9]+\\.[0-9]{2}$" }
        currency: { type: string, enum: [SAR] }
    ProblemDetails:
      type: object
      required: [type, title, status]
      properties:
        type: { type: string, format: uri }
        title: { type: string }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        traceId: { type: string }
        errorCode: { type: string }
        errorCategory: { type: string }
        fieldErrors:
          type: array
          items:
            type: object
            properties:
              field: { type: string }
              code: { type: string }
              message: { type: string }
        localizedMessages:
          type: object
          properties:
            ar: { type: string }
            en: { type: string }
    # ... (Customer, Vehicle, Lease, Quotation, Inspection, Invoice, etc.)
  parameters:
    PageParam: { name: page, in: query, schema: { type: integer, default: 1, minimum: 1 } }
    PageSizeParam: { name: pageSize, in: query, schema: { type: integer, default: 20, minimum: 1, maximum: 100 } }
    IdempotencyKey: { name: Idempotency-Key, in: header, required: false, schema: { type: string, format: uuid } }
    IfMatch: { name: If-Match, in: header, required: false, schema: { type: string } }
paths:
  /leases/save:
    post:
      summary: Save a new lease contract (async to Tajeer)
      operationId: saveLease
      parameters:
        - $ref: '#/components/parameters/IdempotencyKey'
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SaveLeaseRequest' }
      responses:
        '202':
          description: Accepted; check status URL
          headers:
            Location: { schema: { type: string }, description: Status polling URL }
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LeaseSaveAcceptedResponse' }
        '400':
          description: Business error from Tajeer or validation
          content:
            application/problem+json:
              schema: { $ref: '#/components/schemas/ProblemDetails' }
        '409':
          description: Vehicle reserved or conflict
          content:
            application/problem+json:
              schema: { $ref: '#/components/schemas/ProblemDetails' }
  # ... (every other endpoint)
```

---

## 9. Rate limiting

Phase 1 — APIM rules:

| Tier | Endpoint pattern | Limit |
|---|---|---|
| Public webhook | `POST /webhooks/*` | 100 req/min per source IP |
| Authenticated read | `GET /*` | 600 req/min per user |
| Authenticated write | `POST/PUT/DELETE /*` | 60 req/min per user |
| Admin | `*/admin/*` | 30 req/min per user |
| Health | `GET /health/*` | unlimited |

Exceeded → `429 Too Many Requests` with `Retry-After` header.

---

## 10. Telemetry per endpoint

Every request emits:

- App Insights request telemetry (auto-instrumented)
- Custom dimensions: `tenant.id`, `user.id`, `user.type`, `customer.id` (if applicable)
- Outbound integration calls captured by adapter (doc 03 §14)
- Slow requests (>2s) auto-flagged

---

## 11. Open questions

| # | Question | Default |
|---|---|---|
| Q1 | SignalR for real-time push, or polling only Phase 1? | Polling Phase 1 — SignalR adds infra; only worth it if user feedback demands it |
| Q2 | GraphQL alongside REST? | No — adds complexity; REST is sufficient |
| Q3 | API versioning strategy if we need v2 quickly? | URL path (`/api/v2/`); coexist with v1 for 6 months minimum |
| Q4 | OpenAPI spec authoring — by hand or code-first via Swashbuckle? | Hand-authored single file; Swashbuckle output used for *validation* of implementation parity in tests |
| Q5 | Support B2B-customer access to subset of admin endpoints? | No — admin endpoints are internal only; customer-portal calls are non-admin |
| Q6 | Webhook receivers under `/api/v1/` or separate `/webhooks/` root path? | Under `/api/v1/webhooks/` — keeps versioning consistent |

---

## 12. Sign-off checklist

- [ ] REST conventions approved
- [ ] Permission catalog approved
- [ ] Endpoint catalog (Phase 1) approved as complete
- [ ] Common envelope shapes (success/list/error) approved
- [ ] Idempotency + ETag patterns approved
- [ ] Async-with-202 pattern for long-running ops approved
- [ ] OpenAPI source-of-truth approach approved
- [ ] Versioning strategy approved
- [ ] Open questions §11 answered
