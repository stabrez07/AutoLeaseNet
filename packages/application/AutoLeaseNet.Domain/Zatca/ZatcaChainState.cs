using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Zatca;

/// <summary>
/// Per-tenant aggregate-of-one tracking the ZATCA Phase-2 PIH chain — the Previous
/// Invoice Hash threaded through every clearance submission so the gateway can detect
/// gaps or tampering (Spec 02 §4.5 + CLAUDE.md §6).
///
/// <para>
/// <b>Invariant the saga must keep true</b> (NOT enforced here — the entity is
/// intentionally passive): <c>AdvanceTo</c> may only be called by the
/// <c>ZatcaSubmissionSaga</c> when the adapter response carries
/// <see cref="Adapters.Zatca.Configuration.ZatcaResultStatus.Cleared"/>,
/// <see cref="Adapters.Zatca.Configuration.ZatcaResultStatus.WarningCleared"/>, or
/// <see cref="Adapters.Zatca.Configuration.ZatcaResultStatus.Reported"/>. A
/// <see cref="Adapters.Zatca.Configuration.ZatcaResultStatus.Rejected"/> response MUST
/// NOT advance the chain — that's how chain-break detection works.
/// </para>
///
/// <para>
/// Mirroring the existing pattern (e.g. <c>Lease.MarkIssued</c> accepts any timestamp;
/// the saga calls it only when Tajeer confirmed issuance) keeps the entity simple and
/// the policy testable in saga unit tests instead of brittle entity-level checks.
/// </para>
/// </summary>
public sealed class ZatcaChainState : Entity
{
    /// <summary>
    /// SHA-256 of the most recently cleared invoice (Base64). <c>null</c> means no
    /// invoice has cleared yet — the very first submission for the tenant carries the
    /// ZATCA-mandated "initial PIH" sentinel string (Spec 02 §4.5); subsequent
    /// submissions carry the value stored here.
    /// </summary>
    public string? LastClearedInvoiceHash { get; private set; }

    /// <summary>UTC instant of the last cleared submission; <c>null</c> if no clearance has happened.</summary>
    public DateTimeOffset? LastClearedAtUtc { get; private set; }

    private ZatcaChainState() { }

    /// <summary>
    /// Factory used by <c>IZatcaChainStateRepository.GetOrCreateAsync</c> when no row
    /// exists for the tenant yet. The created row starts with no cleared hash — the
    /// saga's first <c>AdvanceTo</c> call will set both fields.
    /// </summary>
    public static ZatcaChainState ForNewTenant(Guid tenantId, DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        return new ZatcaChainState
        {
            TenantId = tenantId,
            LastClearedInvoiceHash = null,
            LastClearedAtUtc = null,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Move the chain forward to a newly cleared invoice hash. Caller (saga) is
    /// responsible for only invoking on cleared / reported / warning-cleared results.
    /// </summary>
    public void AdvanceTo(string newClearedHash, DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newClearedHash);
        LastClearedInvoiceHash = newClearedHash;
        LastClearedAtUtc = occurredAtUtc;
        UpdatedAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// Operator-only recovery: clear the chain back to the "no clearance yet" state.
    /// Used when a confirmed chain break has been reconciled with ZATCA and the next
    /// submission needs to restart with the initial PIH sentinel.
    /// </summary>
    public void Reset(DateTimeOffset nowUtc)
    {
        LastClearedInvoiceHash = null;
        LastClearedAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }
}
