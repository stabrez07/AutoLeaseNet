namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Incident severity per Spec 01 §5.6. <see cref="TotalLoss"/> is the
/// <c>RequiresReplacement</c> trigger — the Replacement Saga (Spec 02 §6.5)
/// subscribes to <c>IncidentReportedDomainEvent</c> filtered on this value
/// once the saga lands (deferred from Phase 1).
/// </summary>
public enum IncidentSeverity
{
    Minor = 1,
    Major = 2,
    TotalLoss = 3,
}
