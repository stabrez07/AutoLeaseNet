using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Pricing;

/// <summary>
/// Versioned pricing configuration published by tenant administrators.
/// This is the anchor for rate/discount/tax rules used by the internal pricing engine.
/// </summary>
public sealed class PricingVersion : Entity
{
    public string Name { get; private set; } = string.Empty;
    public PricingVersionStatus Status { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    private PricingVersion() { }

    public static PricingVersion CreateDraft(
        Guid tenantId,
        string name,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PricingVersion
        {
            TenantId = tenantId,
            Name = name,
            Status = PricingVersionStatus.Draft,
            EffectiveFromUtc = effectiveFromUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void Publish(DateTimeOffset nowUtc)
    {
        if (Status == PricingVersionStatus.Published) return;
        if (Status == PricingVersionStatus.Retired)
            throw new InvalidOperationException("A retired pricing version cannot be re-published.");

        Status = PricingVersionStatus.Published;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTimeOffset nowUtc, DateTimeOffset? effectiveToUtc = null)
    {
        if (Status == PricingVersionStatus.Retired) return;
        if (effectiveToUtc is not null && effectiveToUtc < EffectiveFromUtc)
            throw new ArgumentOutOfRangeException(nameof(effectiveToUtc), "EffectiveToUtc cannot precede EffectiveFromUtc.");

        EffectiveToUtc = effectiveToUtc;
        Status = PricingVersionStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }
}

public enum PricingVersionStatus
{
    Draft = 1,
    Published = 2,
    Retired = 3,
}
