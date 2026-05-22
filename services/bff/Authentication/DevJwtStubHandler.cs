using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Bff.Authentication;

/// <summary>
/// Authentication handler for local development only. Reads tenant identity from
/// <c>X-Dev-*</c> headers and constructs a <see cref="ClaimsPrincipal"/> so the rest of
/// the auth pipeline behaves the same as it will in production with Entra ID.
///
/// Supported headers:
/// - <c>X-Dev-Tenant-Id</c>      (required; if missing → <see cref="AuthenticateResult.NoResult"/>)
/// - <c>X-Dev-User-Id</c>        (optional; defaults to a stable per-process Guid)
/// - <c>X-Dev-User-Type</c>      (optional; default <c>INTERNAL_STAFF</c>)
/// - <c>X-Dev-Customer-Id</c>    (optional; required by app for external user types)
/// - <c>X-Dev-Branch-Ids</c>     (optional; comma-separated)
/// - <c>X-Dev-Roles</c>          (optional; comma-separated)
///
/// MUST NOT be registered in Production — <see cref="DevJwtStubExtensions.AddDevJwtStub"/>
/// enforces this with a throw at startup.
/// </summary>
public sealed class DevJwtStubHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevJwtStub";
    public const string TenantIdHeader = "X-Dev-Tenant-Id";
    public const string UserIdHeader = "X-Dev-User-Id";
    public const string UserTypeHeader = "X-Dev-User-Type";
    public const string CustomerIdHeader = "X-Dev-Customer-Id";
    public const string BranchIdsHeader = "X-Dev-Branch-Ids";
    public const string RolesHeader = "X-Dev-Roles";

    // Claim type names match the BFF API surface (Spec 06 §3.2).
    public const string ClaimTenantId = "tenant_id";
    public const string ClaimUserType = "user_type";
    public const string ClaimCustomerId = "customer_id";
    public const string ClaimBranchId = "branch_id";

    public DevJwtStubHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TenantIdHeader, out var tenantValues)
            || string.IsNullOrWhiteSpace(tenantValues.ToString())
            || !Guid.TryParse(tenantValues.ToString(), out var tenantId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTenantId, tenantId.ToString()),
        };

        var userId = ReadGuid(UserIdHeader) ?? Guid.Empty;
        claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var userType = ReadHeader(UserTypeHeader) ?? "INTERNAL_STAFF";
        claims.Add(new Claim(ClaimUserType, userType));

        var customerId = ReadGuid(CustomerIdHeader);
        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimCustomerId, customerId.Value.ToString()));
        }

        foreach (var branchId in ReadCsvGuids(BranchIdsHeader))
        {
            claims.Add(new Claim(ClaimBranchId, branchId.ToString()));
        }

        foreach (var role in ReadCsv(RolesHeader))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? ReadHeader(string name) =>
        Request.Headers.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v.ToString())
            ? v.ToString()
            : null;

    private Guid? ReadGuid(string name) =>
        ReadHeader(name) is { } s && Guid.TryParse(s, out var g) ? g : null;

    private string[] ReadCsv(string name) =>
        ReadHeader(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

    private Guid[] ReadCsvGuids(string name) =>
        ReadCsv(name)
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToArray();
}
