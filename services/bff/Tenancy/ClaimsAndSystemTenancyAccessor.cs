using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using Microsoft.AspNetCore.Http;

// Disambiguates against the BFF's own AutoLeaseNet.Bff.Tenancy namespace.
using TenancyValue = AutoLeaseNet.Application.Ports.Tenancy.Tenancy;

namespace AutoLeaseNet.Bff.Tenancy;

/// <summary>
/// BFF implementation of <see cref="ITenancyAccessor"/>. Returns — in priority order:
///
/// <list type="number">
///   <item><see cref="SystemTenancyScope.Current"/> when set (demo seeder during app
///         startup, Tajeer webhook cross-tenant resolution).</item>
///   <item>A <see cref="Tenancy"/> built from the current request's claims, when an
///         authenticated principal carries a parseable <c>tenant_id</c>.</item>
///   <item><c>null</c> otherwise (anonymous endpoints: health, swagger, the public
///         webhook surface before its bypass kicks in).</item>
/// </list>
///
/// Critically, returning <c>null</c> is safe: the connection interceptor skips
/// setting SESSION_CONTEXT, the RLS predicate evaluates to false, and any
/// business query returns zero rows. There is no path where this accessor
/// silently leaks cross-tenant data.
/// </summary>
public sealed class ClaimsAndSystemTenancyAccessor(IHttpContextAccessor httpContextAccessor)
    : ITenancyAccessor
{
    public TenancyValue? Current
    {
        get
        {
            if (SystemTenancyScope.Current is { } systemScope) return systemScope;

            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            var tenantValue = user.FindFirst(DevJwtStubHandler.ClaimTenantId)?.Value;
            if (!Guid.TryParse(tenantValue, out var tenantId)) return null;

            Guid? customerId = Guid.TryParse(
                user.FindFirst(DevJwtStubHandler.ClaimCustomerId)?.Value, out var cid)
                ? cid
                : null;

            var userType = user.FindFirst(DevJwtStubHandler.ClaimUserType)?.Value ?? "INTERNAL_STAFF";

            return new TenancyValue(tenantId, customerId, userType);
        }
    }
}
