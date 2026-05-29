namespace AutoLeaseNet.Infrastructure.Reconciliation;

/// <summary>
/// Configuration for <see cref="ReconciliationService"/>. Defaults match Plan
/// 02 Day-20 guidance (15-minute cadence). Tests typically set
/// <see cref="Enabled"/> false so the loop doesn't run in WebApplicationFactory
/// fixtures.
/// </summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between reconciliation cycles. Default 15 minutes.</summary>
    public int IntervalSeconds { get; set; } = 900;

    /// <summary>
    /// Random extra seconds added to each delay (0..JitterSeconds). Prevents
    /// multi-instance reconciliations from stacking. Single-instance Phase 1
    /// doesn't require it; default 30 is harmless.
    /// </summary>
    public int JitterSeconds { get; set; } = 30;

    /// <summary>Configuration for the Tajeer status-mirror check.</summary>
    public TajeerMirrorOptions Tajeer { get; set; } = new();

    public sealed class TajeerMirrorOptions
    {
        /// <summary>Most recent Active leases to inspect per cycle, per tenant.</summary>
        public int MaxLeasesPerCycle { get; set; } = 50;

        /// <summary>
        /// Tenant ids to mirror. Phase-1 is single-tenant per environment so this
        /// is typically the seeded tenant. Phase 2 derives this from the
        /// Customers/Leases table.
        /// </summary>
        public Guid[] TenantIds { get; set; } = Array.Empty<Guid>();
    }
}
