namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Incident classification per Spec 01 §5.6. Drives downstream rules — only
/// <see cref="TrafficAccident"/> requires a PoliceReportNumber for full
/// resolution, only <see cref="TotalLoss"/>-severity events trigger the
/// Replacement Saga.
/// </summary>
public enum IncidentType
{
    TrafficAccident = 1,
    NonTrafficDamage = 2,
    Breakdown = 3,
    Theft = 4,
    Vandalism = 5,
    Other = 99,
}
