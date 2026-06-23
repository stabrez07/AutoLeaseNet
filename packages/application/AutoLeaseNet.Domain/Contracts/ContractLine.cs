using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Contracts;

public sealed class ContractLine : Entity, ILineItem
{
    public Guid ContractId { get; private set; }
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPriceSar { get; private set; }
    public decimal LineTotalSar { get; private set; }

    private ContractLine() { }

    public static ContractLine Create(
        Guid tenantId, Guid contractId,
        string make, string model, int year,
        string description, int quantity, decimal unitPriceSar)
    {
        return new ContractLine
        {
            TenantId = tenantId,
            ContractId = contractId,
            Make = make,
            Model = model,
            Year = year,
            Description = description,
            Quantity = quantity,
            UnitPriceSar = unitPriceSar,
            LineTotalSar = quantity * unitPriceSar,
        };
    }
}
