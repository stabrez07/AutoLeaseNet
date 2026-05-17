# 04 — Integration Architecture: Ports, Adapters & Pluggability

**Status**: Draft v0.1 — **STANDARD: applies to every integration**
**Phase**: Foundation
**Owner**: Architecture
**Depends on**: [01-multi-tenancy-and-domain-model.md](./01-multi-tenancy-and-domain-model.md)
**Last updated**: 2026-05-17

> **Note on doc order**: This doc was inserted after 03 (Tajeer adapter) but defines the architectural pattern that 03 already follows. Treat 04 as the rule; treat 03 as the canonical worked example.

---

## 1. Purpose

Every external integration in this platform — Tajeer, ZATCA, Nafath, SMS, Storage, D365, Telematics, Payments, MOI Fines, Car Servicing, AI services — **must** live in a separate, self-contained, swappable module.

This document defines:

1. **The architectural pattern** — Hexagonal / Ports & Adapters with two clearly-named flavors for different integration types.
2. **The module standard** — what every integration package must contain.
3. **DI registration pattern** — one `Add{Adapter}()` extension per package, never registered by app code directly.
4. **Configuration pattern** — per-adapter `XxxOptions` bound from `appsettings`/Key Vault.
5. **Health checks** — every adapter exposes a standard health probe.
6. **Testability** — every port has at least one in-memory/fake implementation for tests.
7. **The integration catalog** — every planned integration named, sized, and assigned a phase.
8. **The recipe** — step-by-step for adding a new integration.
9. **Anti-patterns** — what we don't do.

If any of these rules conflicts with delivery speed, raise it. **Do not violate the standard silently.**

---

## 2. Core principles

| # | Principle | Rationale |
|---|---|---|
| 1 | **Hexagonal / Ports & Adapters** | Application/domain code depends only on ports (interfaces). Adapters are interchangeable implementations behind those ports. |
| 2 | **One adapter = one package** | Every integration is a separate `.csproj`. No shared "Adapters.Shared" package containing concrete clients. |
| 3 | **No leaky abstractions in app code** | Application code never imports `AutoLeaseNet.Adapters.*`. It imports `AutoLeaseNet.Application.Ports.*` (or, for vendor-specific adapters, the port interface from that adapter package). |
| 4 | **DI registration is the only seam** | Composition root (BFF startup) calls `services.AddTajeer(...)`, `services.AddZatca(...)`. App code is registration-agnostic. |
| 5 | **Every port has at least 2 implementations** | The real adapter + an in-memory/fake. Tests use the fake by default. |
| 6 | **Configuration is per-adapter** | `TajeerOptions`, `ZatcaOptions`, `UnifonicOptions` — never a god-object `IntegrationsOptions`. |
| 7 | **Cross-cutting concerns are reusable** | Polly resilience policies, OTel telemetry, idempotency stores, Key Vault credential providers live in a `AutoLeaseNet.Adapters.Common` package and are wired into each adapter the same way. |
| 8 | **Feature-flag-friendly** | Every adapter check `IsEnabled` from config; disabled adapters register a no-op or null implementation so app code doesn't crash. |
| 9 | **Versioning is per-package** | Each adapter has its own version. Vendor API changes affect only that package; semantic versioning on the public interface. |
| 10 | **Document the vendor contract per adapter** | A `README.md` in each adapter package links to vendor docs, captures known quirks, and lists supported versions. |

---

## 3. Two patterns based on integration nature

### 3.1 Pattern A — Generic capability with multiple providers

Use when the *capability* is the contract, and the vendor is interchangeable.

**Examples**: SMS dispatch (Unifonic ↔ 4Jawaly ↔ Twilio), Object storage (Azure Blob ↔ S3), Cache (Redis ↔ in-memory), Payment gateways (HyperPay ↔ Moyasar ↔ PayTabs), Telematics providers (Mix ↔ Geotab ↔ OEM), Email (SendGrid ↔ MailJet), AI (Azure OpenAI ↔ Anthropic ↔ etc.).

**Where ports live**: `AutoLeaseNet.Application.Ports.*` (or a dedicated `AutoLeaseNet.Contracts.Integrations` package).

**Structure**:

```
packages/
├── application/
│   └── AutoLeaseNet.Application.Ports/
│       ├── Messaging/
│       │   └── ISmsSender.cs          ← the port
│       └── Storage/
│           └── IObjectStorage.cs       ← the port
└── adapters/
    ├── AutoLeaseNet.Adapters.Sms.Unifonic/
    │   └── implements ISmsSender
    ├── AutoLeaseNet.Adapters.Sms.FourJawaly/
    │   └── implements ISmsSender
    ├── AutoLeaseNet.Adapters.Sms.InMemory/      ← test double, shipped with adapters
    │   └── implements ISmsSender
    ├── AutoLeaseNet.Adapters.Storage.AzureBlob/
    ├── AutoLeaseNet.Adapters.Storage.InMemory/
    └── ...
```

**Sample port**:

```csharp
namespace AutoLeaseNet.Application.Ports.Messaging;

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct);
}

public sealed record SmsMessage(
    string ToE164,
    string Body,
    string? SenderId = null,
    Dictionary<string, string>? Tags = null);

public sealed record SmsSendResult(
    bool Success,
    string? ProviderMessageId,
    SmsFailureReason? FailureReason = null,
    string? FailureDetail = null);

public enum SmsFailureReason
{
    InvalidRecipient,
    Throttled,
    InsufficientBalance,
    ProviderUnavailable,
    Other
}
```

**Sample adapter** (Unifonic):

```csharp
namespace AutoLeaseNet.Adapters.Sms.Unifonic;

internal sealed class UnifonicSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly UnifonicOptions _options;

    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct)
    {
        // ... call Unifonic API
        // ... translate Unifonic-specific responses to the abstract SmsSendResult
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUnifonicSms(
        this IServiceCollection services,
        Action<UnifonicOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<ISmsSender, UnifonicSmsSender>()
                .AddResilienceHandler("unifonic", ResiliencePolicies.Default);
        services.AddHealthChecks().AddCheck<UnifonicHealthCheck>("unifonic-sms");
        return services;
    }
}
```

**Sample in-memory fake** (always ships alongside):

```csharp
namespace AutoLeaseNet.Adapters.Sms.InMemory;

public sealed class InMemorySmsSender : ISmsSender
{
    public List<SmsMessage> Sent { get; } = new();
    public Func<SmsMessage, SmsSendResult>? RespondWith { get; set; }

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        var result = RespondWith?.Invoke(message)
                     ?? new SmsSendResult(true, $"in-mem-{Guid.NewGuid():N}");
        return Task.FromResult(result);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemorySms(this IServiceCollection services)
    {
        services.AddSingleton<InMemorySmsSender>();
        services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<InMemorySmsSender>());
        return services;
    }
}
```

**Composition root** picks one:

```csharp
// BFF Program.cs
if (builder.Configuration.GetValue<string>("Sms:Provider") == "Unifonic")
    services.AddUnifonicSms(o => builder.Configuration.GetSection("Sms:Unifonic").Bind(o));
else
    services.AddInMemorySms(); // dev / tests
```

### 3.2 Pattern B — Specific vendor system with one API

Use when there's a single regulator/vendor with a unique API surface and "swap to a different vendor" isn't realistic.

**Examples**: Tajeer (KSA TGA), ZATCA (KSA Fatoorah), Nafath (KSA NIC identity), TAMM (KSA owner authorization), Wasl (KSA TGA tracking), MOI/Absher (KSA fines), D365 F&O / CRM / FA / HR (Microsoft-specific), Car Servicing App (a specific internal app).

**Where ports live**: in the adapter package itself, as the typed client interface. Pluggability here means "swap implementation when vendor's API version changes" — not "switch to a different vendor".

**Structure** (as already shown for Tajeer in doc 03):

```
packages/adapters/AutoLeaseNet.Adapters.Tajeer/
├── Client/
│   ├── ITajeerClient.cs            ← the interface, lives here (vendor-specific)
│   └── TajeerClient.cs             ← v9.7 implementation
├── ...
└── ServiceCollectionExtensions.cs  ← AddTajeer(...)

packages/adapters/AutoLeaseNet.Adapters.Tajeer.InMemory/
├── InMemoryTajeerClient.cs         ← test double implementing ITajeerClient
└── ServiceCollectionExtensions.cs  ← AddInMemoryTajeer(...)
```

**Why allow the interface to live in the adapter**: a true hexagonal purist would put `ITajeerClient` in the application layer. We're pragmatic — Tajeer's domain model (contract, renter, vehicle in their terms) is so specific to KSA leasing that pretending we'd ever swap vendors creates fictional abstractions. The application layer talks to `ITajeerClient` directly. If we ever need a UAE equivalent, we'd introduce a higher-level port (e.g. `ILeaseRegistryClient`) at that point.

**Common-sense rule**: if you'd never realistically swap the vendor, the interface lives in the adapter package. If you would, lift the port to the application layer.

---

## 4. Standard module layout

Every adapter package — Pattern A or Pattern B — follows this structure:

```
AutoLeaseNet.Adapters.{Name}/
├── AutoLeaseNet.Adapters.{Name}.csproj
├── README.md                              ← vendor links, quirks, supported version
├── Configuration/
│   └── {Name}Options.cs                   ← bound from config + secrets
├── Client/ (Pattern B) OR direct in root (Pattern A)
│   ├── I{Name}Client.cs (Pattern B only)
│   └── {Name}Client.cs                    ← the implementation
├── Resilience/
│   └── {Name}ResiliencePipeline.cs        ← Polly v8 pipeline config
├── ErrorHandling/
│   ├── {Name}Result.cs (Pattern B) — discriminated union
│   ├── {Name}Error.cs
│   └── {Name}ErrorCatalog.cs              ← vendor error code → friendly message
├── Observability/
│   ├── {Name}Telemetry.cs                 ← ActivitySource + Meter + Counters
│   └── {Name}LoggingHandler.cs            ← HTTP message handler for request/response logging
├── Health/
│   └── {Name}HealthCheck.cs               ← implements IHealthCheck
├── Authentication/ (if applicable)
│   ├── I{Name}CredentialProvider.cs
│   └── KeyVault{Name}CredentialProvider.cs
├── ServiceCollectionExtensions.cs         ← Add{Name}(...) — the ONLY public entry point
└── Tests/  (sibling: AutoLeaseNet.Adapters.{Name}.Tests/)
    ├── Unit/                              ← deterministic, no network
    ├── Contract/                          ← snapshot tests against captured vendor responses
    └── Integration/                       ← runs against vendor sandbox, [Trait("Integration", "{Name}")]
```

**Companion package** (Pattern A always, Pattern B optional but recommended):

```
AutoLeaseNet.Adapters.{Name}.InMemory/
├── AutoLeaseNet.Adapters.{Name}.InMemory.csproj
├── InMemory{Name}Client.cs                ← fake implementation
├── {Name}TestDataBuilder.cs               ← helpers to construct test scenarios
└── ServiceCollectionExtensions.cs         ← AddInMemory{Name}(...)
```

---

## 5. The `AddXxx()` extension contract

The single public entry point for every adapter. Mandatory signature shape:

```csharp
public static IServiceCollection Add{Name}(
    this IServiceCollection services,
    IConfiguration config,         // OR Action<XxxOptions> configure
    Action<{Name}Builder>? extras = null)  // optional extension point
{
    // 1. Bind options
    services.Configure<{Name}Options>(config.GetSection("{Name}"));

    // 2. Register the typed client + HTTP pipeline (if HTTP)
    services.AddHttpClient<I{Name}Client, {Name}Client>(...)
            .AddHttpMessageHandler<{Name}AuthHandler>()
            .AddHttpMessageHandler<{Name}LoggingHandler>()
            .AddResilienceHandler("{name}", {Name}ResiliencePipeline.Configure);

    // 3. Register supporting services
    services.AddSingleton<I{Name}CredentialProvider, KeyVault{Name}CredentialProvider>();
    services.AddSingleton<I{Name}LookupCache, Redis{Name}LookupCache>();

    // 4. Register health check
    services.AddHealthChecks().AddCheck<{Name}HealthCheck>(
        name: "{name}",
        tags: new[] { "integration", "{name}" });

    // 5. Telemetry
    services.AddOpenTelemetry()
        .WithTracing(t => t.AddSource({Name}Telemetry.ActivitySource.Name))
        .WithMetrics(m => m.AddMeter({Name}Telemetry.Meter.Name));

    // 6. Optional extension point
    extras?.Invoke(new {Name}Builder(services));

    return services;
}
```

**Mandatory rules**:

- Must be `internal` for the implementation classes — only the extension method is public.
- Must register a health check.
- Must register OTel source + meter.
- Must accept config via `IConfiguration` section OR `Action<TOptions>` (provide both overloads).
- Must be idempotent: calling twice doesn't double-register.

---

## 6. Configuration & secrets

### 6.1 Per-adapter options

```csharp
public sealed class TajeerOptions
{
    public required string BaseUrl { get; init; }
    public required string IssuanceUrlBase { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public required string WebhookSharedSecret { get; init; }  // resolved from Key Vault via @Microsoft.KeyVault(...) reference
    public bool IsEnabled { get; init; } = true;
    public bool IsSandbox { get; init; }
}
```

**Naming convention**: `{IntegrationName}Options`. Lives in `Configuration/` folder of the adapter package.

### 6.2 `appsettings` layout

```jsonc
{
  "Tajeer": {
    "BaseUrl": "https://tajeer-stg.api.elm.sa",
    "IssuanceUrlBase": "https://tajeerstg.logisti.sa",
    "WebhookSharedSecret": "@Microsoft.KeyVault(SecretUri=https://kv-superplexity-dev.vault.azure.net/secrets/tajeer-webhook-secret)",
    "IsEnabled": true,
    "IsSandbox": true
  },
  "Zatca": {
    "BaseUrl": "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal",
    "Environment": "Sandbox",
    "IsEnabled": true
  },
  "Sms": {
    "Provider": "Unifonic",
    "Unifonic": {
      "BaseUrl": "https://api.unifonic.com",
      "AppSid": "@Microsoft.KeyVault(...)",
      "SenderId": "SUPERPLX"
    }
  }
  // ... per-adapter sections
}
```

### 6.3 Secrets

- All credentials, secrets, API keys → Azure Key Vault.
- Resolved via Managed Identity (no connection strings in code).
- Cached in-process for 1h (`IMemoryCache`) to avoid Key Vault throttling.
- Per-tenant secrets named consistently: `{adapter}-{tenantId}-{secretName}` (e.g. `tajeer-{tenantId}-app-key`).

### 6.4 Feature flags

```csharp
public sealed class TajeerOptions
{
    public bool IsEnabled { get; init; } = true;
    // ...
}
```

When `IsEnabled = false`:

```csharp
public static IServiceCollection AddTajeer(this IServiceCollection services, IConfiguration config)
{
    var options = config.GetSection("Tajeer").Get<TajeerOptions>();
    if (options?.IsEnabled == false)
    {
        services.AddSingleton<ITajeerClient, DisabledTajeerClient>();
        // Returns SystemError on any call so app code surfaces "feature unavailable"
        return services;
    }
    // ... normal registration
}
```

This lets us ship code referencing an adapter before the credentials/onboarding are ready (esp. useful for Nafath, ZATCA prod).

---

## 7. Health checks

Every adapter implements `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck`:

```csharp
internal sealed class TajeerHealthCheck : IHealthCheck
{
    private readonly ITajeerClient _client;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            // Lightweight: fetch a small lookup. Doesn't burn quotas.
            var result = await _client.Lookups.GetAllBranchesAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
            return result.IsOk
                ? HealthCheckResult.Healthy("Tajeer reachable")
                : HealthCheckResult.Degraded($"Tajeer returned business error: {result}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Tajeer unreachable", ex);
        }
    }
}
```

Exposed at `GET /health` and `GET /health/integrations` on the BFF. Used by:

- Azure App Service health probes (auto-restart on unhealthy).
- Status page (internal ops dashboard).
- Pre-deploy smoke tests in CI.

---

## 8. Testability

### 8.1 In-memory implementations are first-class

Every port has an `InMemory` companion package. Tests register it instead of the real adapter:

```csharp
// Integration test setup
services.AddInMemoryTajeer();
services.AddInMemorySms();
services.AddInMemoryStorage();
services.AddInMemoryZatca();
// ... real components like DB, app services use these fakes transparently
```

### 8.2 Test data builders

```csharp
public static class TajeerTestDataBuilder
{
    public static SaveContractRequest MinimalValidRequest() => new() { /* ... */ };
    public static SaveContractRequest WithExpiredLicense() => MinimalValidRequest() with { /* ... */ };
    public static GetContractResponse IssuedContract(long contractNumber) => new() { /* ... */ };
}
```

### 8.3 Contract snapshot tests

Capture real vendor responses; verify our DTOs still parse them correctly when vendor API evolves. See doc 03 §15.2.

### 8.4 Sandbox integration tests

Marked `[Trait("Integration", "{Name}")]`. Run in CI on PR with sandbox credentials. Skipped locally unless creds are present in env vars.

---

## 9. Cross-cutting: `AutoLeaseNet.Adapters.Common`

Shared infrastructure reused by every adapter, **not** vendor-specific:

```
packages/adapters/AutoLeaseNet.Adapters.Common/
├── Resilience/
│   ├── ResiliencePolicies.cs           ← default Polly v8 policies (timeout, retry, breaker)
│   └── RetryConditions.cs              ← shared "is this retryable?" logic
├── Idempotency/
│   ├── IIdempotencyStore.cs            ← port
│   ├── RedisIdempotencyStore.cs
│   └── InMemoryIdempotencyStore.cs
├── Credentials/
│   ├── ICredentialProvider.cs           ← generic per-tenant credential interface
│   └── KeyVaultCredentialProvider.cs
├── Observability/
│   ├── HttpLoggingHandler.cs            ← request/response logging with PII masking
│   └── PiiMasking.cs                    ← shared list of fields to mask (idNumber, mobile, email, etc.)
├── Outbox/
│   ├── IOutboxStore.cs
│   ├── OutboxEvent.cs
│   └── OutboxBackgroundService.cs
└── Result/
    └── IntegrationResult{T}.cs          ← shared generic result type (optional — Pattern B may have own)
```

Every adapter takes a dependency on `AutoLeaseNet.Adapters.Common` and reuses these pieces. Never duplicate.

---

## 10. The integration catalog

Every external system we integrate with, mapped to its module:

| # | Integration | Module(s) | Port | Pattern | Phase | Phase 1 strategy |
|---|---|---|---|---|---|---|
| 1 | **Tajeer** (KSA leasing registry) | `Adapters.Tajeer`, `Adapters.Tajeer.InMemory` | `ITajeerClient` | B | 1 | Real adapter against staging |
| 2 | **ZATCA** (e-invoicing) | `Adapters.Zatca`, `Adapters.Zatca.InMemory` | `IZatcaClient` | B | 1 | Real adapter against sandbox CSID |
| 3 | **SMS** | `Adapters.Sms.Unifonic`, `Adapters.Sms.FourJawaly`, `Adapters.Sms.InMemory` | `ISmsSender` | A | 1 | Unifonic in prod; InMemory in tests |
| 4 | **Object Storage** | `Adapters.Storage.AzureBlob`, `Adapters.Storage.InMemory` | `IObjectStorage` | A | 1 | Azure Blob |
| 5 | **Cache / Idempotency** | `Adapters.Cache.Redis`, `Adapters.Cache.InMemory` | `ICacheStore`, `IIdempotencyStore` | A | 1 | Redis (Azure Cache for Redis) |
| 6 | **Identity — Internal** | `Adapters.Identity.Entra` | `IIdentityProvider` (or use ASP.NET auth directly) | B | 1 | Entra ID for staff |
| 7 | **Identity — External CIAM** | `Adapters.Identity.EntraExternal` | (ASP.NET OIDC) | B | 1 | Entra External ID for customer portal users (email + SMS OTP via #3) |
| 8 | **Email** | `Adapters.Email.AzureCommunication`, `Adapters.Email.InMemory` | `IEmailSender` | A | 1 | Azure Communication Services or SendGrid |
| 9 | **Document signing** (in-app e-sign UI, not Tajeer) | `Adapters.DocSign.Local` (canvas e-sign) | `IDocumentSigner` | A | 1 | Local canvas-based e-sign for non-Tajeer docs (quotes, NDAs) |
| 10 | **PDF generation** | `Adapters.Pdf.QuestPdf`, `Adapters.Pdf.InMemory` | `IPdfRenderer` | A | 1 | QuestPDF for quotes, ZATCA invoices |
| 11 | **D365 F&O** | `Adapters.D365.Fo`, `Adapters.D365.Fo.InMemory` | `ID365FoClient` | B | 2 | OData APIs to F&O — customers, invoices, payments |
| 12 | **D365 CRM** | `Adapters.D365.Crm`, `Adapters.D365.Crm.InMemory` | `ID365CrmClient` | B | 2 | Dataverse APIs — contacts, opportunities |
| 13 | **D365 Fixed Assets** | (subset of `Adapters.D365.Fo` or separate) | `ID365FixedAssetsClient` | B | 2 | Vehicle ↔ Fixed Asset sync |
| 14 | **D365 HR & Payroll** | `Adapters.D365.HrPayroll` | `ID365HrPayrollClient` | B | 3 | Driver = employee sync |
| 15 | **Car Servicing App** | `Adapters.CarServicing` | `ICarServicingClient` | B | 2 | Service booking + workshop status |
| 16 | **Payment Gateway** | `Adapters.Payments.HyperPay`, `Adapters.Payments.Moyasar`, `Adapters.Payments.InMemory` | `IPaymentGateway` | A | 2 | One real provider for B2B online payments + B2C cards |
| 17 | **WhatsApp Business** | `Adapters.Messaging.WhatsApp` | `IMessagingChannel` (alongside `ISmsSender`) | A | 2 | WhatsApp Business API for notifications |
| 18 | **Telematics** | `Adapters.Telematics.Mix`, `Adapters.Telematics.Geotab`, `Adapters.Telematics.InMemory` | `ITelematicsProvider` | A | 3 | Vendor-agnostic — Mix or Geotab as first impl |
| 19 | **Wasl** (KSA TGA fleet tracking) | `Adapters.Wasl` | `IWaslClient` | B | 3 | Mandatory for KSA fleets — telematics ↔ Wasl |
| 20 | **Nafath** (KSA national digital ID) | `Adapters.Nafath` | `INafathClient` | B | 3 | OIDC federation; deferred (long NIC onboarding). Phase 1 uses email+SMS OTP. |
| 21 | **TAMM** (KSA owner authorization) | `Adapters.Tamm` | `ITammClient` | B | 3+ | Currently subsumed via Tajeer's `tammExternalAuthorizationCountries`. Standalone only if needed. |
| 22 | **MOI / Absher** (traffic fines) | `Adapters.Moi` | `IMoiFinesClient` | B | 3 | Fine retrieval + pass-through to customer invoice |
| 23 | **Yakeen** (identity verification) | (accessed via Tajeer in P1; direct future) | `IYakeenClient` | B | 3+ | Direct only if non-leasing flows need it |
| 24 | **Naql** (vehicle ownership lookup) | (accessed via Tajeer in P1; direct future) | `INaqlClient` | B | 3+ | Same |
| 25 | **AI Copilot** | `Adapters.Ai.AzureOpenAi`, `Adapters.Ai.Anthropic`, `Adapters.Ai.InMemory` | `IAiCopilot` | A | 3 | Natural-language queries over fleet |
| 26 | **Document Vision** (damage detection from photos) | `Adapters.Ai.AzureVision`, `Adapters.Ai.InMemory` | `IDocumentVision` | A | 3 | Auto-detect vehicle damage in E-Check photos |
| 27 | **OCR** (Iqama/license auto-fill) | `Adapters.Ai.AzureFormRecognizer`, `Adapters.Ai.InMemory` | `IDocumentOcr` | A | 3 | Reduce form abandonment for driver onboarding |

**Phase 1 in-scope adapters (real implementations)**: #1, #2, #3, #4, #5, #6, #7, #8, #9, #10. Everything else: InMemory or no-op until phase.

---

## 11. Recipe: adding a new integration

When you need to integrate a new external system, follow these steps:

1. **Decide pattern (A or B)** — is the capability vendor-swappable, or is this a unique vendor API?
2. **Create the package** (`dotnet new classlib -n AutoLeaseNet.Adapters.{Name}` in `packages/adapters/`).
3. **Define the port**:
   - Pattern A: add interface to `AutoLeaseNet.Application.Ports.{Capability}/I{Name}.cs`.
   - Pattern B: add interface to the adapter package's `Client/I{Name}Client.cs`.
4. **Scaffold the standard structure** (Configuration/, Resilience/, ErrorHandling/, Observability/, Health/, ServiceCollectionExtensions.cs).
5. **Implement** the client against the vendor's API. Reference Polly policies from `Adapters.Common`.
6. **Map errors**: build the `{Name}ErrorCatalog` with vendor error codes → friendly localized messages (AR + EN).
7. **Add health check** (`{Name}HealthCheck : IHealthCheck`).
8. **Add OTel source + meter** (`{Name}Telemetry`).
9. **Create companion `InMemory` package** with a fake implementation.
10. **Write tests**: unit (error mapping, helpers) + contract snapshot (vendor response shapes) + sandbox integration (`[Trait("Integration", "{Name}")]`).
11. **Add README.md** with vendor docs links, supported API version, known quirks, onboarding contact.
12. **Add to integration catalog** in this doc.
13. **Wire up in composition root** (BFF `Program.cs`): `services.Add{Name}(config.GetSection("{Name}"))`.
14. **Add config section** to `appsettings.json` with secrets via Key Vault references.

**Time budget for a typical adapter**: 1–3 days end-to-end depending on API complexity. Tajeer was ~5 days because of its breadth. Simple ones like SMS are 1 day.

---

## 12. Versioning & deprecation

- Each adapter package has its own SemVer.
- Breaking changes to a port interface (Pattern A) require a major version bump and a migration note in the package README.
- When a vendor releases a new API version (e.g. Tajeer V10):
  - **Minor change**: bump minor, add new fields as nullable, mark deprecated fields with `[Obsolete]`.
  - **Major change**: create a new package version (e.g. `Adapters.Tajeer.V10`) alongside the old one. Migrate callers gradually. Retire old version after all callers migrated.
- No silent vendor API switches.

---

## 13. Anti-patterns to avoid

| Anti-pattern | Why it's bad | Do this instead |
|---|---|---|
| Calling `HttpClient` directly from a BFF endpoint to hit Tajeer/ZATCA | No retry, no idempotency, no telemetry, untestable, scattered config | Always go through the adapter |
| Putting `Adapters.{Name}` reference in `Application` or `Domain` projects | Couples app code to vendor; can't swap or fake | App code references only ports; composition root references adapters |
| One mega `IntegrationsService` class | Becomes a god object; impossible to test piece-by-piece | One adapter per integration; compose via DI |
| Shared `Adapters.All` package re-exporting all clients | Defeats modular versioning; pulls all transitive deps into every consumer | Each consumer takes the specific adapter package(s) it needs |
| Hardcoded credentials or API keys in code | Security violation; rotation impossible | Always Key Vault with managed identity |
| Throwing exceptions for business errors (e.g. license expired) | Pollutes call stack; encourages over-broad `try/catch` | Return `Result<T>` or `BusinessError` from adapter |
| Inline retry logic with `Thread.Sleep` / `Task.Delay` loops | Doesn't respect circuit breakers; pollutes domain code | Polly pipeline at adapter layer |
| `if (env == "Production")` checks in adapter code | Sandbox vs prod becomes hard to test; conditional bugs | Configuration switches the BaseUrl + credentials; same code path |
| Adapter calls another adapter directly | Creates hidden coupling | Compose at application layer; or extract shared port |
| Domain events leaked into adapter package | Domain ↔ adapter circular dependency | Adapter is a sink/source for app-defined events at the composition root |
| Skipping the InMemory fake "because it's faster" | Tests become slow + flaky against real services | Build the fake from day 1 — pays for itself within a week |

---

## 14. Updates to other docs (consequence of this doc)

- **Doc 01**: No structural changes needed — bounded contexts remain. Note that `Integration & Reference` context's entities (`OutboxEvent`, `WebhookLog`, etc.) are owned by the application layer; the *transport* of integration events is owned by adapters.
- **Doc 03**: Already aligned. This doc cites it as the canonical Pattern B example.
- **Future docs**:
  - Doc 05 (ZATCA) will be a Pattern B adapter following this standard.
  - Doc 06 (BFF API) is unaffected (no integration code).
  - Doc 07 (Approval Workflow) is application-level, no adapter.
  - Doc 08 (Monorepo Layout) must reflect the per-adapter package layout from this doc.

---

## 15. Open questions

| # | Question | Default |
|---|---|---|
| Q1 | Should `AutoLeaseNet.Adapters.Common` ship as a NuGet package even though it's monorepo-internal, or just a project reference? | Project reference (faster iteration); convert to NuGet only if extracted to a separate repo later |
| Q2 | Where do **domain events** that adapters emit (e.g. "webhook received") live? | In the application layer, not in the adapter. Adapters call into app-layer event publishers via a port (`IDomainEventPublisher`). |
| Q3 | Should we standardize a single `Result<T, TError>` type across all Pattern B adapters, or let each define its own? | Standardize — `IntegrationResult<T>` in `Adapters.Common`. Vendor-specific error types extend a base `IntegrationError`. |
| Q4 | For Pattern A providers (SMS, Storage), do we need to support multiple providers simultaneously (e.g. fallback if primary fails)? | Phase 1: no — pick one provider per environment via config. Phase 3: introduce `IFailoverSmsSender` decorator if needed. |
| Q5 | Should adapter packages be allowed to depend on EF Core for local persistence (e.g. webhook log, idempotency)? | No — adapters use ports from `Adapters.Common` (`IIdempotencyStore`, `IOutboxStore`). The app layer wires those ports to EF Core implementations. Keeps adapters DB-agnostic. |

---

## 16. Sign-off checklist

- [ ] Hexagonal architecture as the standard approved
- [ ] Pattern A vs Pattern B distinction approved
- [ ] Module layout standard approved (every adapter has the same folder structure)
- [ ] `Add{Name}()` extension contract approved
- [ ] `AutoLeaseNet.Adapters.Common` shared infrastructure approved
- [ ] Integration catalog (table in §10) reviewed — phase assignments correct
- [ ] In-memory companion package mandate approved
- [ ] Health check / OTel / config mandates approved
- [ ] Anti-patterns list endorsed
- [ ] Open questions §15 answered
