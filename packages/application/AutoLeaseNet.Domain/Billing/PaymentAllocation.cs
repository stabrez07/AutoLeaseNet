using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Billing;

public sealed class PaymentAllocation : Entity
{
    public Guid AdvancePaymentId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public decimal AllocatedAmountSar { get; private set; }
    public DateTimeOffset AllocatedAtUtc { get; private set; }

    private PaymentAllocation() { }

    public static PaymentAllocation Create(
        Guid tenantId,
        Guid advancePaymentId,
        Guid invoiceId,
        string invoiceNumber,
        decimal amount)
    {
        return new PaymentAllocation
        {
            TenantId = tenantId,
            AdvancePaymentId = advancePaymentId,
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            AllocatedAmountSar = amount,
            AllocatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
