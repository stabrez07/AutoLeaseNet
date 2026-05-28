using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// One photo attached to an <see cref="Inspection"/> per Spec 01 §5.6. Photos are
/// uploaded out-of-band to blob storage; this entity carries only the URI (the upload
/// flow through <c>Adapters.Storage</c> lands in a later workstream). Sequence drives
/// the gallery display order; AiDamageDetectionJson is reserved for Phase 3.
/// </summary>
public sealed class InspectionPhoto : Entity
{
    public Guid InspectionId { get; private set; }
    public string BlobUri { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public string? AiDamageDetectionJson { get; private set; }

    private InspectionPhoto() { }

    internal static InspectionPhoto Create(Guid inspectionId, Guid tenantId, string blobUri, int sequence, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        return new InspectionPhoto
        {
            InspectionId = inspectionId,
            TenantId = tenantId,
            BlobUri = blobUri,
            Sequence = sequence,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
