using AutoLeaseNet.Domain.Zatca;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Port for ZATCA submission persistence per Spec 02 §4.5.
/// Stores submission state (UBL, signature, transaction ID, status) with RLS isolation.
/// </summary>
public interface IZatcaSubmissionRepository
{
    /// <summary>Get submission by ID.</summary>
    Task<ZatcaSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get submission for an invoice (1:1 relationship).</summary>
    Task<ZatcaSubmission?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Get submission by ZATCA transaction ID.</summary>
    Task<ZatcaSubmission?> GetByTransactionIdAsync(string zatcaTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Create and persist new submission.</summary>
    Task CreateAsync(ZatcaSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>Update submission state.</summary>
    Task UpdateAsync(ZatcaSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>Delete submission (for testing/cleanup only).</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
