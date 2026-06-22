using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Billing;

public sealed class AdvancePayment : Entity
{
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public DateOnly ReceivedDate { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public decimal RemainingBalance { get; private set; }

    private readonly List<PaymentAllocation> _allocations = new();
    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations.AsReadOnly();

    private AdvancePayment() { }

    public static AdvancePayment Create(
        Guid tenantId,
        Guid customerId,
        decimal amount,
        string paymentMethod,
        DateOnly receivedDate,
        string? referenceNumber,
        string? notes)
    {
        return new AdvancePayment
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            ReceivedDate = receivedDate,
            ReferenceNumber = referenceNumber,
            Notes = notes,
            RemainingBalance = amount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
