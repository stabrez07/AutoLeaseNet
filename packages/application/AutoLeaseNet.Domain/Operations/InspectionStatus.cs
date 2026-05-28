namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Lifecycle per Spec 02 §4.6. IN_PROGRESS is the only mutable state; COMPLETED is
/// immutable and the only state that satisfies the Lease invariants (Spec 01 §invariant 2/3);
/// ABANDONED exists for offline-mobile flows where the inspector starts the form but
/// never finishes (24h timeout — Phase 1 records the state, the timer itself lands later).
/// </summary>
public enum InspectionStatus
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3,
}
