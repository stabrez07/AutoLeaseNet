using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AutoLeaseNet.Bff.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// === Logging & Telemetry ===
// Serilog configured in appsettings; OpenTelemetry hooked up below.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

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

app.MapHealthChecks("/health/liveness");
app.MapHealthChecks("/health/readiness");

// API v1 endpoint groups (registered as Phase 1 work lands)
var v1 = app.MapGroup("/api/v1");
v1.MapHealthRoot();

app.Run();

/// <summary>Marker for integration tests (WebApplicationFactory&lt;Program&gt;).</summary>
public sealed partial class Program;
