using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Pricing;

/// <summary>
/// Formula metadata configured in administration setup.
/// Expressions are evaluated by the pricing engine and can be versioned without code changes.
/// </summary>
public sealed class PricingFormulaDefinition : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Expression { get; private set; } = string.Empty;
    public string OutputField { get; private set; } = string.Empty;
    public int Precision { get; private set; }
    public MidpointRounding RoundingMode { get; private set; }
    public bool IsActive { get; private set; }

    private PricingFormulaDefinition() { }

    public static PricingFormulaDefinition Create(
        Guid tenantId,
        string code,
        string expression,
        string outputField,
        int precision,
        MidpointRounding roundingMode,
        DateTimeOffset nowUtc,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputField);
        if (precision is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be between 0 and 6.");

        return new PricingFormulaDefinition
        {
            TenantId = tenantId,
            Code = code,
            Expression = expression,
            OutputField = outputField,
            Precision = precision,
            RoundingMode = roundingMode,
            IsActive = isActive,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        IsActive = true;
        UpdatedAtUtc = nowUtc;
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        IsActive = false;
        UpdatedAtUtc = nowUtc;
    }
}
