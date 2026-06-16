namespace AutoLeaseNet.Application.Ports.Images;

public sealed record VehicleImageResult(
    string ImageUrl,
    string? ThumbnailUrl,
    string AltText,
    bool IsAiGenerated);

public interface IVehicleImageService
{
    /// <summary>
    /// Generate an image for the vehicle based on make, model, and color.
    /// In mock/dev mode returns a deterministic placeholder URL.
    /// In production this calls an AI image generation API.
    /// </summary>
    Task<VehicleImageResult> GenerateAsync(string make, string model, string? color, CancellationToken ct);
}
