using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Vehicles;

/// <summary>
/// An image associated with a vehicle.  Can be uploaded by staff or AI-generated.
/// ImageUrl points to Azure Blob (prod) or a placeholder CDN (dev/mock).
/// </summary>
public sealed class VehicleImage : Entity
{
    public Guid VehicleId { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? ThumbnailUrl { get; private set; }
    public string? AltText { get; private set; }
    public bool IsAiGenerated { get; private set; }
    public int SortOrder { get; private set; }

    private VehicleImage() { }

    public static VehicleImage Create(
        Guid tenantId,
        Guid vehicleId,
        string imageUrl,
        DateTimeOffset nowUtc,
        string? thumbnailUrl = null,
        string? altText = null,
        bool isAiGenerated = false,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);
        return new VehicleImage
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            AltText = altText,
            IsAiGenerated = isAiGenerated,
            SortOrder = sortOrder,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
