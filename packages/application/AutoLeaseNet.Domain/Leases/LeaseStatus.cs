namespace AutoLeaseNet.Domain.Leases;

/// <summary>
/// Local mirror of Tajeer's contract lifecycle. Per Spec 03 §7 — we mirror Tajeer's status
/// and refine locally (Extended is a local-only distinction; Tajeer keeps issued contracts
/// at code 4 even after extension).
///
/// Tajeer is system of record for issued leases (CLAUDE.md §5). Saga / webhook drives
/// every transition. Don't update <see cref="Lease.Status"/> from random code.
/// </summary>
public enum LeaseStatus
{
    /// <summary>Local draft before any Tajeer call.</summary>
    Draft = 0,

    /// <summary>Tajeer SaveContract returned 4xx business error — user must correct and retry.</summary>
    SaveFailed = 1,

    /// <summary>Tajeer accepted Save → contract saved with token + issuanceUrl, awaiting renter sign-off.</summary>
    PendingIssuance = 2,

    /// <summary>Renter completed issuance → Tajeer status 4 (Issued) → our active state.</summary>
    Active = 3,

    /// <summary>Local refinement of Active when one or more extensions have been applied.</summary>
    Extended = 4,

    /// <summary>Tajeer status 3 — temporarily paused (non-traffic accident, financial claims).</summary>
    Suspended = 5,

    /// <summary>Tajeer status 2 — contract closed (natural expiry, mutual agreement, damage).</summary>
    Closed = 6,

    /// <summary>Tajeer status 5 — cancelled before issuance.</summary>
    Cancelled = 7,

    /// <summary>Local: 12-hour Tajeer save expiry elapsed without issuance; clone-and-resave required.</summary>
    ExpiredDraft = 8,
}
