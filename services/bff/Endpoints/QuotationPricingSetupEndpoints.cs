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
    private const string BlobName = "catalog.v2.json";
    public const int CurrentSchemaVersion = 2;

    private static readonly string[] RequiredArrayProperties =
    [
        "vehicles",
        "insurance",
        "vehicleInterest",
        "depreciation",
        "maintenance",
        "discountOptions",
        "trackingCharges",
        "leaseTerms",
        "interestRateTable",
        "residualValueTable",
        "replacementPolicy",
        "feeMaster",
        "commissionRateTable",
        "profitMarginSetup",
        "calendarPeriods",
    ];

    private const string EmptyPayload = """
    {
      "schemaVersion": 2,
      "vehicles": [],
      "insurance": [],
      "vehicleInterest": [],
      "depreciation": [],
      "maintenance": [],
      "discountOptions": [],
      "trackingCharges": [],
      "leaseTerms": [],
      "interestRateTable": [],
      "residualValueTable": [],
      "replacementPolicy": [],
      "feeMaster": [],
      "commissionRateTable": [],
      "profitMarginSetup": [],
      "calendarPeriods": []
    }
    """;

    public static IEndpointRouteBuilder MapQuotationPricingSetupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/quotation-pricing-setup").WithTags("admin-quotation-pricing-setup");

        group.MapGet(string.Empty, GetAsync).WithName("GetQuotationPricingSetup").RequireAuthorization();
        group.MapPut(string.Empty, PutAsync).WithName("PutQuotationPricingSetup").RequireAuthorization();

        return routes;
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
            return Results.Problem(
                title: "Missing request body",
                detail: "Request body must be a JSON object.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return Results.Problem(
                title: "Invalid request body",
                detail: "Request body must be a JSON object.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validationErrors = ValidateSetupPayload(body);
        if (validationErrors.Count > 0)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: string.Join("; ", validationErrors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var userIdentity = httpContext.User.Identity?.Name ?? "unknown";

        var envelope = new Dictionary<string, object>();

        foreach (var prop in body.EnumerateObject())
        {
            if (prop.Name == "schemaVersion" || prop.Name == "updatedBy" || prop.Name == "updatedAt")
                continue;
            envelope[prop.Name] = prop.Value;
        }

        envelope["schemaVersion"] = CurrentSchemaVersion;
        envelope["updatedBy"] = userIdentity;
        envelope["updatedAt"] = now.ToString("O");

        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);

        await using var ms = new MemoryStream(bytes);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenant.TenantId.ToString(),
            ["schema"] = $"quotation-pricing-setup.v{CurrentSchemaVersion}",
            ["updatedUtc"] = now.ToString("O"),
            ["updatedBy"] = userIdentity,
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

        return Results.Ok(new { success = true, schemaVersion = CurrentSchemaVersion, updatedAt = now });
    }

    public static List<string> ValidateSetupPayload(JsonElement body)
    {
        var errors = new List<string>();

        foreach (var required in RequiredArrayProperties)
        {
            if (!body.TryGetProperty(required, out var prop))
            {
                errors.Add($"Missing required property '{required}'.");
            }
            else if (prop.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"Property '{required}' must be an array.");
            }
        }

        if (body.TryGetProperty("schemaVersion", out var versionProp))
        {
            if (versionProp.ValueKind == JsonValueKind.Number)
            {
                var version = versionProp.GetInt32();
                if (version > CurrentSchemaVersion)
                {
                    errors.Add($"schemaVersion {version} is not supported; current is {CurrentSchemaVersion}.");
                }
            }
        }

        return errors;
    }

    private static string BuildObjectKey(Guid tenantId) => $"{tenantId:D}/{BlobName}";
}
