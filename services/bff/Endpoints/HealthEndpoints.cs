using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthRoot(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/ping", () => Results.Ok(new
        {
            service = "AutoLeaseNet.Bff",
            version = typeof(HealthEndpoints).Assembly.GetName().Version?.ToString(),
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("Ping")
        .WithSummary("Lightweight liveness check returning service identification");
        return routes;
    }
}
