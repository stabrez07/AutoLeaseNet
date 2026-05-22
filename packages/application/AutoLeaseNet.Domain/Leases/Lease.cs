using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Leases;

/// <summary>
/// Minimal Week-1 Lease aggregate root — just enough to round-trip the Tajeer SaveContract
/// happy path (T5.x) and the inbound webhook update (T6.x). Phase 1 / Week 2 expands this
/// with renter, vehicle, payment, lifecycle dates, etc.
/// </summary>
public sealed class Lease : Entity
{
    /// <summary>Optional — populated for B2B leases tied to a Customer (Fleet account).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Tajeer's contract identifier — null until SaveContract has succeeded.</summary>
    public long? TajeerContractNumber { get; private set; }

    /// <summary>The URL Tajeer returns for the renter to complete (issue) the contract.</summary>
    public string? IssuanceUrl { get; private set; }

    /// <summary>Local mirror of Tajeer's contract status.</summary>
    public LeaseStatus Status { get; private set; }

    /// <summary>Set when Tajeer pushes the LeaseIssued webhook (T6.6).</summary>
    public DateTimeOffset? IssuedAtUtc { get; private set; }

    // EF requires a parameterless constructor; keep private so the only public entry point
    // is the factory below (CreatePending) which sets every invariant up front.
    private Lease() { }

    /// <summary>
    /// Factory for the Saved-but-not-yet-Issued state — used when the Tajeer SaveContract
    /// call returns success with a contract number + issuance URL.
    /// </summary>
    public static Lease CreatePending(
        Guid tenantId,
        Guid? customerId,
        long tajeerContractNumber,
        string issuanceUrl,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tajeerContractNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuanceUrl);

        var lease = new Lease
        {
            TenantId = tenantId,
            CustomerId = customerId,
            TajeerContractNumber = tajeerContractNumber,
            IssuanceUrl = issuanceUrl,
            Status = LeaseStatus.PendingIssuance,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
        return lease;
    }

    /// <summary>
    /// Transition: PendingIssuance → Active when Tajeer's LeaseIssued webhook arrives.
    /// Idempotent: re-entry from the same state is a no-op (defends against webhook replays).
    /// </summary>
    public void MarkIssued(DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.Active) return;
        if (Status != LeaseStatus.PendingIssuance)
        {
            throw new InvalidOperationException(
                $"Cannot mark Lease {Id} as Issued from status {Status}.");
        }
        Status = LeaseStatus.Active;
        IssuedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
