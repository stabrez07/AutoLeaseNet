using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Incident aggregate root (Spec 01 §5.6 fields, Spec 02 §4.7 state machine).
/// Reports anything that happens to a vehicle on or off a lease — traffic
/// accidents, theft, breakdown — and stages it for follow-up (investigation,
/// resolution, claim tracking, replacement).
///
/// <para>
/// Lifecycle: <see cref="IncidentStatus.Open"/> (initial) →
/// <see cref="IncidentStatus.UnderInvestigation"/> | <see cref="IncidentStatus.Resolved"/> |
/// <see cref="IncidentStatus.Closed"/>; <see cref="IncidentStatus.Resolved"/> →
/// <see cref="IncidentStatus.Closed"/>. Closed is terminal.
/// </para>
///
/// <para>
/// <see cref="LeaseId"/> is nullable because incidents can be reported against
/// a Vehicle even when no lease is active (e.g. a parked-lot collision while
/// the vehicle is on the yard). <see cref="ReplacementLeaseId"/> is set by the
/// Replacement Saga (Spec 02 §6.5) when a swap is triggered.
/// </para>
/// </summary>
public sealed class Incident : Entity
{
    // ─── References ─────────────────────────────────────────────────────────
    public Guid VehicleId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public Guid ReportedByPersonId { get; private set; }
    public Guid? ReplacementLeaseId { get; private set; }

    // ─── Classification ─────────────────────────────────────────────────────
    public IncidentType Type { get; private set; }
    public IncidentSeverity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }

    /// <summary>True when severity = <see cref="IncidentSeverity.TotalLoss"/>; derived at Report time.</summary>
    public bool RequiresReplacement { get; private set; }

    // ─── When ───────────────────────────────────────────────────────────────
    public DateTimeOffset ReportedAtUtc { get; private set; }
    public DateTimeOffset IncidentTimeUtc { get; private set; }
    public DateTimeOffset? InvestigationStartedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    // ─── Location ───────────────────────────────────────────────────────────
    public decimal? LocationLat { get; private set; }
    public decimal? LocationLng { get; private set; }
    public string? LocationDescription { get; private set; }

    // ─── Narrative + paperwork ──────────────────────────────────────────────
    public string Description { get; private set; } = string.Empty;
    public string? PoliceReportNumber { get; private set; }
    public string? InsuranceClaimNumber { get; private set; }
    public string? ResolutionNotes { get; private set; }

    private Incident() { }

    public static Incident Report(ReportIncidentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        if (input.VehicleId == Guid.Empty) throw new ArgumentException("VehicleId required.", nameof(input));
        if (input.ReportedByPersonId == Guid.Empty) throw new ArgumentException("ReportedByPersonId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Description);
        if (input.IncidentTimeUtc > input.NowUtc)
            throw new ArgumentOutOfRangeException(nameof(input), "IncidentTimeUtc cannot be in the future.");

        var requiresReplacement = input.Severity == IncidentSeverity.TotalLoss;

        var incident = new Incident
        {
            TenantId = input.TenantId,
            VehicleId = input.VehicleId,
            LeaseId = input.LeaseId,
            ReportedByPersonId = input.ReportedByPersonId,
            Type = input.Type,
            Severity = input.Severity,
            Status = IncidentStatus.Open,
            RequiresReplacement = requiresReplacement,
            ReportedAtUtc = input.NowUtc,
            IncidentTimeUtc = input.IncidentTimeUtc,
            LocationLat = input.LocationLat,
            LocationLng = input.LocationLng,
            LocationDescription = input.LocationDescription,
            Description = input.Description,
            PoliceReportNumber = input.PoliceReportNumber,
            InsuranceClaimNumber = input.InsuranceClaimNumber,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };

        incident.RaiseDomainEvent(new IncidentReportedDomainEvent(
            IncidentId: incident.Id,
            TenantId: incident.TenantId,
            LeaseId: incident.LeaseId,
            VehicleId: incident.VehicleId,
            Type: incident.Type,
            Severity: incident.Severity,
            ReportedAtUtc: incident.ReportedAtUtc,
            RequiresReplacement: incident.RequiresReplacement));

        return incident;
    }

    public void StartInvestigation(DateTimeOffset nowUtc)
    {
        if (Status == IncidentStatus.UnderInvestigation) return; // idempotent
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException(
                $"Cannot start investigation on Incident {Id} from status {Status}; only Open qualifies.");
        Status = IncidentStatus.UnderInvestigation;
        InvestigationStartedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkResolved(string resolutionNotes, DateTimeOffset nowUtc)
    {
        if (Status == IncidentStatus.Resolved) return; // idempotent
        if (Status == IncidentStatus.Closed)
            throw new InvalidOperationException(
                $"Cannot mark Incident {Id} Resolved: it is already Closed.");
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionNotes);
        Status = IncidentStatus.Resolved;
        ResolvedAtUtc = nowUtc;
        ResolutionNotes = resolutionNotes;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkClosed(DateTimeOffset nowUtc)
    {
        if (Status == IncidentStatus.Closed) return; // idempotent
        Status = IncidentStatus.Closed;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Append claim numbers (police / insurance) as they become available. Only allowed
    /// while the incident is open — Closed incidents are immutable.
    /// </summary>
    public void UpdateClaim(string? policeReportNumber, string? insuranceClaimNumber, DateTimeOffset nowUtc)
    {
        if (Status == IncidentStatus.Closed)
            throw new InvalidOperationException(
                $"Cannot update claim on Incident {Id}: it is Closed (immutable).");
        if (policeReportNumber is not null) PoliceReportNumber = policeReportNumber;
        if (insuranceClaimNumber is not null) InsuranceClaimNumber = insuranceClaimNumber;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Replacement Saga hook (Spec 02 §6.5). Sets the back-reference to the replacement
    /// Lease that this incident triggered. Idempotent on the same lease id; rejects on
    /// mismatch — the link is permanent once set.
    /// </summary>
    public void LinkReplacementLease(Guid replacementLeaseId, DateTimeOffset nowUtc)
    {
        if (replacementLeaseId == Guid.Empty)
            throw new ArgumentException("ReplacementLeaseId required.", nameof(replacementLeaseId));
        if (ReplacementLeaseId == replacementLeaseId) return; // idempotent
        if (ReplacementLeaseId is not null)
            throw new InvalidOperationException(
                $"Incident {Id} is already linked to ReplacementLease {ReplacementLeaseId}; cannot re-link to {replacementLeaseId}.");
        ReplacementLeaseId = replacementLeaseId;
        UpdatedAtUtc = nowUtc;
    }
}

/// <summary>
/// Constructor input for <see cref="Incident.Report"/>. Required fields gate the
/// happy path; location + claim numbers are optional at report time and can be
/// added later via <see cref="Incident.UpdateClaim"/>.
/// </summary>
public sealed record ReportIncidentInput
{
    public required Guid TenantId { get; init; }
    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required Guid ReportedByPersonId { get; init; }
    public required IncidentType Type { get; init; }
    public required IncidentSeverity Severity { get; init; }
    public required DateTimeOffset IncidentTimeUtc { get; init; }
    public required string Description { get; init; }

    public decimal? LocationLat { get; init; }
    public decimal? LocationLng { get; init; }
    public string? LocationDescription { get; init; }
    public string? PoliceReportNumber { get; init; }
    public string? InsuranceClaimNumber { get; init; }

    public required DateTimeOffset NowUtc { get; init; }
}
