using AutoLeaseNet.Domain.Billing;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Repository port for Invoice aggregate (Spec 01 §2.3 Hexagonal Pattern A).
/// Every method filters by TenantId via RLS (Spec 01 §3).
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>Fetch invoice by ID (RLS-filtered).</summary>
    Task<Invoice?> GetByIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>Fetch invoice by lease (RLS-filtered). Returns null if no invoice exists for lease yet.</summary>
    Task<Invoice?> GetByLeaseIdAsync(Guid tenantId, Guid leaseId, CancellationToken ct = default);

    /// <summary>Fetch invoice by invoice number (RLS-filtered).</summary>
    Task<Invoice?> GetByNumberAsync(Guid tenantId, string invoiceNumber, CancellationToken ct = default);

    /// <summary>Create new invoice (auto-generates ID).</summary>
    Task<Invoice> CreateAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Update existing invoice.</summary>
    Task<Invoice> UpdateAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Delete invoice (soft-delete via status = Voided in real impl).</summary>
    Task DeleteAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>Get next sequential invoice number for tenant (e.g., "INV-2026-0001").</summary>
    Task<string> GetNextInvoiceNumberAsync(Guid tenantId, CancellationToken ct = default);
}
