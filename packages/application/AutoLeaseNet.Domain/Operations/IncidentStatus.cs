namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Incident lifecycle states per Spec 02 §4.7. Allowed transitions:
/// <c>Open → UnderInvestigation | Resolved | Closed</c>;
/// <c>UnderInvestigation → Resolved | Closed</c>;
/// <c>Resolved → Closed</c>.
/// <c>Closed</c> is terminal.
/// </summary>
public enum IncidentStatus
{
    Open = 1,
    UnderInvestigation = 2,
    Resolved = 3,
    Closed = 4,
}
