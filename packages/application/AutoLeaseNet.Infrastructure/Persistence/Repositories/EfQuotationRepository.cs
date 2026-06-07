using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfQuotationRepository(AutoLeaseNetDbContext db) : IQuotationRepository
{
    public void Add(Quotation quotation)
    {
        ArgumentNullException.ThrowIfNull(quotation);
        db.Quotations.Add(quotation);
    }

    public Task<Quotation?> GetByIdAsync(Guid tenantId, Guid quotationId, CancellationToken ct)
    {
        return db.Quotations
            .Include(q => q.Lines)
            .Include(q => q.Approvals)
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quotationId, ct);
    }

    public Task<Quotation?> GetByQuoteNumberAsync(Guid tenantId, string quoteNumber, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteNumber);

        return db.Quotations
            .Include(q => q.Lines)
            .Include(q => q.Approvals)
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.QuoteNumber == quoteNumber, ct);
    }

    public async Task<IReadOnlyList<Quotation>> GetPendingApprovalsForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await db.Quotations
            .Where(q => q.TenantId == tenantId && q.Status == QuotationStatus.PendingApproval)
            .Include(q => q.Approvals)
            .Include(q => q.Lines)
            .OrderBy(q => q.SubmittedAtUtc)
            .ThenBy(q => q.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
