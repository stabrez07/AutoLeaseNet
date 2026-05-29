namespace AutoLeaseNet.Application.Ports.Tenancy;

/// <summary>
/// Resolves the tenancy that should be applied to SQL <c>SESSION_CONTEXT</c> on the
/// current connection. Differs from <see cref="ITenantContext"/> in two ways:
///
/// <list type="number">
///   <item>Returns <c>null</c> when no tenancy is in scope (anonymous endpoints) instead
///         of throwing. The connection interceptor needs to no-op gracefully on
///         anonymous requests rather than failing.</item>
///   <item>Honours <see cref="SystemTenancyScope"/> overrides for callers running
///         outside an HTTP request (the demo seeder at startup, the webhook receiver's
///         cross-tenant Lease lookup). Those callers push a SYSTEM tenancy onto an
///         AsyncLocal and the accessor returns it in preference to request claims.</item>
/// </list>
///
/// Implemented in the BFF as <c>ClaimsAndSystemTenancyAccessor</c>.
/// </summary>
public interface ITenancyAccessor
{
    Tenancy? Current { get; }
}

/// <summary>
/// Snapshot of the tenancy values that get written into SQL <c>SESSION_CONTEXT</c> —
/// the same three keys the RLS predicate function reads (<c>TenantId</c>,
/// <c>CustomerId</c>, <c>UserType</c>).
/// </summary>
public sealed record Tenancy(Guid TenantId, Guid? CustomerId, string UserType);
