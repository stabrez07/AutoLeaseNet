using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

public sealed class RfqAttachment : Entity
{
    public Guid RfqId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileUrl { get; private set; } = string.Empty;
    public string? FileType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private RfqAttachment() { }

    internal static RfqAttachment Create(
        Guid tenantId, Guid rfqId, string fileName, string fileUrl,
        string? fileType, long? fileSizeBytes, Guid uploadedBy)
    {
        return new RfqAttachment
        {
            TenantId = tenantId,
            RfqId = rfqId,
            FileName = fileName,
            FileUrl = fileUrl,
            FileType = fileType,
            FileSizeBytes = fileSizeBytes,
            UploadedByUserId = uploadedBy,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
