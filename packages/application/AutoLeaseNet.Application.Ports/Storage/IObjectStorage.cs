namespace AutoLeaseNet.Application.Ports.Storage;

/// <summary>
/// Port for object/blob storage. Per doc 04 §3.1.
/// Implementations: Adapters.Storage.AzureBlob, Adapters.Storage.InMemory.
/// </summary>
public interface IObjectStorage
{
    Task<ObjectUploadResult> UploadAsync(
        string container,
        string objectKey,
        Stream content,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct);

    Task<Stream> DownloadAsync(string container, string objectKey, CancellationToken ct);

    Task<bool> DeleteAsync(string container, string objectKey, CancellationToken ct);

    /// <summary>
    /// Returns a time-limited URL (SAS for Azure, signed URL for S3) that grants direct read access
    /// to the object. Used to serve documents/photos to authorized users without proxying through BFF.
    /// </summary>
    Task<Uri> GetSignedReadUrlAsync(
        string container,
        string objectKey,
        TimeSpan validity,
        CancellationToken ct);

    Task<bool> ExistsAsync(string container, string objectKey, CancellationToken ct);
}

public sealed record ObjectUploadResult(string ObjectKey, Uri Location, long SizeBytes, string ETag);
