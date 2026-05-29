using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Infrastructure.Reconciliation;

/// <summary>
/// Phase-1 SKELETON check. Iterates the configured
/// <see cref="ReconciliationOptions.TajeerMirrorOptions.TenantIds"/> and, for each,
/// fetches the most recently-updated <see cref="LeaseStatus.Active"/> leases up
/// to <see cref="ReconciliationOptions.TajeerMirrorOptions.MaxLeasesPerCycle"/>,
/// then logs one line per row. **Does not yet call Tajeer**;
/// <c>ITajeerContractClient</c> has no <c>GetAsync</c> today. The real drift
/// comparison lands in a follow-up workstream paired with that method.
///
/// <para>This check exists now to lock the scope-per-tenant pattern + the
/// log shape + the DI registration ergonomics. Future checks (ZATCA chain,
/// stuck OutboxEvents) drop in the same way.</para>
/// </summary>
public sealed partial class TajeerStatusMirrorCheck(
    AutoLeaseNetDbContext db,
    IOptions<ReconciliationOptions> options,
    ILogger<TajeerStatusMirrorCheck> logger) : IReconciliationCheck
{
    public string Name => "Tajeer.StatusMirror";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value.Tajeer;
        if (opts.TenantIds.Length == 0)
        {
            LogNoTenantsConfigured();
            return;
        }

        var totalInspected = 0;
        foreach (var tenantId in opts.TenantIds)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // RLS-aware: scope SESSION_CONTEXT to this tenant for the query.
            using var tenantScope = SystemTenancyScope.For(tenantId);

            var batch = await db.Leases
                .AsNoTracking()
                .Where(l => l.Status == LeaseStatus.Active)
                .OrderByDescending(l => l.UpdatedAtUtc)
                .Take(opts.MaxLeasesPerCycle)
                .Select(l => new { l.Id, l.TajeerContractNumber, l.UpdatedAtUtc })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            LogTenantInspected(tenantId, batch.Count);
            totalInspected += batch.Count;

            foreach (var lease in batch)
            {
                LogLeaseTracked(tenantId, lease.Id, lease.TajeerContractNumber, lease.UpdatedAtUtc);
            }
        }

        LogCycleSummary(opts.TenantIds.Length, totalInspected);
    }

    [LoggerMessage(EventId = 4401, Level = LogLevel.Warning,
        Message = "Tajeer.StatusMirror skipped — no tenant ids configured (Reconciliation:Tajeer:TenantIds).")]
    private partial void LogNoTenantsConfigured();

    [LoggerMessage(EventId = 4402, Level = LogLevel.Information,
        Message = "Tajeer.StatusMirror inspecting {Count} Active lease(s) for tenant {TenantId}.")]
    private partial void LogTenantInspected(Guid tenantId, int count);

    [LoggerMessage(EventId = 4403, Level = LogLevel.Debug,
        Message = "Tajeer.StatusMirror would compare against Tajeer: tenant={TenantId} lease={LeaseId} contractNumber={TajeerContractNumber} updatedAt={UpdatedAtUtc:o}")]
    private partial void LogLeaseTracked(Guid tenantId, Guid leaseId, long? tajeerContractNumber, DateTimeOffset updatedAtUtc);

    [LoggerMessage(EventId = 4404, Level = LogLevel.Information,
        Message = "Tajeer.StatusMirror cycle summary: {TenantCount} tenant(s), {TotalLeases} lease(s) inspected.")]
    private partial void LogCycleSummary(int tenantCount, int totalLeases);
}
