using System.Data.Common;
using AutoLeaseNet.Application.Ports.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core <see cref="DbConnectionInterceptor"/> that writes the current request's
/// tenancy into SQL <c>SESSION_CONTEXT</c> immediately after a connection opens.
/// Combined with the RLS predicate added in migration
/// <c>Add_RLS_TenancyPolicy</c>, this makes tenant isolation a property of the
/// database engine rather than of every WHERE clause.
///
/// <para>Behavioural contract:</para>
/// <list type="bullet">
///   <item>Non-<see cref="SqlConnection"/> connections (EF InMemory test provider)
///         get a no-op. All existing handler / endpoint tests use InMemory, so
///         enabling this interceptor must not break them.</item>
///   <item>When <see cref="ITenancyAccessor.Current"/> is <c>null</c> (anonymous
///         request: webhook receiver before its cross-tenant lookup, health
///         endpoint), no SESSION_CONTEXT is set. The RLS predicate then evaluates
///         to false and any business query returns zero rows — a safe-by-default
///         failure mode. Callers that need to do anonymous cross-tenant work
///         (seeder, webhook) wrap themselves in <see cref="SystemTenancyScope"/>
///         which the accessor honours first.</item>
///   <item>Re-opening the same physical connection inside one async flow can fire
///         this interceptor twice. <c>sp_set_session_context @read_only=1</c>
///         throws SqlException 15664 on the second set. We swallow it — the
///         intended tenancy is already in place.</item>
/// </list>
/// </summary>
public sealed partial class TenancyConnectionInterceptor(
    ITenancyAccessor accessor,
    ILogger<TenancyConnectionInterceptor> logger) : DbConnectionInterceptor
{
    private const int SqlErrorReadOnlySessionContextAlreadySet = 15664;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection is not SqlConnection sql) return;

        var tenancy = accessor.Current;
        if (tenancy is null) return;

        try
        {
            await SqlSessionContext.SetTenancyAsync(
                sql,
                tenancy.TenantId,
                tenancy.CustomerId,
                tenancy.UserType,
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorReadOnlySessionContextAlreadySet)
        {
            // Already set on this physical connection (rare: nested scope reuse).
            // The previously-set tenancy is identical for this request, so this is benign.
            LogReadOnlyAlreadySet(logger, tenancy.TenantId);
        }
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug,
        Message = "SESSION_CONTEXT('TenantId') already set on connection for {TenantId}; ignoring duplicate set.")]
    private static partial void LogReadOnlyAlreadySet(ILogger logger, Guid tenantId);
}
