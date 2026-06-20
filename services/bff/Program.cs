using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Adapters.Email.InMemory;
using AutoLeaseNet.Adapters.Images.InMemory;
using AutoLeaseNet.Adapters.Pdf.QuestPdf;
using AutoLeaseNet.Adapters.Seed;
using AutoLeaseNet.Adapters.Sms.InMemory;
using AutoLeaseNet.Adapters.Storage.InMemory;
using AutoLeaseNet.Adapters.Tajeer;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.InMemory;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using AutoLeaseNet.Adapters.Zatca.InMemory;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Seeding;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Bff.Health;
using AutoLeaseNet.Bff.Middleware;
using AutoLeaseNet.Bff.Tenancy;
using AutoLeaseNet.Infrastructure;
using AutoLeaseNet.Infrastructure.Outbox;
using AutoLeaseNet.Infrastructure.Reconciliation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// === Logging & Telemetry ===
// Serilog configured in appsettings; OpenTelemetry hooked up below.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var readyTags = new[] { "ready" };
builder.Services
    .AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sql", tags: readyTags)
    .AddCheck<ZatcaHealthCheck>("zatca", tags: readyTags);

// === Authentication ===
// Phase 1: DevJwtStubHandler (header-based, dev/CI/staging only).
// AddDevJwtStub throws if env == Production — by design, the app refuses to start
// if real Entra ID JWT bearer isn't wired (Phase 2+). This is intentional fail-loud
// behaviour (T2.6 startup assertion).
builder.Services
    .AddAuthentication(DevJwtStubHandler.SchemeName)
    .AddDevJwtStub(builder.Environment);
builder.Services.AddAuthorization();

// === CORS (Development only — allows the Next.js portals at :3000/:3001 to call the BFF) ===
const string DevCorsPolicy = "DevPortals";
if (builder.Environment.IsDevelopment())
{
    var devOrigins = builder.Configuration.GetSection("Cors:DevOrigins").Get<string[]>()
        ?? new[] { "http://localhost:3000", "http://localhost:3001" };
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevCorsPolicy, policy => policy
            .WithOrigins(devOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });
}

// === Tenancy (ITenantContext + ITenancyAccessor resolved from current request claims) ===
// ITenantContext throws when no tenant is in scope — Application/Domain handlers depend on it.
// ITenancyAccessor returns null on anonymous requests and honours SystemTenancyScope first —
// the connection interceptor uses it to set SQL SESSION_CONTEXT for RLS without breaking
// the anonymous webhook path or the demo seeder.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
builder.Services.AddScoped<ITenancyAccessor, ClaimsAndSystemTenancyAccessor>();

// === Application & Infrastructure ===
builder.Services.AddAutoLeaseNetInfrastructure(builder.Configuration);
// Outbox capture-side is wired by AddAutoLeaseNetInfrastructure (interceptor registered
// inside AddAutoLeaseNetDbContext); the drain BackgroundService + repository live here.
builder.Services.AddOutbox(builder.Configuration.GetSection(OutboxOptions.SectionName));
// Reconciliation: periodic drift-detection job (Phase-1 stub check: Tajeer status mirror).
builder.Services.AddReconciliation(builder.Configuration.GetSection(ReconciliationOptions.SectionName));
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<SaveContractCommand>();
    // Lookup-query handlers live in Infrastructure so they can use DbContext directly.
    cfg.RegisterServicesFromAssemblyContaining<AutoLeaseNet.Infrastructure.Persistence.AutoLeaseNetDbContext>();
});

// === Adapters (per doc 04 — each via its own AddXxx() extension method) ===
// AddTajeerWithModeSwitch wires the named HttpClient + auth + resilience and then,
// based on Tajeer:Mode (Real | InMemory), binds ITajeerContractClient to the right impl.
builder.Services.AddTajeerWithModeSwitch(builder.Configuration.GetSection(TajeerOptions.SectionName));
// ZATCA Phase-1 wires the shape only — the Real client is a clear-error stub until the Week-4
// UBL+ECDSA+TLV workstream lands. Default Zatca:Mode=InMemory in dev keeps the demo path green.
builder.Services.AddZatcaWithModeSwitch(builder.Configuration.GetSection(ZatcaOptions.SectionName));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddInMemoryCache();
builder.Services.AddInMemorySms();
builder.Services.AddQuestPdfRenderer();
builder.Services.AddInMemoryEmail();
builder.Services.AddSeed(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddInMemoryVehicleImages();
builder.Services.AddInMemoryStorage();
// Future: AddInMemoryStorage() etc.

var app = builder.Build();

// C7 — Demo / Dev seeding hook. Runs once on startup; the seeder itself short-circuits
// if rows already exist for the configured tenant.
//
// The SystemTenancyScope.For(seeder.TenantId) is *required* once RLS is on: without it
// the connection interceptor would skip SESSION_CONTEXT (no HTTP request, no claims),
// the predicate would evaluate to false, and the seeder's newly-inserted rows would be
// hidden from the very same transaction that inserted them — leading to bogus
// "already seeded?" checks and broken startup. See workstream
// Plans/workstreams/2026-05-29-day-9-rls-tenant-isolation/plan.md.
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<IDataSeeder>();
    using var systemScope = SystemTenancyScope.For(seeder.TenantId);
    await seeder.SeedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}
app.UseTenancy();

// Liveness = "process is up" (no downstream checks). Readiness = "ready to serve traffic"
// (all "ready"-tagged checks must pass; today that's just SQL).
app.MapHealthChecks("/health/liveness", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/readiness", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapGet("/", () => Results.Ok(new
{
    service = "AutoLeaseNet.Bff",
    version = "v1",
    health = new
    {
        liveness = "/health/liveness",
        readiness = "/health/readiness"
    },
    apiBase = "/api/v1"
}));

// === API v1 ===
var v1 = app.MapGroup("/api/v1");
v1.MapHealthRoot();
v1.MapLookupEndpoints();
v1.MapInspectionEndpoints();
v1.MapIncidentEndpoints();
v1.MapLeaseEndpoints();
v1.MapQuotationEndpoints();
v1.MapPricingSetupEndpoints();
v1.MapQuotationPricingSetupEndpoints();
v1.MapInvoiceEndpoints();
v1.MapZatcaStatusEndpoints();
v1.MapMeEndpoints();
v1.MapTajeerWebhookEndpoints();
v1.MapCustomerEndpoints();
v1.MapVehicleEndpoints();
v1.MapDriverEndpoints();
v1.MapBranchEndpoints();

if (app.Environment.IsDevelopment())
{
    v1.MapDevEndpoints();
}

app.Run();

/// <summary>Marker for integration tests (WebApplicationFactory&lt;Program&gt;).</summary>
public sealed partial class Program;
