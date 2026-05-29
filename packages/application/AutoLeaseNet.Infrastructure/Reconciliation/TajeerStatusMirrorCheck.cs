using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Tajeer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Infrastructure.Reconciliation;

/// <summary>
/// Reconciliation check that walks the most recently-updated <see cref="LeaseStatus.Active"/>
/// (and <see cref="LeaseStatus.Extended"/>) leases per configured tenant, calls Tajeer's
/// §6.3 GetContract endpoint via <see cref="ITajeerContractClient.GetAsync"/>, and logs
/// drift between Tajeer's view and our local mirror.
///
/// <para>
/// <b>Phase 1 is detect-only.</b> No mutation, no auto-correct. Acting on drift is a
/// product decision (Tajeer-wins per CLAUDE.md §5, but auto-applying risks masking
/// upstream bugs — a missed webhook is a real signal that needs investigation, not a
/// silent fix). Phase 2 lands an action policy.
/// </para>
///
/// <para>
/// Failure semantics per row:
/// <list type="bullet">
///   <item>Tajeer returns success + matching status → debug log.</item>
///   <item>Tajeer returns success + differing status → warn log with both sides ("drift").</item>
///   <item>Tajeer returns vendor failure (e.g. contract.not_found) → warn log (drift signal).</item>
///   <item>Tajeer returns transient failure → debug log + continue (next cycle retries).</item>
///   <item>Mapper throws <see cref="InvalidTajeerStatusException"/> → warn log + continue.</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class TajeerStatusMirrorCheck(
    AutoLeaseNetDbContext db,
    ITajeerContractClient tajeerClient,
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

        var grandInspected = 0;
        var grandDrifts = 0;
        var grandErrors = 0;

        foreach (var tenantId in opts.TenantIds)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // RLS-aware: scope SESSION_CONTEXT to this tenant for the query + any later calls.
            using var tenantScope = SystemTenancyScope.For(tenantId);

            var batch = await db.Leases
                .AsNoTracking()
                .Where(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Extended)
                    && l.TajeerContractNumber != null)
                .OrderByDescending(l => l.UpdatedAtUtc)
                .Take(opts.MaxLeasesPerCycle)
                .Select(l => new LocalLeaseSnapshot(
                    l.Id, l.TajeerContractNumber!.Value, l.Status, l.ExtensionCount, l.UpdatedAtUtc))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var (inspected, drifts, errors) = await CompareBatchAsync(tenantId, batch, cancellationToken)
                .ConfigureAwait(false);

            LogTenantSummary(tenantId, inspected, drifts, errors);

            grandInspected += inspected;
            grandDrifts += drifts;
            grandErrors += errors;
        }

        LogCycleSummary(opts.TenantIds.Length, grandInspected, grandDrifts, grandErrors);
    }

    private async Task<(int Inspected, int Drifts, int Errors)> CompareBatchAsync(
        Guid tenantId,
        IReadOnlyList<LocalLeaseSnapshot> batch,
        CancellationToken ct)
    {
        var inspected = 0;
        var drifts = 0;
        var errors = 0;

        foreach (var lease in batch)
        {
            if (ct.IsCancellationRequested) break;
            inspected++;

            var get = await tajeerClient.GetAsync(lease.TajeerContractNumber, ct).ConfigureAwait(false);
            if (!get.IsSuccess)
            {
                if (get.IsTransient)
                {
                    LogTransientFailure(tenantId, lease.Id, lease.TajeerContractNumber, get.ErrorCode, get.ErrorMessage);
                }
                else
                {
                    drifts++;
                    LogVendorFailureDrift(tenantId, lease.Id, lease.TajeerContractNumber, lease.LocalStatus,
                        get.ErrorCode, get.ErrorMessage);
                }
                continue;
            }

            try
            {
                var raw = TajeerStatusMapper.FromTajeer(
                    get.Value!.ContractStatusCode,
                    get.Value.SuspensionReasonCode,
                    get.Value.ClosureReasonCode);
                var vendorAsLocal = TajeerStatusMapper.ApplyLocalRefinements(raw, lease.LocalExtensionCount);

                if (vendorAsLocal == lease.LocalStatus)
                {
                    LogMatched(tenantId, lease.Id, lease.TajeerContractNumber, lease.LocalStatus);
                }
                else
                {
                    drifts++;
                    LogStatusDrift(tenantId, lease.Id, lease.TajeerContractNumber, lease.LocalStatus, vendorAsLocal);
                }
            }
            catch (InvalidTajeerStatusException ex)
            {
                errors++;
                LogInvalidTajeerStatus(tenantId, lease.Id, lease.TajeerContractNumber,
                    ex.ContractStatusCode, ex.SuspensionReasonCode, ex.ClosureReasonCode);
            }
        }

        return (inspected, drifts, errors);
    }

    private sealed record LocalLeaseSnapshot(
        Guid Id,
        long TajeerContractNumber,
        LeaseStatus LocalStatus,
        int LocalExtensionCount,
        DateTimeOffset UpdatedAtUtc);

    [LoggerMessage(EventId = 4401, Level = LogLevel.Warning,
        Message = "Tajeer.StatusMirror skipped — no tenant ids configured (Reconciliation:Tajeer:TenantIds).")]
    private partial void LogNoTenantsConfigured();

    [LoggerMessage(EventId = 4402, Level = LogLevel.Information,
        Message = "Tajeer.StatusMirror tenant {TenantId}: inspected={Inspected} drifts={Drifts} errors={Errors}.")]
    private partial void LogTenantSummary(Guid tenantId, int inspected, int drifts, int errors);

    [LoggerMessage(EventId = 4403, Level = LogLevel.Debug,
        Message = "Tajeer.StatusMirror match: tenant={TenantId} lease={LeaseId} contractNumber={ContractNumber} status={LocalStatus}.")]
    private partial void LogMatched(Guid tenantId, Guid leaseId, long contractNumber, LeaseStatus localStatus);

    [LoggerMessage(EventId = 4404, Level = LogLevel.Warning,
        Message = "Tajeer.StatusMirror DRIFT: tenant={TenantId} lease={LeaseId} contractNumber={ContractNumber} local={LocalStatus} vendor(asLocal)={VendorAsLocal}.")]
    private partial void LogStatusDrift(Guid tenantId, Guid leaseId, long contractNumber, LeaseStatus localStatus, LeaseStatus vendorAsLocal);

    [LoggerMessage(EventId = 4405, Level = LogLevel.Debug,
        Message = "Tajeer.StatusMirror transient failure: tenant={TenantId} lease={LeaseId} contractNumber={ContractNumber} errorCode={ErrorCode} errorMessage={ErrorMessage}. Will retry next cycle.")]
    private partial void LogTransientFailure(Guid tenantId, Guid leaseId, long contractNumber, string? errorCode, string? errorMessage);

    [LoggerMessage(EventId = 4406, Level = LogLevel.Warning,
        Message = "Tajeer.StatusMirror VENDOR-FAILURE DRIFT: tenant={TenantId} lease={LeaseId} contractNumber={ContractNumber} local={LocalStatus} errorCode={ErrorCode} errorMessage={ErrorMessage}.")]
    private partial void LogVendorFailureDrift(Guid tenantId, Guid leaseId, long contractNumber, LeaseStatus localStatus, string? errorCode, string? errorMessage);

    [LoggerMessage(EventId = 4407, Level = LogLevel.Warning,
        Message = "Tajeer.StatusMirror UNRECOGNISED VENDOR STATE: tenant={TenantId} lease={LeaseId} contractNumber={ContractNumber} contractStatusCode={ContractStatusCode} suspensionReasonCode={SuspensionReasonCode} closureReasonCode={ClosureReasonCode}. Mapper rejected the triple; row skipped.")]
    private partial void LogInvalidTajeerStatus(Guid tenantId, Guid leaseId, long contractNumber, int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode);

    [LoggerMessage(EventId = 4408, Level = LogLevel.Information,
        Message = "Tajeer.StatusMirror cycle summary: {TenantCount} tenant(s), {TotalLeases} lease(s) inspected, {TotalDrifts} drift(s), {TotalErrors} error(s).")]
    private partial void LogCycleSummary(int tenantCount, int totalLeases, int totalDrifts, int totalErrors);
}
