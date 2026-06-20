using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Pricing;

/// <summary>
/// Administration setup policy for allowed quote discount presets and maximum override.
/// </summary>
public sealed class PricingDiscountPolicy : Entity
{
    private readonly List<decimal> _allowedPresets = new();

    public decimal MaxDiscountPercent { get; private set; }
    public IReadOnlyCollection<decimal> AllowedPresets => _allowedPresets.AsReadOnly();

    private PricingDiscountPolicy() { }

    public static PricingDiscountPolicy Create(
        Guid tenantId,
        decimal maxDiscountPercent,
        IEnumerable<decimal> allowedPresets,
        DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (maxDiscountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maxDiscountPercent), maxDiscountPercent, "MaxDiscountPercent must be 0-100.");
        ArgumentNullException.ThrowIfNull(allowedPresets);

        var normalized = allowedPresets
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (normalized.Count == 0)
            throw new ArgumentException("At least one preset is required.", nameof(allowedPresets));
        if (normalized.Any(x => x is < 0 or > 100))
            throw new ArgumentOutOfRangeException(nameof(allowedPresets), "All presets must be within 0-100.");
        if (normalized.Any(x => x > maxDiscountPercent))
            throw new ArgumentException("Preset discount cannot exceed MaxDiscountPercent.", nameof(allowedPresets));

        var policy = new PricingDiscountPolicy
        {
            TenantId = tenantId,
            MaxDiscountPercent = maxDiscountPercent,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        policy._allowedPresets.AddRange(normalized);
        return policy;
    }

    public static PricingDiscountPolicy CreateDefault(Guid tenantId, DateTimeOffset nowUtc)
    {
        return Create(tenantId, maxDiscountPercent: 20m, allowedPresets: [10m, 20m], nowUtc);
    }

    public void SetMaxDiscountPercent(decimal maxDiscountPercent, DateTimeOffset nowUtc)
    {
        if (maxDiscountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maxDiscountPercent), maxDiscountPercent, "MaxDiscountPercent must be 0-100.");
        if (_allowedPresets.Any(x => x > maxDiscountPercent))
            throw new InvalidOperationException("Reduce or remove presets before lowering max discount.");

        MaxDiscountPercent = maxDiscountPercent;
        UpdatedAtUtc = nowUtc;
    }

    public void SetAllowedPresets(IEnumerable<decimal> allowedPresets, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(allowedPresets);

        var normalized = allowedPresets
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (normalized.Count == 0)
            throw new ArgumentException("At least one preset is required.", nameof(allowedPresets));
        if (normalized.Any(x => x is < 0 or > 100))
            throw new ArgumentOutOfRangeException(nameof(allowedPresets), "All presets must be within 0-100.");
        if (normalized.Any(x => x > MaxDiscountPercent))
            throw new ArgumentException("Preset discount cannot exceed MaxDiscountPercent.", nameof(allowedPresets));

        _allowedPresets.Clear();
        _allowedPresets.AddRange(normalized);
        UpdatedAtUtc = nowUtc;
    }

    public bool IsPresetAllowed(decimal discountPercent)
        => _allowedPresets.Contains(discountPercent);
}
