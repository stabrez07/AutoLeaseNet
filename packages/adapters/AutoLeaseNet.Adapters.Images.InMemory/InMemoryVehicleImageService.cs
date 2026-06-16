using AutoLeaseNet.Application.Ports.Images;

namespace AutoLeaseNet.Adapters.Images.InMemory;

/// <summary>
/// Mock AI image service.  Returns deterministic placeholder image URLs that
/// visually represent the vehicle make/model/color using a public CDN stub.
/// In production, swap this for the real AI image generation adapter.
/// </summary>
public sealed class InMemoryVehicleImageService : IVehicleImageService
{
    // Stable placeholder base from Unsplash source API (no API key needed for demo).
    // Pattern: car+{make}+{model}+{color} resolves to a relevant stock image.
    private const string PlaceholderBase = "https://source.unsplash.com/800x500/?car";

    private static readonly Dictionary<string, string> ColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["White"]  = "white",
        ["Black"]  = "black",
        ["Silver"] = "silver",
        ["Grey"]   = "grey",
        ["Red"]    = "red",
        ["Blue"]   = "blue",
        ["Green"]  = "green",
        ["Beige"]  = "beige",
        ["Gold"]   = "gold",
    };

    public Task<VehicleImageResult> GenerateAsync(string make, string model, string? color, CancellationToken ct)
    {
        var colorSlug = color is not null && ColorMap.TryGetValue(color, out var c) ? c : "auto";
        var makeSlug = Uri.EscapeDataString(make.ToLowerInvariant());
        var modelSlug = Uri.EscapeDataString(model.ToLowerInvariant());

        var imageUrl = $"{PlaceholderBase},{makeSlug},{modelSlug},{colorSlug}";
        var thumbUrl = $"{PlaceholderBase},{makeSlug},{modelSlug},{colorSlug}&w=320&h=200";
        var alt = $"{color} {make} {model}".Trim();

        return Task.FromResult(new VehicleImageResult(imageUrl, thumbUrl, alt, IsAiGenerated: true));
    }
}
