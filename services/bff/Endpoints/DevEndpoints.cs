using System.Security.Claims;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Development-only endpoints. Gated by <c>app.Environment.IsDevelopment()</c> at registration
/// time so they're never exposed in staging or production.
/// </summary>
public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder routes)
    {
        var dev = routes.MapGroup("/dev").WithTags("dev");

        dev.MapGet("/whoami", (HttpContext ctx, ITenantContext tenancy) =>
        {
            var user = ctx.User;
            if (user.Identity is null || !user.Identity.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                IsAuthenticated = true,
                TenantId = user.FindFirst(DevJwtStubHandler.ClaimTenantId)?.Value,
                CustomerId = user.FindFirst(DevJwtStubHandler.ClaimCustomerId)?.Value,
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserType = user.FindFirst(DevJwtStubHandler.ClaimUserType)?.Value,
                BranchIds = user.FindAll(DevJwtStubHandler.ClaimBranchId).Select(c => c.Value).ToArray(),
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                // T2.2 — also echo the resolved ITenantContext to prove DI wiring works.
                Tenancy = new
                {
                    TenantId = tenancy.TenantId.ToString(),
                    CustomerId = tenancy.CustomerId?.ToString(),
                    UserId = tenancy.UserId?.ToString(),
                    tenancy.UserType,
                    BranchIds = tenancy.BranchIds.Select(g => g.ToString()).ToArray(),
                    tenancy.IsInternalStaff,
                    tenancy.IsSystem,
                },
            });
        })
        .RequireAuthorization()
        .WithName("DevWhoami")
        .WithSummary("Echoes the current authenticated user's tenancy claims (Development only)");

        return dev;
    }
}
