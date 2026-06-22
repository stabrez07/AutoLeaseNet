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
    public int TotalVehicles { get; private set; }
    public decimal MonthlyRentSar { get; private set; }
    public decimal TotalContractValueSar { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<ContractLine> _lines = new();
    public IReadOnlyCollection<ContractLine> Lines => _lines.AsReadOnly();

    private Contract() { }

    public static Contract CreateFromQuotation(
        Guid tenantId,
        string contractNumber,
        Guid customerId,
        Guid quotationId,
        int contractTypeCode,
        DateTimeOffset startDate,
        int durationMonths,
        int paymentTermsDays,
        DateTimeOffset nowUtc)
    {
        var endDate = startDate.AddMonths(durationMonths);
        var contract = new Contract
        {
            TenantId = tenantId,
            ContractNumber = contractNumber,
            CustomerId = customerId,
            QuotationId = quotationId,
            Status = ContractStatus.Draft,
            ContractTypeCode = contractTypeCode,
            StartDate = startDate,
            EndDate = endDate,
            DurationMonths = durationMonths,
            PaymentTermsDays = paymentTermsDays,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
        return contract;
    }

    public void AddLine(string make, string model, int year, string description, int quantity, decimal unitPriceSar)
    {
        var line = ContractLine.Create(TenantId, Id, make, model, year, description, quantity, unitPriceSar);
        _lines.Add(line);
        RecalculateTotals();
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

    private void RecalculateTotals()
    {
        TotalVehicles = _lines.Sum(l => l.Quantity);
        MonthlyRentSar = _lines.Sum(l => l.LineTotalSar);
        TotalContractValueSar = MonthlyRentSar * DurationMonths;
    }
}
