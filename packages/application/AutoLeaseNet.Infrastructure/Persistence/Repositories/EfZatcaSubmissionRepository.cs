using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Zatca;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IZatcaSubmissionRepository.
/// All queries filtered by TenantId via RLS context (SQL SESSION_CONTEXT).
/// </summary>
public sealed class EfZatcaSubmissionRepository : IZatcaSubmissionRepository
{
    private readonly AutoLeaseNetDbContext _context;

    public EfZatcaSubmissionRepository(AutoLeaseNetDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Get submission by ID (RLS filtered).</summary>
    public async Task<ZatcaSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ZatcaSubmissions
            .FirstOrDefaultAsync(z => z.Id == id, cancellationToken);
    }

    /// <summary>Get submission for invoice (RLS filtered).</summary>
    public async Task<ZatcaSubmission?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return await _context.ZatcaSubmissions
            .FirstOrDefaultAsync(z => z.InvoiceId == invoiceId, cancellationToken);
    }

    /// <summary>Get submission by ZATCA transaction ID (RLS filtered).</summary>
    public async Task<ZatcaSubmission?> GetByTransactionIdAsync(string zatcaTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zatcaTransactionId);

        return await _context.ZatcaSubmissions
            .FirstOrDefaultAsync(z => z.ZatcaTransactionId == zatcaTransactionId, cancellationToken);
    }

    /// <summary>Create submission.</summary>
    public async Task CreateAsync(ZatcaSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        _context.ZatcaSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Update submission state.</summary>
    public async Task UpdateAsync(ZatcaSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        _context.ZatcaSubmissions.Update(submission);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Delete submission (testing/cleanup).</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(id, cancellationToken);
        if (submission == null)
            return;

        _context.ZatcaSubmissions.Remove(submission);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
