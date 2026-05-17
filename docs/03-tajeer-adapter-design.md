# 03 — Tajeer Adapter: Interface, State Mapping & Resilience

**Status**: Draft v0.1
**Phase**: Foundation (locks before Week 1 coding)
**Owner**: Architecture / Integration
**Depends on**: [01-multi-tenancy-and-domain-model.md](./01-multi-tenancy-and-domain-model.md), [02-state-machines-and-sagas.md](./02-state-machines-and-sagas.md), [04-integration-architecture.md](./04-integration-architecture.md) (this doc is the canonical **Pattern B** example of the integration standard)
**Reference**: Tajeer Integration Guide V9.7 (Elm, 18/12/2025)
**Last updated**: 2026-05-17

---

## 1. Purpose

This document is the build spec for the **Tajeer adapter library** — the .NET 8 package that every other part of our system uses to talk to Tajeer. It locks down:

1. **Adapter design principles** — what the adapter does and doesn't do.
2. **Project structure** — where the code lives in the monorepo.
3. **Authentication & configuration** — headers, secrets, environments.
4. **Public C# interface** — the typed API our BFF/services call.
5. **DTO design** — request/response POCOs with versioning notes.
6. **State mapping** — Tajeer `contractStatusCode` ↔ our `Lease.Status`.
7. **Error mapping** — Tajeer's 300+ `errorKey`s → our domain errors → Arabic/English user messages.
8. **Resilience policies** — retry, circuit breaker, timeout (Polly).
9. **Idempotency wrapper** — preventing duplicate Save/Close/Extend calls.
10. **Webhook receiver** — security, dedup, processing.
11. **Lookup caching** — Redis strategy + invalidation.
12. **Helpers** — plate char conversion, Hijri dates, sketch JSON.
13. **Testing strategy** — unit + sandbox integration + contract snapshots.

This document **is** the spec — when the implementer (me, working with Claude Code) builds the adapter, this is the contract.

---

## 2. Design principles

| # | Principle | Rationale |
|---|---|---|
| 1 | **Adapter is a pure I/O layer — no business logic** | The adapter knows how to talk to Tajeer. Domain rules (when to suspend vs close, whether to retry, etc.) live in domain services that *use* the adapter. |
| 2 | **Typed all the way down — no `JObject`/`Dictionary<string,object>`** | Every Tajeer request/response is a strongly-typed C# record. Schema changes are compile-time errors. |
| 3 | **Returns `TajeerResult<T>`, not exceptions for known business errors** | Business rule failures (license expired, vehicle in another contract) are first-class result values, not exceptions. Exceptions reserved for network/auth/programmer errors. |
| 4 | **Idempotency enforced at adapter layer, not just by callers** | The adapter checks "have I already done this for this aggregate?" before calling Tajeer. Defense in depth against caller bugs. |
| 5 | **All Tajeer calls go through Polly pipeline** | Retry, circuit breaker, timeout, bulkhead — applied consistently, not per-call. |
| 6 | **Observability is mandatory** | Every call emits structured log + OpenTelemetry span + integration log row. Operators can answer "what did we send Tajeer at 14:32?" in one query. |
| 7 | **Sandbox vs prod is a configuration toggle** | Same code, different `TajeerOptions.BaseUrl` + credentials. No `#if SANDBOX` in source. |
| 8 | **Per-tenant credentials** | Tajeer auth is per-tenant (each leasing company has its own Rabet creds). Adapter resolves credentials by `TenantId` at call time. |
| 9 | **Tolerant reader — strict writer** | Deserialize Tajeer responses leniently (ignore unknown fields). Serialize our requests strictly (validate before send). |
| 10 | **State mapping is centralized, not scattered** | One `TajeerStatusMapper` class maps `contractStatusCode` → `Lease.Status`. Used everywhere, tested exhaustively. |

---

## 3. Project structure

In the monorepo (`Turborepo` + .NET solution):

```
AutoLeaseNet/
├── apps/
│   ├── web-portal/                      # Next.js
│   └── customer-portal/                 # Next.js
├── services/
│   └── bff/                             # .NET 8 minimal API
│       └── AutoLeaseNet.Bff.csproj
└── packages/
    ├── ui/                              # Shared React components
    └── adapters/
        ├── AutoLeaseNet.Adapters.Tajeer/
        │   ├── AutoLeaseNet.Adapters.Tajeer.csproj
        │   ├── Configuration/
        │   │   └── TajeerOptions.cs
        │   ├── Authentication/
        │   │   ├── ITajeerCredentialProvider.cs
        │   │   ├── KeyVaultCredentialProvider.cs
        │   │   └── TajeerAuthHandler.cs
        │   ├── Client/
        │   │   ├── ITajeerClient.cs
        │   │   ├── TajeerClient.cs
        │   │   └── SubInterfaces/
        │   │       ├── ITajeerContracts.cs
        │   │       ├── ITajeerLookups.cs
        │   │       ├── ITajeerWebhookRegistration.cs
        │   │       └── ITajeerExecution.cs
        │   ├── Contracts/                # Request/response DTOs
        │   │   ├── Save/
        │   │   ├── Get/
        │   │   ├── Close/
        │   │   ├── Extend/
        │   │   ├── Suspend/
        │   │   ├── Cancel/
        │   │   ├── Validate/
        │   │   ├── Calculate/
        │   │   ├── UpdatePaidAmount/
        │   │   └── Execution/
        │   ├── Lookups/
        │   │   ├── RentPolicyDto.cs
        │   │   ├── BranchDto.cs
        │   │   ├── ExtendedCoverageDto.cs
        │   │   └── ... (one per lookup endpoint)
        │   ├── Webhooks/
        │   │   ├── TajeerWebhookPayload.cs
        │   │   ├── TajeerEventCategory.cs
        │   │   ├── TajeerEventType.cs
        │   │   └── WebhookSignatureValidator.cs
        │   ├── ErrorHandling/
        │   │   ├── TajeerResult.cs            # discriminated union
        │   │   ├── TajeerError.cs
        │   │   ├── TajeerErrorCode.cs         # enum
        │   │   ├── TajeerErrorCatalog.cs      # 300+ mappings
        │   │   └── TajeerErrorMapper.cs
        │   ├── Resilience/
        │   │   ├── TajeerResiliencePipeline.cs # Polly v8 ResiliencePipeline
        │   │   └── TajeerHttpPolicies.cs
        │   ├── Idempotency/
        │   │   ├── IIdempotencyStore.cs
        │   │   ├── RedisIdempotencyStore.cs
        │   │   └── IdempotentTajeerClient.cs   # decorator
        │   ├── Helpers/
        │   │   ├── PlateNumberConverter.cs
        │   │   ├── HijriDateConverter.cs
        │   │   ├── SketchInfoBuilder.cs
        │   │   └── TajeerDateTimeConverter.cs  # KSA timezone, Tajeer date format
        │   ├── StateMapping/
        │   │   ├── TajeerStatusMapper.cs
        │   │   ├── TajeerClosureReasonMapper.cs
        │   │   └── TajeerSuspensionReasonMapper.cs
        │   ├── Caching/
        │   │   ├── ITajeerLookupCache.cs
        │   │   └── RedisLookupCache.cs
        │   ├── Observability/
        │   │   ├── TajeerTelemetry.cs           # OpenTelemetry source
        │   │   └── TajeerLoggingHandler.cs      # HttpMessageHandler for full request/response logging
        │   └── ServiceCollectionExtensions.cs    # AddTajeerAdapter(...)
        ├── AutoLeaseNet.Adapters.Tajeer.Tests/
        │   ├── Unit/
        │   │   ├── ErrorMappingTests.cs
        │   │   ├── PlateNumberConverterTests.cs
        │   │   ├── HijriDateConverterTests.cs
        │   │   ├── SketchInfoBuilderTests.cs
        │   │   ├── StatusMapperTests.cs
        │   │   └── IdempotencyDecoratorTests.cs
        │   ├── Contract/
        │   │   ├── SaveContractSnapshotTests.cs   # Verify-based snapshot tests
        │   │   └── GetContractSnapshotTests.cs
        │   └── Integration/
        │       └── TajeerSandboxTests.cs          # Hits real staging API, [Trait("Integration","Tajeer")]
        ├── AutoLeaseNet.Adapters.Zatca/
        ├── AutoLeaseNet.Adapters.Unifonic/
        └── AutoLeaseNet.Adapters.Entra/
```

**Package boundaries**:

- `AutoLeaseNet.Adapters.Tajeer` depends only on:
  - `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging`
  - `Polly` v8 (Resilience Pipelines)
  - `StackExchange.Redis` (for idempotency + lookup cache)
  - `Azure.Security.KeyVault.Secrets` (for credentials)
  - `System.Text.Json`
- Does **not** depend on: BFF, domain entities, EF Core, any business library.
- Other code consumes via DI: `services.AddTajeerAdapter(config)`.

---

## 4. Configuration & authentication

### 4.1 `TajeerOptions`

```csharp
public sealed class TajeerOptions
{
    /// <summary>Base URL. Staging: https://tajeer-stg.api.elm.sa, Prod: https://tajeer.api.elm.sa</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Issuance URL host. Staging: https://tajeerstg.logisti.sa, Prod: https://tajeer.logisti.sa</summary>
    public required string IssuanceUrlBase { get; init; }

    /// <summary>HTTP request timeout. Default 30s.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Webhook shared secret used to verify inbound webhooks.</summary>
    public required string WebhookSharedSecret { get; init; }

    /// <summary>Whether this is a sandbox environment (for telemetry tagging).</summary>
    public bool IsSandbox { get; init; }
}
```

Bound from `appsettings.json` per environment:

```json
"Tajeer": {
  "BaseUrl": "https://tajeer-stg.api.elm.sa",
  "IssuanceUrlBase": "https://tajeerstg.logisti.sa",
  "RequestTimeout": "00:00:30",
  "WebhookSharedSecret": "<from KeyVault: tajeer-webhook-secret>",
  "IsSandbox": true
}
```

### 4.2 Per-tenant credentials

Per Tajeer §4 and §8.3, the three headers required on every request are tenant-scoped:

- `app-id` (from Rabet)
- `app-key` (from Rabet)
- `Authorization` (generated via Tajeer portal `/users/apiUser`)

These are per-leasing-company. Stored in Key Vault, resolved at call time:

```csharp
public interface ITajeerCredentialProvider
{
    Task<TajeerCredentials> GetForTenantAsync(Guid tenantId, CancellationToken ct);
}

public sealed record TajeerCredentials(string AppId, string AppKey, string Authorization);

// Key Vault implementation
public sealed class KeyVaultCredentialProvider : ITajeerCredentialProvider
{
    private readonly SecretClient _secretClient;
    private readonly IMemoryCache _cache; // 1-hour cache to avoid Key Vault throttling

    public async Task<TajeerCredentials> GetForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync($"tajeer-creds:{tenantId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var appId = await _secretClient.GetSecretAsync($"tajeer-{tenantId}-app-id", cancellationToken: ct);
            var appKey = await _secretClient.GetSecretAsync($"tajeer-{tenantId}-app-key", cancellationToken: ct);
            var auth = await _secretClient.GetSecretAsync($"tajeer-{tenantId}-authorization", cancellationToken: ct);
            return new TajeerCredentials(appId.Value.Value, appKey.Value.Value, auth.Value.Value);
        });
    }
}
```

### 4.3 `TajeerAuthHandler` (HTTP message handler)

```csharp
public sealed class TajeerAuthHandler : DelegatingHandler
{
    private readonly ITajeerCredentialProvider _credentials;
    private readonly ITenantContext _tenantContext; // resolves current tenant from request scope

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var creds = await _credentials.GetForTenantAsync(_tenantContext.TenantId, ct);
        request.Headers.TryAddWithoutValidation("app-id", creds.AppId);
        request.Headers.TryAddWithoutValidation("app-key", creds.AppKey);
        request.Headers.TryAddWithoutValidation("Authorization", creds.Authorization);
        return await base.SendAsync(request, ct);
    }
}
```

Registered in DI via `IHttpClientFactory`:

```csharp
services.AddHttpClient<ITajeerClient, TajeerClient>(c =>
{
    c.BaseAddress = new Uri(options.BaseUrl);
    c.Timeout = options.RequestTimeout;
})
.AddHttpMessageHandler<TajeerAuthHandler>()
.AddHttpMessageHandler<TajeerLoggingHandler>()
.AddResilienceHandler("tajeer", TajeerResiliencePipeline.Configure);
```

---

## 5. Public C# interface

### 5.1 Top-level interface

```csharp
public interface ITajeerClient
{
    ITajeerContracts Contracts { get; }
    ITajeerLookups Lookups { get; }
    ITajeerWebhookRegistration Webhooks { get; }
    ITajeerExecution Execution { get; }
}
```

### 5.2 `ITajeerContracts` — the workhorse

```csharp
public interface ITajeerContracts
{
    // §6.1 Save Contract
    Task<TajeerResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.1.2 Save Vehicle Rent Status (optional, if not provided in Save)
    Task<TajeerResult<SaveVehicleStatusResponse>> SaveVehicleStatusAsync(
        SaveVehicleStatusRequest request,
        CancellationToken ct);

    // §6.2.1 Get Full Contract PDF
    Task<TajeerResult<byte[]>> GetContractPdfAsync(
        long contractNumber, CancellationToken ct);

    // §6.2.2 Get Summarized Contract PDF
    Task<TajeerResult<byte[]>> GetSummarizedContractPdfAsync(
        long contractNumber, CancellationToken ct);

    // §6.3 Get Contract
    Task<TajeerResult<GetContractResponse>> GetAsync(
        long contractNumber, CancellationToken ct);

    // §6.4 Get Saved Contract By Plate Number
    Task<TajeerResult<IReadOnlyList<GetContractResponse>>> GetSavedByPlateAsync(
        PlateNumber plate, CancellationToken ct);

    // §6.6 Close Contract
    Task<TajeerResult<CloseContractResponse>> CloseAsync(
        CloseContractRequest request,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.7 Extend Contract
    Task<TajeerResult<Unit>> ExtendAsync(
        ExtendContractRequest request,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.8 Cancel Contract
    Task<TajeerResult<Unit>> CancelAsync(
        long contractNumber,
        string cancellationReason,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.10 Suspend Contract
    Task<TajeerResult<Unit>> SuspendAsync(
        SuspendContractRequest request,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.11 Update Paid Amount
    Task<TajeerResult<Unit>> UpdatePaidAmountAsync(
        UpdatePaidAmountRequest request,
        IdempotencyKey idempotencyKey,
        CancellationToken ct);

    // §6.12 Validate Contract (non-destructive pre-check)
    Task<TajeerResult<Unit>> ValidateAsync(
        ValidateContractRequest request, CancellationToken ct);

    // §6.13 Calculate Contract Payment (preview close/suspend amounts)
    Task<TajeerResult<CalculatePaymentResponse>> CalculatePaymentAsync(
        CalculatePaymentRequest request, CancellationToken ct);

    /// <summary>
    /// Returns the URL the renter visits to complete a saved contract.
    /// Format: {IssuanceUrlBase}/#/public-contract/{contractNumber}/{token}
    /// </summary>
    Uri BuildIssuanceUrl(long contractNumber, string token);
}
```

### 5.3 `ITajeerLookups`

```csharp
public interface ITajeerLookups
{
    // §6.5.1
    Task<TajeerResult<IReadOnlyList<RentPolicyDto>>> GetAllRentPoliciesAsync(CancellationToken ct);

    // §6.5.2
    Task<TajeerResult<IReadOnlyList<BranchDto>>> GetAllBranchesAsync(CancellationToken ct);

    // §6.5.3
    Task<TajeerResult<IReadOnlyList<ExtendedCoverageDto>>> GetAllExtendedCoveragesAsync(CancellationToken ct);

    // §6.14 — generic lookups
    Task<TajeerResult<IReadOnlyList<LookupItem>>> GetLookupAsync(
        TajeerLookupType type, CancellationToken ct);
    // Types: AuthorizationType, ContractStatus, ContractType, ExternalAuthorizationCountries,
    //        FuelType, IdType, PaymentMethod, YakeenNationality, GccNationality, Country,
    //        ClosureReasons, ClosureReasonsByMain, SuspensionReasons
}
```

### 5.4 `ITajeerWebhookRegistration`

```csharp
public interface ITajeerWebhookRegistration
{
    // §6.16 Register webhook (notification URL)
    Task<TajeerResult<Unit>> RegisterAsync(
        Uri notificationUrl, string sharedSecret, CancellationToken ct);
}
```

### 5.5 `ITajeerExecution`

```csharp
public interface ITajeerExecution
{
    // §6.15 Execution Status (MOJ status check)
    Task<TajeerResult<ExecutionStatusResponse>> GetExecutionStatusAsync(
        ExecutionStatusRequest request, CancellationToken ct);
}
```

### 5.6 `TajeerResult<T>` — discriminated union

```csharp
public abstract record TajeerResult<T>
{
    public sealed record Ok(T Value) : TajeerResult<T>;
    public sealed record BusinessError(TajeerError Error) : TajeerResult<T>;
    public sealed record SystemError(string Message, Exception? Exception) : TajeerResult<T>;

    public bool IsOk => this is Ok;
    public T? ValueOrDefault => this is Ok ok ? ok.Value : default;

    public TajeerResult<TOut> Map<TOut>(Func<T, TOut> mapper) => this switch
    {
        Ok ok => new TajeerResult<TOut>.Ok(mapper(ok.Value)),
        BusinessError be => new TajeerResult<TOut>.BusinessError(be.Error),
        SystemError se => new TajeerResult<TOut>.SystemError(se.Message, se.Exception),
        _ => throw new InvalidOperationException()
    };
}

public sealed record TajeerError(
    string ErrorKey,     // e.g. "server.error.renter.mobile.invalid"
    int ErrorCode,       // e.g. 168
    string RawMessage,   // Tajeer's message
    TajeerErrorCategory Category,
    LocalizedMessage UserMessage);

public sealed record LocalizedMessage(string Ar, string En);

public enum TajeerErrorCategory
{
    Validation,           // User input invalid → show field error
    BusinessRule,         // Driver license expired, vehicle in another contract → user-actionable message
    Authorization,        // 401/403 → don't surface to user, alert ops
    ExternalDependency,   // Yakeen/Naql/MVPI down → "try again later"
    SystemError,          // 5xx, timeout → "try again later" + ops alert
    NotFound,             // Contract/vehicle not found → re-fetch / refresh UI
    Conflict              // Concurrent modification → reload + retry
}

public sealed record Unit  // for void-returning operations
{
    public static readonly Unit Value = new();
}

public sealed record IdempotencyKey(string Value)
{
    public static IdempotencyKey New() => new(Guid.NewGuid().ToString("N"));
    public static IdempotencyKey For(string aggregateType, Guid aggregateId, string operation)
        => new($"{aggregateType}:{aggregateId}:{operation}");
}
```

---

## 6. DTO design

### 6.1 Conventions

- All DTOs are `sealed record` types — immutable.
- Use `JsonPropertyName` attributes to match Tajeer's `camelCase` exactly.
- Required fields use `required` modifier; optional fields are nullable.
- Money: `decimal` (System.Text.Json handles correctly with `JsonNumberHandling.AllowReadingFromString` for safety).
- Dates: see §11.2 for date conventions.
- Plate: see §11.1 — DTOs carry the Tajeer-format fields; conversion happens in helpers.

### 6.2 Example: `SaveContractRequest`

```csharp
public sealed record SaveContractRequest
{
    [JsonPropertyName("renter")]
    public required RenterDto Renter { get; init; }

    [JsonPropertyName("paymentDetails")]
    public required PaymentDetailsDto PaymentDetails { get; init; }

    [JsonPropertyName("vehicleDetails")]
    public required VehicleDetailsDto VehicleDetails { get; init; }

    [JsonPropertyName("rentStatus")]
    public RentStatusDto? RentStatus { get; init; }  // optional at save, required at create

    [JsonPropertyName("extraDriver")]
    public ExtraDriverDto? ExtraDriver { get; init; }

    [JsonPropertyName("rentedDriver")]
    public RentedDriverDto? RentedDriver { get; init; }

    [JsonPropertyName("authorizedDriver")]
    public AuthorizedDriverDto? AuthorizedDriver { get; init; }

    [JsonPropertyName("authorizationDetails")]
    public AuthorizationDetailsDto? AuthorizationDetails { get; init; }

    [JsonPropertyName("addtionalServices")]  // Tajeer typo preserved
    public AdditionalServicesDto? AdditionalServices { get; init; }

    [JsonPropertyName("extendedCoverageId")]
    public int? ExtendedCoverageId { get; init; }

    [JsonPropertyName("workingBranchId")]
    public required int WorkingBranchId { get; init; }

    [JsonPropertyName("rentPolicyId")]
    public required int RentPolicyId { get; init; }

    [JsonPropertyName("contractStartDate")]
    public required string ContractStartDate { get; init; }  // "yyyy-MM-ddTHH:mm"

    [JsonPropertyName("contractEndDate")]
    public required string ContractEndDate { get; init; }

    [JsonPropertyName("allowedKmPerHour")]
    public int AllowedKmPerHour { get; init; }

    [JsonPropertyName("allowedKmPerDay")]
    public int AllowedKmPerDay { get; init; }

    [JsonPropertyName("unlimitedKm")]
    public bool UnlimitedKm { get; init; }

    [JsonPropertyName("receiveBranchId")]
    public required int ReceiveBranchId { get; init; }

    [JsonPropertyName("returnBranchId")]
    public required int ReturnBranchId { get; init; }

    [JsonPropertyName("contractTypeCode")]
    public required int ContractTypeCode { get; init; }  // 1=daily, 2=hourly, 3=daily with driver, 4=hourly with driver

    [JsonPropertyName("allowedLateHours")]
    public int AllowedLateHours { get; init; }  // 0–24

    [JsonPropertyName("operatorId")]
    public required long OperatorId { get; init; }
}

public sealed record RenterDto
{
    [JsonPropertyName("personAddress")] public required string PersonAddress { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }  // required for GCC/Visitor
    [JsonPropertyName("mobile")] public required string Mobile { get; init; }
    [JsonPropertyName("idTypeCode")] public required int IdTypeCode { get; init; }
    [JsonPropertyName("idNumber")] public required long IdNumber { get; init; }
    [JsonPropertyName("passportNumber")] public string? PassportNumber { get; init; }
    [JsonPropertyName("hijriBirthDate")] public int? HijriBirthDate { get; init; }
    [JsonPropertyName("birthDate")] public string? BirthDate { get; init; }
    [JsonPropertyName("nationalityCode")] public int? NationalityCode { get; init; }
    [JsonPropertyName("driveLicenseNumber")] public string? DriveLicenseNumber { get; init; }
    [JsonPropertyName("licenseExpiryDate")] public string? LicenseExpiryDate { get; init; }
    [JsonPropertyName("issuePlaceId")] public long? IssuePlaceId { get; init; }
    [JsonPropertyName("idCopyNumber")] public int? IdCopyNumber { get; init; }
    [JsonPropertyName("idExpiryDate")] public string? IdExpiryDate { get; init; }
}
```

> **Note on the `addtionalServices` typo**: Tajeer's spec uses this exact misspelling. Match it. Don't "fix" it on our side.

### 6.3 `SaveContractResponse`

```csharp
public sealed record SaveContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }
    [JsonPropertyName("token")] public required string Token { get; init; }
    [JsonPropertyName("issuanceURL")] public required string IssuanceUrl { get; init; }
    [JsonPropertyName("mainPaymentDetails")] public required PaymentSummary MainPaymentDetails { get; init; }
    [JsonPropertyName("otherPaymentDetails")] public required PaymentSummary OtherPaymentDetails { get; init; }
    [JsonPropertyName("totalPaymentDetails")] public required PaymentSummary TotalPaymentDetails { get; init; }
}

public sealed record PaymentSummary
{
    [JsonPropertyName("paid")] public decimal Paid { get; init; }
    [JsonPropertyName("remaining")] public decimal Remaining { get; init; }
    [JsonPropertyName("total")] public decimal Total { get; init; }
    [JsonPropertyName("vat")] public decimal Vat { get; init; }
}
```

(All other DTOs follow the same pattern. Full list will be generated alongside the implementation.)

---

## 7. State mapping

### 7.1 `TajeerContractStatusCode` enum

From Tajeer responses (observed values):

```csharp
public enum TajeerContractStatusCode
{
    Saved = 1,           // PENDING_ISSUANCE locally
    Closed = 2,          // CLOSED
    Suspended = 3,       // SUSPENDED (informal — Tajeer surfaces via suspensionReason)
    Issued = 4,          // ACTIVE
    Cancelled = 5,       // CANCELLED
    // Extended: Tajeer does not have a distinct code — stays as Issued (4) with updated dates
}
```

### 7.2 `TajeerStatusMapper`

```csharp
public static class TajeerStatusMapper
{
    /// <summary>
    /// Maps Tajeer contractStatusCode + optional suspension/closure reasons to our local LeaseStatus.
    /// </summary>
    public static LeaseStatus FromTajeer(int contractStatusCode, int? suspensionReasonCode, int? closureCode)
    {
        return (contractStatusCode, suspensionReasonCode, closureCode) switch
        {
            (1, null, null) => LeaseStatus.PendingIssuance,
            (4, null, null) => LeaseStatus.Active,
            (3, _, null) => LeaseStatus.Suspended,
            (2, _, _) => LeaseStatus.Closed,
            (5, _, _) => LeaseStatus.Cancelled,
            _ => throw new InvalidTajeerStatusException(contractStatusCode, suspensionReasonCode, closureCode)
        };
    }

    /// <summary>
    /// EXTENDED is a local-only distinction — Tajeer keeps as Issued. We detect via extension count.
    /// </summary>
    public static LeaseStatus ApplyLocalRefinements(
        LeaseStatus tajeerStatus, int localExtensionCount)
    {
        if (tajeerStatus == LeaseStatus.Active && localExtensionCount > 0)
            return LeaseStatus.Extended;
        return tajeerStatus;
    }
}

public enum LeaseStatus
{
    Draft,
    SaveFailed,
    PendingIssuance,
    Active,
    Extended,
    Suspended,
    Closed,
    Cancelled,
    ExpiredDraft
}

public sealed class InvalidTajeerStatusException : Exception { /* ... */ }
```

### 7.3 Closure reason mapping (Tajeer §8.7)

```csharp
public enum TajeerClosureMainReason
{
    ContractPeriodExpiration = 1,
    ClosureBeforePeriodExpiration = 2,
    ClosureDueToDamage = 444
}

public enum TajeerClosureSubReason
{
    BothPartiesAgreement = 4,
    Accident = 5,
    CommercialRecall = 6,
    ClosureForReplacementOrUpgrade = 10
}

public static class TajeerClosureReasonMapper
{
    public static (TajeerClosureMainReason main, TajeerClosureSubReason? sub) FromLeaseClosureReason(
        LeaseClosureReason ours)
    {
        return ours switch
        {
            LeaseClosureReason.NaturalExpiry =>
                (TajeerClosureMainReason.ContractPeriodExpiration, null),
            LeaseClosureReason.MutualAgreement =>
                (TajeerClosureMainReason.ClosureBeforePeriodExpiration, TajeerClosureSubReason.BothPartiesAgreement),
            LeaseClosureReason.Accident =>
                (TajeerClosureMainReason.ClosureBeforePeriodExpiration, TajeerClosureSubReason.Accident),
            LeaseClosureReason.CommercialRecall =>
                (TajeerClosureMainReason.ClosureBeforePeriodExpiration, TajeerClosureSubReason.CommercialRecall),
            LeaseClosureReason.VehicleReplacement =>
                (TajeerClosureMainReason.ClosureBeforePeriodExpiration, TajeerClosureSubReason.ClosureForReplacementOrUpgrade),
            LeaseClosureReason.Damage =>
                (TajeerClosureMainReason.ClosureDueToDamage, null),
            _ => throw new ArgumentOutOfRangeException(nameof(ours))
        };
    }
}
```

### 7.4 Suspension reason mapping (Tajeer §8.6)

```csharp
public enum TajeerSuspensionReason
{
    NonTrafficAccident = 1,
    FinancialClaims = 2
}
```

### 7.5 Contract type mapping

```csharp
public enum TajeerContractType
{
    DailyWithoutDriver = 1,
    HourlyWithoutDriver = 2,
    DailyWithDriver = 3,
    HourlyWithDriver = 4
}
```

### 7.6 ID type mapping

```csharp
public enum TajeerIdType
{
    SaudiNational = 1,
    Iqama = 2,
    GccNational = 3,
    Visitor = 4
}
```

### 7.7 Payment method codes

Phase 1: capture Tajeer's `paymentMethodCode` and `otherPaymentMethodCode` from `/lookups/payment-method`. Don't hardcode in enum — populate `Lookup` table at startup. (Lookup values can change.)

---

## 8. Error mapping

### 8.1 Categorization of Tajeer's 300+ errors

Per Tajeer §8.2. Rather than mapping each individually, group by pattern:

```csharp
public static class TajeerErrorCatalog
{
    private static readonly Dictionary<string, TajeerErrorMapping> _map = BuildMap();

    public static TajeerError Map(string errorKey, int errorCode, string rawMessage)
    {
        if (_map.TryGetValue(errorKey, out var mapping))
            return new TajeerError(errorKey, errorCode, rawMessage, mapping.Category, mapping.UserMessage);

        // Fallback: classify by errorKey prefix
        var category = errorKey switch
        {
            var k when k.Contains(".invalid") => TajeerErrorCategory.Validation,
            var k when k.Contains(".required") => TajeerErrorCategory.Validation,
            var k when k.Contains(".not.found") => TajeerErrorCategory.NotFound,
            var k when k.Contains(".access.denied") => TajeerErrorCategory.Authorization,
            var k when k.Contains(".integration.") => TajeerErrorCategory.ExternalDependency,
            var k when k.Contains(".yakeen.") => TajeerErrorCategory.ExternalDependency,
            var k when k.Contains(".naql.") => TajeerErrorCategory.ExternalDependency,
            var k when k.Contains(".expired") => TajeerErrorCategory.BusinessRule,
            _ => TajeerErrorCategory.BusinessRule
        };

        return new TajeerError(errorKey, errorCode, rawMessage, category,
            new LocalizedMessage(
                Ar: "حدث خطأ أثناء معالجة الطلب. يرجى المحاولة مرة أخرى.",
                En: "An error occurred. Please try again or contact support."));
    }

    private static Dictionary<string, TajeerErrorMapping> BuildMap() => new()
    {
        // High-frequency validation errors — explicitly mapped with friendly messages
        ["server.error.renter.mobile.invalid"] = new(
            TajeerErrorCategory.Validation,
            new LocalizedMessage(
                Ar: "رقم جوال المستأجر غير صحيح. يجب أن يبدأ بـ 9665 ويتكون من 12 رقمًا.",
                En: "Renter mobile number is invalid. Must start with 9665 and be 12 digits.")),

        ["server.error.renter.email.invalid"] = new(
            TajeerErrorCategory.Validation,
            new LocalizedMessage(
                Ar: "البريد الإلكتروني للمستأجر غير صحيح.",
                En: "Renter email is invalid.")),

        ["server.error.renter.email.required"] = new(
            TajeerErrorCategory.Validation,
            new LocalizedMessage(
                Ar: "البريد الإلكتروني مطلوب للمستأجر من دول الخليج أو الزوار.",
                En: "Email is required for GCC and Visitor renters.")),

        ["server.error.car.license.expired"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "رخصة سير المركبة منتهية الصلاحية.",
                En: "Vehicle registration (Istimara) has expired.")),

        ["server.error.car.insurance.expired"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "تأمين المركبة منتهي الصلاحية.",
                En: "Vehicle insurance has expired.")),

        ["server.error.mvpi.expired"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "الفحص الدوري للمركبة منتهي الصلاحية.",
                En: "Vehicle periodic inspection (MVPI) has expired.")),

        ["server.error.operation.card.expired"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "بطاقة التشغيل منتهية الصلاحية.",
                En: "Operation card has expired.")),

        ["server.error.current.active.contract.exist"] = new(
            TajeerErrorCategory.Conflict,
            new LocalizedMessage(
                Ar: "يوجد عقد ساري حالياً على هذه المركبة.",
                En: "An active contract already exists for this vehicle.")),

        ["server.error.current.pending.contract.exist"] = new(
            TajeerErrorCategory.Conflict,
            new LocalizedMessage(
                Ar: "يوجد عقد محفوظ بانتظار الإصدار على هذه المركبة.",
                En: "A pending contract exists for this vehicle. Wait for issuance or cancel it.")),

        ["server.error.contract.start.date.passed"] = new(
            TajeerErrorCategory.Validation,
            new LocalizedMessage(
                Ar: "تاريخ بداية العقد قد مضى.",
                En: "Contract start date is in the past.")),

        ["server.error.extension.limit.exceeded"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "تم الوصول للحد الأقصى لعدد التمديدات (25). أغلق العقد وأنشئ عقدًا جديدًا.",
                En: "Maximum extension limit (25) reached. Close this contract and create a new one.")),

        ["server.error.cannot.modify.deductible.amount"] = new(
            TajeerErrorCategory.BusinessRule,
            new LocalizedMessage(
                Ar: "لا يمكن تعديل مبلغ التحمل بعد حفظ العقد.",
                En: "Endurance/deductible amount cannot be modified after the contract has been saved.")),

        ["server.error.yakeen.integration.server.error"] = new(
            TajeerErrorCategory.ExternalDependency,
            new LocalizedMessage(
                Ar: "تعذر الاتصال بنظام يقين حالياً. حاول مرة أخرى بعد قليل.",
                En: "Cannot connect to Yakeen at the moment. Please try again shortly.")),

        ["server.error.naql.not.available"] = new(
            TajeerErrorCategory.ExternalDependency,
            new LocalizedMessage(
                Ar: "تعذر الاتصال بنظام نقل حالياً.",
                En: "Cannot connect to Naql at the moment. Please try again shortly.")),

        ["server.error.access.denied"] = new(
            TajeerErrorCategory.Authorization,
            new LocalizedMessage(
                Ar: "غير مصرح لك بتنفيذ هذه العملية.",
                En: "You are not authorized to perform this operation.")),

        ["server.error.daily.rental.price.exceed.the.maximum.price.50000"] = new(
            TajeerErrorCategory.Validation,
            new LocalizedMessage(
                Ar: "سعر التأجير اليومي يتجاوز الحد الأقصى (50,000 ريال).",
                En: "Daily rental price exceeds the maximum allowed (50,000 SAR).")),

        // ... (Phase 1: map the top 30 most-likely errors explicitly; rest fall through to category-based default)
    };
}

internal sealed record TajeerErrorMapping(TajeerErrorCategory Category, LocalizedMessage UserMessage);
```

### 8.2 Error mapping strategy

**Phase 1**: Map the top ~30 errors explicitly (validation + common business rules). Everything else falls through to category-based defaults. Friendly enough for users.

**Phase 2**: Mine production logs for unmapped errors; add explicit mappings for any that occur >10x/week. Don't try to map all 300 upfront — most you'll never see.

**HTTP-level mapping**:

| HTTP Status | Tajeer body | Mapping |
|---|---|---|
| 200 + valid body | Success | `TajeerResult.Ok` |
| 200 + `errorKey` in body | (Tajeer sometimes returns 200 with error in body — defensive parsing) | `TajeerResult.BusinessError` |
| 400 + error body | Business/validation | `TajeerResult.BusinessError` |
| 401 / 403 | Auth issue | `TajeerResult.SystemError`, alert ops (don't surface to user) |
| 429 | Rate limited | Retry per Polly policy; if exhausted → `SystemError` |
| 5xx | Server error | Retry per Polly policy; if exhausted → `SystemError` |
| Network failure / timeout | Transient | Retry per Polly policy; if exhausted → `SystemError` |

---

## 9. Resilience (Polly v8)

### 9.1 The pipeline

```csharp
public static class TajeerResiliencePipeline
{
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, ResilienceHandlerContext context)
    {
        builder
            // Timeout per attempt
            .AddTimeout(TimeSpan.FromSeconds(30))

            // Retry on transient failures
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r =>
                        (int)r.StatusCode >= 500 ||
                        r.StatusCode == HttpStatusCode.RequestTimeout ||
                        r.StatusCode == HttpStatusCode.TooManyRequests),
                OnRetry = args =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<TajeerClient>>();
                    logger.LogWarning(
                        "Tajeer retry {Attempt}/{Max} after {Delay}ms. Reason: {Reason}",
                        args.AttemptNumber, 3, args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return default;
                }
            })

            // Circuit breaker
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })

            // Bulkhead (max concurrent requests)
            .AddConcurrencyLimiter(permitLimit: 50, queueLimit: 100);
    }
}
```

### 9.2 What NOT to retry

- 4xx with `errorKey` set — that's a business rule violation. Retrying won't change the outcome.
- 200 + error body — same.
- Auth failures (401/403) — alert ops, don't burn quota retrying.

### 9.3 Circuit breaker behavior

When open:

- All Tajeer calls fail fast with `TajeerResult.SystemError("Circuit breaker open")`.
- BFF surfaces "Tajeer is temporarily unavailable. Operations will resume automatically."
- Reads can fall back to cached lookups (lookups Redis TTL 1h, so stale-but-OK).
- Writes go to outbox and wait — the worker also gets fast-fail and queues for the next interval.
- After 30s break duration, half-open: 1 trial request decides if circuit re-closes.

---

## 10. Idempotency wrapper

### 10.1 Strategy

Tajeer doesn't expose an `Idempotency-Key` header. Our adapter enforces idempotency *before* calling Tajeer by checking aggregate state:

```csharp
public sealed class IdempotentTajeerContracts : ITajeerContracts
{
    private readonly ITajeerContracts _inner;
    private readonly IIdempotencyStore _store;
    private readonly ILeaseStateReader _leaseReader;

    public async Task<TajeerResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request, IdempotencyKey key, CancellationToken ct)
    {
        // 1. Check idempotency store for prior response
        var cached = await _store.GetAsync<SaveContractResponse>(key, ct);
        if (cached is not null)
            return new TajeerResult<SaveContractResponse>.Ok(cached);

        // 2. Defense in depth: check if a Lease already has a Tajeer contract for this request
        // (in case caller forgot to set idempotency key but the operation is logically same)
        // This is application-specific and requires the caller to provide the LeaseId in the key.

        // 3. Call Tajeer
        var result = await _inner.SaveAsync(request, key, ct);

        // 4. Cache the response for 24h
        if (result is TajeerResult<SaveContractResponse>.Ok ok)
            await _store.SetAsync(key, ok.Value, TimeSpan.FromHours(24), ct);

        return result;
    }

    // Similar wrappers for Close, Extend, Cancel, Suspend, UpdatePaidAmount, SaveVehicleStatus
    // Pure-read operations (Get*, Validate, Calculate, Lookups) skip idempotency
}

public interface IIdempotencyStore
{
    Task<T?> GetAsync<T>(IdempotencyKey key, CancellationToken ct) where T : class;
    Task SetAsync<T>(IdempotencyKey key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
}
```

Stored in Redis with key `tajeer-idem:{tenantId}:{key.Value}`.

### 10.2 Operation-specific idempotency keys

| Operation | Caller-provided key format | Rationale |
|---|---|---|
| Save | `lease:{leaseId}:save` | One save per lease |
| Close | `lease:{leaseId}:close` | One close per lease |
| Extend | `lease:{leaseId}:extend:{extensionAttempt}` | Multiple extensions allowed; key per attempt |
| Cancel | `lease:{leaseId}:cancel` | One cancel per lease |
| Suspend | `lease:{leaseId}:suspend:{suspensionAttempt}` | Multiple suspensions allowed |
| UpdatePaidAmount | `lease:{leaseId}:payment:{paymentTxnId}` | One per logical payment transaction |

---

## 11. Helpers

### 11.1 Plate number conversion

Per Tajeer §6.4 v9.3: characters `أ` and `ي` are transitioning to `ا` and `ى`. New `newPlateNumber` field on responses uses new chars.

```csharp
public sealed record PlateNumber(string Number, string Char1, string Char2, string Char3, int PlateType)
{
    // Normalized form: "0008 أ ي ي" (with Tajeer's separator if any)
    public string ToNormalized() => $"{Number} {Char1} {Char2} {Char3}";

    public static PlateNumber Parse(string normalized, int plateType)
    {
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) throw new FormatException($"Invalid plate: {normalized}");
        return new(parts[0], parts[1], parts[2], parts[3], plateType);
    }
}

public static class PlateNumberConverter
{
    private static readonly Dictionary<char, char> _oldToNew = new()
    {
        ['أ'] = 'ا',
        ['ي'] = 'ى'
    };

    private static readonly Dictionary<char, char> _newToOld =
        _oldToNew.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Convert old chars to new (for outbound to Tajeer).</summary>
    public static PlateNumber ToNew(PlateNumber p) => new(
        p.Number,
        ConvertChars(p.Char1, _oldToNew),
        ConvertChars(p.Char2, _oldToNew),
        ConvertChars(p.Char3, _oldToNew),
        p.PlateType);

    /// <summary>Pick new chars if available, else old.</summary>
    public static string PreferNew(string? newPlate, string oldPlate)
        => !string.IsNullOrWhiteSpace(newPlate) ? newPlate : oldPlate;

    private static string ConvertChars(string s, Dictionary<char, char> map)
        => new(s.Select(c => map.GetValueOrDefault(c, c)).ToArray());
}
```

### 11.2 Date conversion

Tajeer uses several date formats — be careful:

| Field | Format | Example | Notes |
|---|---|---|---|
| `contractStartDate`, `contractEndDate` | `yyyy-MM-ddTHH:mm` | `2021-11-26T13:37` | KSA local time (AST = UTC+3) |
| `hijriBirthDate`, `hijriIdExpiryDate` | INT `yyyymmdd` | `14430109` | Hijri calendar |
| `birthDate`, `idExpiryDate`, `licenseExpiryDate` | `yyyy-MM-dd` | `1980-01-03` | Gregorian, date only |
| `oilChangeDate` | `yyyy-MM-ddTHH:mm` | `2022-12-11T00:00` | Gregorian |
| `contractSignDate` (response) | `yyyy-MM-ddTHH:mm:ss.fff` | `2021-08-25T14:14:33.957` | KSA local |

```csharp
public static class TajeerDateConverter
{
    private static readonly TimeZoneInfo _ksaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); // UTC+3, no DST

    public static string ToTajeerDateTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _ksaTimeZone).ToString("yyyy-MM-ddTHH:mm");

    public static DateTime FromTajeerDateTime(string tajeerLocal)
    {
        var local = DateTime.SpecifyKind(
            DateTime.ParseExact(tajeerLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _ksaTimeZone);
    }

    public static string ToTajeerDateOnly(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static int ToHijriBirthDate(DateOnly gregorian)
    {
        var hijri = new HijriCalendar();
        return hijri.GetYear(gregorian.ToDateTime(TimeOnly.MinValue)) * 10000
             + hijri.GetMonth(gregorian.ToDateTime(TimeOnly.MinValue)) * 100
             + hijri.GetDayOfMonth(gregorian.ToDateTime(TimeOnly.MinValue));
    }

    public static DateOnly FromHijriBirthDate(int hijriYyyymmdd)
    {
        var year = hijriYyyymmdd / 10000;
        var month = (hijriYyyymmdd / 100) % 100;
        var day = hijriYyyymmdd % 100;
        var hijri = new HijriCalendar();
        var date = hijri.ToDateTime(year, month, day, 0, 0, 0, 0);
        return DateOnly.FromDateTime(date);
    }
}
```

### 11.3 Sketch JSON builder

Per Tajeer §7. Canvas is 893×429 pixels. Four damage types.

```csharp
public sealed record DamageMarker(DamageType Type, double X, double Y);

public enum DamageType
{
    SmallScratch,        // "small-scratch"
    DeepScratch,         // "deep-scratch"
    VeryDeepScratch,     // "very-deep-scratch"
    BendInBody           // "bend-in-body"
}

public static class SketchInfoBuilder
{
    private const int CanvasWidth = 893;
    private const int CanvasHeight = 429;

    public static string ToJson(IEnumerable<DamageMarker> markers)
    {
        var list = markers.Select(m =>
        {
            if (m.X < 0 || m.X > CanvasWidth || m.Y < 0 || m.Y > CanvasHeight)
                throw new ArgumentOutOfRangeException(
                    $"Marker out of canvas bounds: ({m.X},{m.Y}). Canvas is {CanvasWidth}x{CanvasHeight}.");
            return new { type = ToTajeerType(m.Type), x = m.X, y = m.Y };
        }).ToArray();
        return JsonSerializer.Serialize(list);
    }

    public static IReadOnlyList<DamageMarker> Parse(string sketchInfoJson)
    {
        if (string.IsNullOrWhiteSpace(sketchInfoJson) || sketchInfoJson == "[]")
            return Array.Empty<DamageMarker>();
        using var doc = JsonDocument.Parse(sketchInfoJson);
        return doc.RootElement.EnumerateArray()
            .Select(e => new DamageMarker(
                FromTajeerType(e.GetProperty("type").GetString()!),
                e.GetProperty("x").GetDouble(),
                e.GetProperty("y").GetDouble()))
            .ToArray();
    }

    private static string ToTajeerType(DamageType t) => t switch
    {
        DamageType.SmallScratch => "small-scratch",
        DamageType.DeepScratch => "deep-scratch",
        DamageType.VeryDeepScratch => "very-deep-scratch",
        DamageType.BendInBody => "bend-in-body",
        _ => throw new ArgumentOutOfRangeException(nameof(t))
    };

    private static DamageType FromTajeerType(string s) => s switch
    {
        "small-scratch" => DamageType.SmallScratch,
        "deep-scratch" => DamageType.DeepScratch,
        "very-deep-scratch" => DamageType.VeryDeepScratch,
        "bend-in-body" => DamageType.BendInBody,
        _ => throw new ArgumentException($"Unknown damage type: {s}")
    };
}
```

---

## 12. Webhook receiver

### 12.1 Per Tajeer §6.16.1

Tajeer POSTs to our registered URL with body:

```json
{
  "id": "notif_982374",
  "timestamp": "2025-10-06T10:30:00",
  "category": "contract",
  "type": "contract.create",
  "referenceId": "2569450000400015",
  "message": "Contract 2569450000400015 is created."
}
```

Headers:
- `Content-Type: application/json`
- `secret-key: <our registered shared secret>`

### 12.2 Receiver implementation

```csharp
[ApiController]
[Route("webhooks/tajeer")]
public sealed class TajeerWebhookController : ControllerBase
{
    private readonly IOptions<TajeerOptions> _options;
    private readonly IWebhookProcessor _processor;
    private readonly ILogger<TajeerWebhookController> _logger;

    [HttpPost]
    public async Task<IActionResult> Receive(
        [FromBody] TajeerWebhookPayload payload,
        [FromHeader(Name = "secret-key")] string? secretKey,
        CancellationToken ct)
    {
        // 1. Verify secret
        if (!WebhookSignatureValidator.IsValid(secretKey, _options.Value.WebhookSharedSecret))
        {
            _logger.LogWarning("Invalid Tajeer webhook secret. RemoteIP={IP}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        // 2. Validate payload
        if (string.IsNullOrEmpty(payload.Id) || string.IsNullOrEmpty(payload.Type))
            return BadRequest("Missing required fields");

        // 3. Persist + dedup (UNIQUE constraint on (Source, ExternalEventId))
        try
        {
            await _processor.EnqueueAsync(payload, ct);
        }
        catch (DuplicateWebhookException)
        {
            _logger.LogInformation("Duplicate Tajeer webhook {EventId} — already processed", payload.Id);
            // Idempotent: return 200 so Tajeer doesn't retry
        }

        // 4. Ack immediately. Actual processing happens async via worker.
        return Ok();
    }
}

public sealed record TajeerWebhookPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("category")] string Category,   // "contract", "invoice", "general"
    [property: JsonPropertyName("type")] string Type,           // e.g. "contract.create"
    [property: JsonPropertyName("referenceId")] string? ReferenceId,
    [property: JsonPropertyName("message")] string? Message
);

public static class WebhookSignatureValidator
{
    public static bool IsValid(string? receivedSecret, string expectedSecret)
    {
        if (string.IsNullOrEmpty(receivedSecret) || string.IsNullOrEmpty(expectedSecret))
            return false;
        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(receivedSecret),
            Encoding.UTF8.GetBytes(expectedSecret));
    }
}
```

### 12.3 Async processing

The controller acks immediately. A background worker (BackgroundService) drains `WebhookLog` rows where `ProcessedAtUtc IS NULL`:

```csharp
public sealed class TajeerWebhookProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await _store.GetUnprocessedAsync(maxBatch: 50, stoppingToken);
            foreach (var entry in batch)
            {
                try
                {
                    await Dispatch(entry, stoppingToken);
                    await _store.MarkProcessedAsync(entry.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    await _store.RecordErrorAsync(entry.Id, ex.Message, stoppingToken);
                    _logger.LogError(ex, "Failed processing webhook {Id}", entry.Id);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task Dispatch(WebhookLogEntry entry, CancellationToken ct)
    {
        // Always re-fetch authoritative state from Tajeer (don't trust webhook body alone)
        if (entry.Source == "TAJEER" && entry.Category == "contract" && long.TryParse(entry.ReferenceId, out var contractNumber))
        {
            var fullContract = await _tajeer.Contracts.GetAsync(contractNumber, ct);
            await _leaseSync.ReconcileFromTajeerAsync(fullContract, ct);
        }
        // ...invoice category, etc.
    }
}
```

### 12.4 Event type catalog (Tajeer §8.9)

| `category` | `type` | Our handler |
|---|---|---|
| `contract` | `contract.create` | Reconcile lease from `GET /rent-contract`; transition PENDING_ISSUANCE → ACTIVE |
| `contract` | `contract.extend` | Reconcile; increment ExtensionCount |
| `invoice` | `invoice.unpaid` | Mark invoice OVERDUE; notify customer |
| `general` | (any) | Log; no automated action Phase 1 |

---

## 13. Lookup caching

### 13.1 Strategy

Lookups are mostly static (rent policies, branches, fuel types, countries). Pre-warm at app start; refresh every 1h or on demand.

```csharp
public interface ITajeerLookupCache
{
    Task<IReadOnlyList<RentPolicyDto>> GetRentPoliciesAsync(CancellationToken ct);
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct);
    Task<IReadOnlyList<ExtendedCoverageDto>> GetExtendedCoveragesAsync(CancellationToken ct);
    Task<IReadOnlyList<LookupItem>> GetLookupAsync(TajeerLookupType type, CancellationToken ct);
    Task InvalidateAsync(string? lookupType = null, CancellationToken ct = default);
}

public sealed class RedisLookupCache : ITajeerLookupCache
{
    private readonly IDatabase _redis;
    private readonly ITajeerClient _client;
    private readonly ITenantContext _tenant;

    private string Key(string type) => $"tajeer-lookup:{_tenant.TenantId}:{type}";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<RentPolicyDto>> GetRentPoliciesAsync(CancellationToken ct)
    {
        var key = Key("rent-policies");
        var cached = await _redis.StringGetAsync(key);
        if (cached.HasValue)
            return JsonSerializer.Deserialize<RentPolicyDto[]>(cached!)!;

        var result = await _client.Lookups.GetAllRentPoliciesAsync(ct);
        if (result is TajeerResult<IReadOnlyList<RentPolicyDto>>.Ok ok)
        {
            await _redis.StringSetAsync(key, JsonSerializer.Serialize(ok.Value), Ttl);
            return ok.Value;
        }
        throw new InvalidOperationException("Failed to fetch rent policies and no cache available");
    }

    // ... similar for other lookups
}
```

### 13.2 Local DB sync

The `TajeerLookupCache` SQL table (doc 01 §5.8) is the **source of truth for display** in our UI — populated by a nightly background job, with Redis as the hot cache.

This gives us:
- Fast reads from Redis.
- Fallback to DB if Redis is cold.
- Searchable/filterable from DB for admin screens.
- Independent of Tajeer availability for read paths.

---

## 14. Observability

### 14.1 OpenTelemetry source

```csharp
public static class TajeerTelemetry
{
    public static readonly ActivitySource ActivitySource = new("AutoLeaseNet.Tajeer", "1.0");
    public static readonly Meter Meter = new("AutoLeaseNet.Tajeer", "1.0");

    public static readonly Counter<long> CallsTotal =
        Meter.CreateCounter<long>("tajeer.calls.total", description: "Total Tajeer API calls");
    public static readonly Counter<long> ErrorsTotal =
        Meter.CreateCounter<long>("tajeer.errors.total", description: "Tajeer errors by category");
    public static readonly Histogram<double> CallDuration =
        Meter.CreateHistogram<double>("tajeer.call.duration.ms", "ms");
}
```

Every call records:
- Tag: `tajeer.endpoint` (e.g. `Contracts.Save`)
- Tag: `tajeer.environment` (`sandbox` or `prod`)
- Tag: `tajeer.outcome` (`ok`, `business_error`, `system_error`)
- Tag: `tajeer.error_key` (if error)
- Tag: `tenant.id`
- Histogram: call duration

### 14.2 Structured logging

`TajeerLoggingHandler` (HTTP message handler) logs **every** request/response with sanitization:

- Request: method, URL, body (with PII fields masked: `idNumber`, `mobile`, `email`)
- Response: status, body (with sensitive fields masked), duration
- Correlation ID propagated via `traceparent` header

### 14.3 Integration log table

Per doc 01 §5.8 `IntegrationLog`. Every Tajeer call writes a row asynchronously (fire-and-forget via channel) so ops can query "what did we send for lease X?" in SQL.

---

## 15. Testing strategy

### 15.1 Unit tests

| Suite | Coverage |
|---|---|
| `ErrorMappingTests` | Every explicitly mapped errorKey + samples of category-based fallback |
| `PlateNumberConverterTests` | Old↔new char conversion, edge cases (mixed chars, normalization) |
| `HijriDateConverterTests` | Round-trip, leap years, month boundaries |
| `SketchInfoBuilderTests` | JSON shape, all 4 damage types, canvas bounds validation, empty array, parse round-trip |
| `StatusMapperTests` | Every Tajeer status code combo → expected LeaseStatus |
| `IdempotencyDecoratorTests` | Cached response returned on repeat call, errors not cached |
| `WebhookSignatureValidatorTests` | Constant-time comparison, missing secrets, mismatch |

### 15.2 Contract snapshot tests (Verify.NET)

Pin known Tajeer responses (captured from sandbox) as snapshot files. Tests verify deserialization still produces the expected DTO.

This catches Tajeer schema changes (new fields, removed fields) at test time without hitting the network.

```csharp
[Fact]
public async Task SaveContractResponse_Snapshot_2026_05_17()
{
    var json = await File.ReadAllTextAsync("snapshots/save-contract-success-2026-05-17.json");
    var dto = JsonSerializer.Deserialize<SaveContractResponse>(json, JsonOptions);
    await Verify(dto);
}
```

### 15.3 Sandbox integration tests

Marked `[Trait("Integration", "Tajeer")]`, run on PR via CI with sandbox credentials. Test happy path of each endpoint end-to-end. Skipped locally unless creds present.

```csharp
[Fact]
[Trait("Integration", "Tajeer")]
public async Task Save_Then_Get_RoundTrip()
{
    var save = await _client.Contracts.SaveAsync(TestData.MinimalSaveRequest, IdempotencyKey.New(), CancellationToken.None);
    save.Should().BeOfType<TajeerResult<SaveContractResponse>.Ok>();

    var contractNumber = ((TajeerResult<SaveContractResponse>.Ok)save).Value.ContractNumber;

    var get = await _client.Contracts.GetAsync(contractNumber, CancellationToken.None);
    get.Should().BeOfType<TajeerResult<GetContractResponse>.Ok>();
}
```

### 15.4 No-op against prod

CI never runs integration tests against prod. Production sanity is end-to-end manual UAT before each release.

---

## 16. Open questions

| # | Question | Default |
|---|---|---|
| Q1 | Should we register webhook URL programmatically on app start, or one-time manual setup? | Programmatic on startup, idempotent (Tajeer's `/webhook/register` documented as POST — assume upsert behavior; verify in sandbox) |
| Q2 | Rate limit handling: Tajeer V9.7 doesn't document explicit limits. Should we add a self-imposed rate limiter? | Yes — concurrency limiter (50 in pipeline). Add per-tenant token bucket if we see throttling responses (429). |
| Q3 | For Save Contract, should we always call Validate Contract first as a pre-check? | Phase 1: No (extra latency, Tajeer validates server-side anyway). Phase 2: optional client-driven validation for inline form feedback. |
| Q4 | How do we handle Tajeer's reported issue where `200 OK` can include error body? | Defensive: every response with `errorKey` in body is treated as `BusinessError` regardless of HTTP status. |
| Q5 | Should webhook payloads be persisted with full body before processing, or only metadata? | Full body in `WebhookLog.Payload` — debugging is worth the storage cost. Truncate after 90 days. |
| Q6 | Pre-warm lookup cache on app start? | Yes for the small/critical ones (branches, rent policies, payment methods). Lazy-load the rest. |

---

## 17. Sign-off checklist

- [ ] Project structure approved (especially the package boundaries)
- [ ] `TajeerResult<T>` discriminated union pattern approved (vs throwing on business errors)
- [ ] Per-tenant credential resolution via Key Vault approved
- [ ] State mapping enums (`LeaseStatus`, `TajeerContractStatusCode`, closure/suspension reasons) approved
- [ ] Error catalog: top-30 explicit + category-based fallback approved
- [ ] Polly v8 pipeline configuration approved (timeout, retry, breaker, bulkhead values)
- [ ] Idempotency wrapper approach (decorator + Redis store) approved
- [ ] Webhook handler: ack-fast + async processing approved
- [ ] Lookup cache: Redis hot + SQL warm + nightly sync approved
- [ ] Sketch JSON format / plate char conversion / Hijri date helpers approved
- [ ] Testing strategy (unit + Verify snapshot + sandbox integration) approved
- [ ] Open questions §16 answered

---

## 18. Next docs

- **04 — BFF API Surface (OpenAPI)** — REST endpoints for portals + how they map to adapter calls
- **05 — ZATCA Invoice Generation Design** — UBL XML, library choice, EGS lifecycle
- **06 — Approval Workflow Engine** — config schema + evaluator + delegation
- **07 — Monorepo Layout & Build System** (Turborepo + .NET)
