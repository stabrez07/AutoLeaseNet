namespace AutoLeaseNet.Application.Ports.Tenancy;

/// <summary>
/// Establishes a SYSTEM-typed <see cref="Tenancy"/> on an <see cref="AsyncLocal{T}"/>
/// for the duration of the <c>using</c> block. <see cref="ITenancyAccessor"/>
/// implementations consult <see cref="Current"/> before falling back to request
/// claims, so any DbContext connection opened inside the scope gets
/// <c>SESSION_CONTEXT('UserType')='SYSTEM'</c> set by the connection interceptor
/// and bypasses the customer-scoping branch of the RLS predicate.
///
/// <para>Required for two call sites that run outside an HTTP request:</para>
/// <list type="bullet">
///   <item>The demo seeder at app startup (no <c>HttpContext</c> yet).</item>
///   <item>The Tajeer webhook receiver, which is anonymous and must do a
///         cross-tenant <c>Lease</c> lookup by contract number before any tenant
///         is known.</item>
/// </list>
///
/// Scopes nest correctly via <see cref="_previous"/> save-and-restore.
/// </summary>
public sealed class SystemTenancyScope : IDisposable
{
    private static readonly AsyncLocal<Tenancy?> _current = new();

    /// <summary>The currently-active SYSTEM tenancy on this async flow, or null.</summary>
    public static Tenancy? Current => _current.Value;

    private readonly Tenancy? _previous;
    private bool _disposed;

    private SystemTenancyScope(Tenancy tenancy)
    {
        _previous = _current.Value;
        _current.Value = tenancy;
    }

    /// <summary>Push a SYSTEM tenancy for <paramref name="tenantId"/>.</summary>
    public static SystemTenancyScope For(Guid tenantId)
        => new(new Tenancy(tenantId, CustomerId: null, UserType: "SYSTEM"));

    /// <summary>
    /// Push a cross-tenant <c>WEBHOOK_BOOTSTRAP</c> tenancy. Used by the Tajeer webhook
    /// receiver to do its anonymous cross-tenant Lease lookup before any per-tenant
    /// scope can be established. The RLS predicate recognizes
    /// <c>UserType = 'WEBHOOK_BOOTSTRAP'</c> as a see-all override; <see cref="Tenancy.TenantId"/>
    /// is <see cref="Guid.Empty"/> as a placeholder (never matched against rows).
    ///
    /// <para>Phase 2 will encode the tenant in the registered webhook URL and retire
    /// both the bootstrap scope and the predicate clause that honours it.</para>
    /// </summary>
    public static SystemTenancyScope ForWebhookBootstrap()
        => new(new Tenancy(Guid.Empty, CustomerId: null, UserType: "WEBHOOK_BOOTSTRAP"));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current.Value = _previous;
    }
}
