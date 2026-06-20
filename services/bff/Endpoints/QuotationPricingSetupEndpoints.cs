using System.Text;
using System.Text.Json;
using AutoLeaseNet.Application.Ports.Storage;
using AutoLeaseNet.Application.Ports.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Tenant-scoped setup catalog for quotation pricing screens (vehicles, insurance, maintenance, etc.).
/// Stored as a JSON document via <see cref="IObjectStorage"/> so multiple users share one setup per tenant.
/// </summary>
public static class QuotationPricingSetupEndpoints
{
    private const string Container = "quotation-pricing-setup";
    private const string BlobName = "catalog.v1.json";

    private const string EmptyPayload = """
    {
      "vehicles": [],
      "insurance": [],
      "vehicleInterest": [],
      "depreciation": [],
      "maintenance": [],
      "discountOptions": [],
      "trackingCharges": []
    }
    """;

    public static IEndpointRouteBuilder MapQuotationPricingSetupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/quotation-pricing-setup").WithTags("admin-quotation-pricing-setup");

        group.MapGet(string.Empty, GetAsync).WithName("GetQuotationPricingSetup").RequireAuthorization();
        group.MapPut(string.Empty, PutAsync).WithName("PutQuotationPricingSetup").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetAsync(
        ITenantContext tenant,
        IObjectStorage storage,
        CancellationToken ct)
    {
        var objectKey = BuildObjectKey(tenant.TenantId);

        if (!await storage.ExistsAsync(Container, objectKey, ct).ConfigureAwait(false))
        {
            return Results.Content(EmptyPayload, "application/json", Encoding.UTF8);
        }

        await using var stream = await storage.DownloadAsync(Container, objectKey, ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        return Results.Content(string.IsNullOrWhiteSpace(json) ? EmptyPayload : json, "application/json", Encoding.UTF8);
    }

    private static async Task<IResult> PutAsync(
        HttpContext httpContext,
        ITenantContext tenant,
        IObjectStorage storage,
        JsonElement body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(httpContext.Request.Headers["Idempotency-Key"].ToString()))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "PUT /admin/quotation-pricing-setup requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Results.BadRequest("Missing request body.");
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest("Request body must be a JSON object.");
        }

        var json = body.GetRawText();
        var bytes = Encoding.UTF8.GetBytes(json);

        await using var ms = new MemoryStream(bytes);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenant.TenantId.ToString(),
            ["schema"] = "quotation-pricing-setup.v1",
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        await storage
            .UploadAsync(
                Container,
                BuildObjectKey(tenant.TenantId),
                ms,
                "application/json",
                metadata,
                ct)
            .ConfigureAwait(false);

        return Results.Ok(new { success = true });
    }

    private static string BuildObjectKey(Guid tenantId) => $"{tenantId:D}/{BlobName}";
}
