using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Contracts;

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Suspended = 3,
    Closed = 4,
    Cancelled = 5,
}

public sealed class Contract : Entity
{
    public string ContractNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? QuotationId { get; private set; }
    public ContractStatus Status { get; private set; }
    public int ContractTypeCode { get; private set; }
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public int DurationMonths { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public string? Notes { get; private set; }

    // ─── Vehicle totals ────────────────────────────────────────────────────
    public int TotalVehicles { get; private set; }
    public int CheckedOutVehicles { get; private set; }

    // ─── Pricing (unified via LeasingPricingEngine) ────────────────────────
    public decimal BaseAmountSar { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal DiscountAmountSar { get; private set; }
    public decimal NetAmountSar { get; private set; }
    public decimal VatPercent { get; private set; }
    public decimal VatAmountSar { get; private set; }
    public decimal TotalAmountSar { get; private set; }
    public decimal MonthlyRentSar { get; private set; }
    public decimal TotalContractValueSar { get; private set; }

    private readonly List<ContractLine> _lines = new();
    public IReadOnlyCollection<ContractLine> Lines => _lines.AsReadOnly();

    private Contract() { }

    public static Contract CreateFromQuotation(
        Guid tenantId,
        string contractNumber,
        Guid customerId,
        Guid quotationId,
        int contractTypeCode,
        decimal discountPercent,
        decimal vatPercent,
        DateTimeOffset startDate,
        int durationMonths,
        int paymentTermsDays,
        DateTimeOffset nowUtc)
    {
        return new Contract
        {
            TenantId = tenantId,
            ContractNumber = contractNumber,
            CustomerId = customerId,
            QuotationId = quotationId,
            Status = ContractStatus.Draft,
            ContractTypeCode = contractTypeCode,
            DiscountPercent = discountPercent,
            VatPercent = vatPercent,
            StartDate = startDate,
            EndDate = startDate.AddMonths(durationMonths),
            DurationMonths = durationMonths,
            PaymentTermsDays = paymentTermsDays,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void AddLine(string make, string model, int year, string description, int quantity, decimal unitPriceSar)
    {
        _lines.Add(ContractLine.Create(TenantId, Id, make, model, year, description, quantity, unitPriceSar));
        RecalculatePricing();
    }

    public void IncrementCheckout()
    {
        if (CheckedOutVehicles >= TotalVehicles)
            throw new InvalidOperationException("All vehicles in this contract are already checked out.");
        CheckedOutVehicles++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void DecrementCheckout()
    {
        if (CheckedOutVehicles <= 0) return;
        CheckedOutVehicles--;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        if (Status != ContractStatus.Draft)
            throw new InvalidOperationException($"Cannot activate contract in status {Status}.");
        Status = ContractStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Suspend(DateTimeOffset nowUtc)
    {
        if (Status != ContractStatus.Active)
            throw new InvalidOperationException($"Cannot suspend contract in status {Status}.");
        Status = ContractStatus.Suspended;
        UpdatedAtUtc = nowUtc;
    }

    public void Close(DateTimeOffset nowUtc)
    {
        if (Status != ContractStatus.Active && Status != ContractStatus.Suspended)
            throw new InvalidOperationException($"Cannot close contract in status {Status}.");
        Status = ContractStatus.Closed;
        UpdatedAtUtc = nowUtc;
    }

    private void RecalculatePricing()
    {
        TotalVehicles = _lines.Sum(l => l.Quantity);

        // Lines represent monthly amounts per vehicle category
        // BaseAmountSar = sum of all lines × duration = total base for full contract period
        var monthlyBase = _lines.Sum(l => l.LineTotalSar);
        BaseAmountSar = Math.Round(monthlyBase * DurationMonths, 2, MidpointRounding.AwayFromZero);

        // Apply shared pricing engine on the full-period base amount
        var pricing = LeasingPricingEngine.Calculate(BaseAmountSar, DiscountPercent, VatPercent);
        DiscountAmountSar = pricing.DiscountAmountSar;
        NetAmountSar = pricing.NetAmountSar;
        VatAmountSar = pricing.VatAmountSar;
        TotalAmountSar = pricing.TotalAmountSar;
        TotalContractValueSar = TotalAmountSar;

        // Monthly rent = total / duration (auto-calculated, read-only)
        MonthlyRentSar = DurationMonths > 0
            ? Math.Round(TotalAmountSar / DurationMonths, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }
}
