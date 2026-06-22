using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Customers;

/// <summary>
/// Represents a document attached to a customer account (e.g. commercial registration,
/// VAT certificate, bank statement, national ID). Documents are uploaded as metadata
/// references — the actual file lives in blob storage at <see cref="FileUrl"/>.
/// </summary>
public sealed class CustomerDocument : Entity
{
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Document type: CommercialRegistration, VATCertificate, BankStatement,
    /// NationalId, AuthorizationLetter, InsuranceCert, Other.
    /// </summary>
    public string DocType { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;
    public string FileUrl { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public string? Notes { get; private set; }

    private CustomerDocument() { }

    public static CustomerDocument Create(
        Guid tenantId,
        Guid customerId,
        string docType,
        string fileName,
        string fileUrl,
        DateOnly? expiryDate,
        string? notes)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(docType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileUrl);

        var now = DateTimeOffset.UtcNow;
        return new CustomerDocument
        {
            TenantId = tenantId,
            CustomerId = customerId,
            DocType = docType,
            FileName = fileName,
            FileUrl = fileUrl,
            ExpiryDate = expiryDate,
            Notes = notes,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Verify(Guid userId, DateTimeOffset now)
    {
        VerifiedAtUtc = now;
        VerifiedByUserId = userId;
        UpdatedAtUtc = now;
    }
}
