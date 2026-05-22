using System.Security.Claims;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using Microsoft.AspNetCore.Http;

namespace AutoLeaseNet.Bff.Tenancy;

/// <summary>
/// BFF implementation of <see cref="ITenantContext"/> that reads claims from the current
/// authenticated principal (set by DevJwtStubHandler in dev, JwtBearer in prod).
///
/// Registered as scoped — one instance per HTTP request, matching HttpContext lifetime.
/// Application/domain code receives this via constructor injection; no static state.
/// </summary>
public sealed class ClaimsTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private readonly IHttpContextAccessor _accessor = accessor
        ?? throw new ArgumentNullException(nameof(accessor));

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid TenantId
    {
        get
        {
            var raw = User?.FindFirst(DevJwtStubHandler.ClaimTenantId)?.Value;
            return Guid.TryParse(raw, out var g)
                ? g
                : throw new InvalidOperationException(
                    "No tenant_id claim on the current request. Did the request bypass authentication?");
        }
    }

    public Guid? CustomerId =>
        Guid.TryParse(User?.FindFirst(DevJwtStubHandler.ClaimCustomerId)?.Value, out var g) ? g : null;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var g) ? g : null;

    public string UserType =>
        User?.FindFirst(DevJwtStubHandler.ClaimUserType)?.Value ?? "UNKNOWN";

    public IReadOnlyList<Guid> BranchIds
    {
        get
        {
            var claims = User?.FindAll(DevJwtStubHandler.ClaimBranchId);
            if (claims is null) return Array.Empty<Guid>();
            return claims
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToArray();
        }
    }

    public bool IsInternalStaff => string.Equals(UserType, "INTERNAL_STAFF", StringComparison.Ordinal);

    public bool IsSystem => string.Equals(UserType, "SYSTEM", StringComparison.Ordinal);
}
