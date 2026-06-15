using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Billing;

/// <summary>
/// Invoice aggregate root per Spec 02 §4.4. Auto-created when lease issued; tracks ZATCA clearance status.
/// Phase 1: single-line invoice (monthly base rent). Phase 2: multi-line (insurance, extensions, adjustments).
/// </summary>
public sealed class Invoice : Entity
{
    /// <summary>Auto-generated tenant-scoped sequential number (e.g., "INV-2026-0001").</summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>The lease this invoice is for.</summary>
    public Guid LeaseId { get; private set; }

    /// <summary>Customer ID from the lease.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Current invoice state per Spec 02 §4.4.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>Invoice issue date (typically when lease issues).</summary>
    public DateOnly IssueDateUtc { get; private set; }

    /// <summary>Invoice due date (30 days from issue by default; Phase 2: configurable).</summary>
    public DateOnly DueDateUtc { get; private set; }

    /// <summary>Phase 1: single monthly rental charge (SAR). Phase 2: sum of line items.</summary>
    public decimal BaseAmountSar { get; private set; }

    /// <summary>VAT amount (15% KSA standard rate).</summary>
    public decimal VatSar { get; private set; }

    /// <summary>Total invoice amount (base + VAT).</summary>
    public decimal TotalSar { get; private set; }

    /// <summary>ZATCA UBL XML (populated by Day-26 UBL builder); null until submitted.</summary>
    public string? UblXml { get; private set; }

    /// <summary>ZATCA invoice hash (SHA-256 of canonical XML); set on clearance.</summary>
    public string? ZatcaInvoiceHash { get; private set; }

    /// <summary>ZATCA clearance timestamp; set when status = Cleared.</summary>
    public DateTimeOffset? ClearedAtUtc { get; private set; }

    /// <summary>Last submission error message (if SubmissionFailed or ClearanceFailed).</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>Submission attempt count.</summary>
    public int SubmissionAttempts { get; private set; }

    private Invoice() { }

    /// <summary>Factory: create draft invoice from lease. Phase 1 single-line version.</summary>
    public static Invoice CreateFromLease(
        Guid tenantId,
        Guid leaseId,
        Guid customerId,
        string invoiceNumber,
        decimal baseAmountSar,
        DateOnly issueDateUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        if (baseAmountSar < 0)
            throw new ArgumentOutOfRangeException(nameof(baseAmountSar), "Base amount cannot be negative.");

        const decimal vatRate = 0.15m;
        var vat = Math.Round(baseAmountSar * vatRate, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(baseAmountSar + vat, 2, MidpointRounding.AwayFromZero);

        return new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            LeaseId = leaseId,
            CustomerId = customerId,
            Status = InvoiceStatus.Draft,
            IssueDateUtc = issueDateUtc,
            DueDateUtc = issueDateUtc.AddDays(30),
            BaseAmountSar = baseAmountSar,
            VatSar = vat,
            TotalSar = total,
            SubmissionAttempts = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Record submission to ZATCA (async operation; idempotent).</summary>
    public void MarkSubmitted(DateTimeOffset nowUtc)
    {
        if (Status != InvoiceStatus.Draft && Status != InvoiceStatus.SubmissionFailed)
            throw new InvalidOperationException($"Cannot submit invoice with status {Status}.");

        Status = InvoiceStatus.Submitted;
        SubmissionAttempts++;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Record ZATCA clearance success.</summary>
    public void MarkCleared(string invoiceHash, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceHash);
        if (Status != InvoiceStatus.Submitted)
            throw new InvalidOperationException($"Cannot clear invoice with status {Status}.");

        Status = InvoiceStatus.Cleared;
        ZatcaInvoiceHash = invoiceHash;
        ClearedAtUtc = nowUtc;
        LastErrorMessage = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Record submission or clearance failure.</summary>
    public void MarkFailed(bool isSubmissionPhase, string errorMessage, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        Status = isSubmissionPhase ? InvoiceStatus.SubmissionFailed : InvoiceStatus.ClearanceFailed;
        LastErrorMessage = errorMessage;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Mark invoice as finalized (no further changes allowed).</summary>
    public void MarkFinalized(DateTimeOffset nowUtc)
    {
        if (Status != InvoiceStatus.Cleared)
            throw new InvalidOperationException($"Can only finalize cleared invoices; current status: {Status}.");

        Status = InvoiceStatus.Finalized;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Void invoice (credit memo or lease cancellation scenario).</summary>
    public void Void(DateTimeOffset nowUtc)
    {
        if (Status == InvoiceStatus.Finalized)
            throw new InvalidOperationException("Cannot void a finalized invoice.");

        Status = InvoiceStatus.Voided;
        UpdatedAtUtc = nowUtc;
    }
}
