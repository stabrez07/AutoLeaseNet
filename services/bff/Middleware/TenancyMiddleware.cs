using AutoLeaseNet.Bff.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Bff.Middleware;

/// <summary>
/// Per-request middleware that enforces every authenticated request carries a valid
/// <c>tenant_id</c> claim. Anonymous endpoints (health, ping, login) pass through unchanged.
///
/// Also opens a logging scope tagged with the tenant id so adapter / handler logs are
/// auto-tagged for filtering in App Insights.
/// </summary>
public sealed class TenancyMiddleware(RequestDelegate next, ILogger<TenancyMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tenantClaim = user.FindFirst(DevJwtStubHandler.ClaimTenantId);
        if (tenantClaim is null || !Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            // Authenticated but no usable tenant — refuse the request. Phase-2-protected
            // endpoints assume tenancy; serving without it would corrupt RLS scoping.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Authenticated principal is missing a valid tenant_id claim.");
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenantId,
        });

        await next(context);
    }
}

public static class TenancyMiddlewareExtensions
{
    public static IApplicationBuilder UseTenancy(this IApplicationBuilder app)
        => app.UseMiddleware<TenancyMiddleware>();
}
