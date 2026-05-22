using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using AutoLeaseNet.Bff.Endpoints;
using AutoLeaseNet.Bff.Middleware;
using AutoLeaseNet.Bff.Tenancy;

var builder = WebApplication.CreateBuilder(args);

// === Logging & Telemetry ===
// Serilog configured in appsettings; OpenTelemetry hooked up below.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// === Authentication ===
// Phase 1: DevJwtStubHandler (header-based, dev/CI/staging only).
// AddDevJwtStub throws if env == Production — by design, the app refuses to start
// if real Entra ID JWT bearer isn't wired (Phase 2+). This is intentional fail-loud
// behaviour (T2.6 startup assertion).
builder.Services
    .AddAuthentication(DevJwtStubHandler.SchemeName)
    .AddDevJwtStub(builder.Environment);
builder.Services.AddAuthorization();

// === Tenancy (ITenantContext resolved from current request claims) ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();

// === Application & Infrastructure ===
// builder.Services.AddAutoLeaseNetInfrastructure(builder.Configuration);

// === Adapters (per doc 04 — each via its own AddXxx() extension method) ===
// Phase 1 wiring will be added as each adapter is built:
// builder.Services.AddTajeer(builder.Configuration.GetSection("Tajeer"));
// builder.Services.AddInMemorySms();
// builder.Services.AddInMemoryStorage();
// builder.Services.AddInMemoryCache();
// builder.Services.AddInMemoryEmail();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseTenancy();

app.MapHealthChecks("/health/liveness");
app.MapHealthChecks("/health/readiness");

// === API v1 ===
var v1 = app.MapGroup("/api/v1");
v1.MapHealthRoot();

if (app.Environment.IsDevelopment())
{
    v1.MapDevEndpoints();
}

app.Run();

/// <summary>Marker for integration tests (WebApplicationFactory&lt;Program&gt;).</summary>
public sealed partial class Program;
