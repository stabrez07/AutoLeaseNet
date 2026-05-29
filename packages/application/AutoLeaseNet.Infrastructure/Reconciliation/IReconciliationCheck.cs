namespace AutoLeaseNet.Infrastructure.Reconciliation;

/// <summary>
/// One reconciliation pass run by <see cref="ReconciliationService"/> on every
/// cycle. Implementations are resolved from a fresh DI scope each cycle, so
/// they can safely use scoped services (DbContext, repositories) and they
/// should NOT cache state across cycles.
///
/// <para>Implementations must be **read-only** unless they own the system of
/// record for the data they touch. Drift detection logs + (future) alerts;
/// auto-correction is an explicit per-check policy decision and not the
/// default.</para>
///
/// <para>Implementations must be **idempotent**: the service may run them
/// twice in quick succession if the operator triggers a manual cycle.</para>
/// </summary>
public interface IReconciliationCheck
{
    /// <summary>Short, stable identifier used in logs and (future) metrics.</summary>
    string Name { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
