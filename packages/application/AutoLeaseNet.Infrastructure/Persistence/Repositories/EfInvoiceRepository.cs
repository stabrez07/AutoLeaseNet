using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IInvoiceRepository. RLS-filtered via SESSION_CONTEXT('tenant_id').
/// Phase 1: single-line invoicing per lease. Phase 2: multi-line + credit memo support.
/// </summary>
internal sealed class EfInvoiceRepository(AutoLeaseNetDbContext dbContext) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        return await dbContext.Invoices
            .Where(i => i.TenantId == tenantId && i.Id == invoiceId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Invoice?> GetByLeaseIdAsync(Guid tenantId, Guid leaseId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        return await dbContext.Invoices
            .Where(i => i.TenantId == tenantId && i.LeaseId == leaseId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Invoice?> GetByNumberAsync(Guid tenantId, string invoiceNumber, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        return await dbContext.Invoices
            .Where(i => i.TenantId == tenantId && i.InvoiceNumber == invoiceNumber)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Invoice> CreateAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return invoice;
    }

    public async Task<Invoice> UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        dbContext.Invoices.Update(invoice);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return invoice;
    }

    public async Task DeleteAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        var invoice = await GetByIdAsync(tenantId, invoiceId, ct).ConfigureAwait(false);
        if (invoice != null)
        {
            dbContext.Invoices.Remove(invoice);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<string> GetNextInvoiceNumberAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        // Phase 1: simple in-memory counter (format: INV-YYYY-NNNN)
        // Phase 2: use SQL Server sequence or distributed counter
        var year = DateTime.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var count = await dbContext.Invoices
            .Where(i => i.TenantId == tenantId && i.InvoiceNumber.Contains(year))
            .CountAsync(ct)
            .ConfigureAwait(false);

        return $"INV-{year}-{(count + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
