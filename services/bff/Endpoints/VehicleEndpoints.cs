using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Vehicles;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace AutoLeaseNet.Bff.Endpoints;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/vehicles").WithTags("vehicles");

        group.MapPost("/", CreateAsync).WithName("CreateVehicle").RequireAuthorization();
        group.MapGet("/", GetPagedAsync).WithName("GetVehicles").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetVehicle").RequireAuthorization();
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateVehicle").RequireAuthorization();
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteVehicle").RequireAuthorization();

        group.MapGet("/{id:guid}/history", GetHistoryAsync).WithName("GetVehicleHistory").RequireAuthorization();
        group.MapGet("/{id:guid}/service-records", GetServiceRecordsAsync).WithName("GetVehicleServiceRecords").RequireAuthorization();
        group.MapPost("/{id:guid}/service-records", CreateServiceRecordAsync).WithName("CreateServiceRecord").RequireAuthorization();

        group.MapGet("/{id:guid}/images", GetImagesAsync).WithName("GetVehicleImages").RequireAuthorization();
        group.MapPost("/{id:guid}/images/generate", GenerateImageAsync).WithName("GenerateVehicleImage").RequireAuthorization();

        group.MapPost("/bulk-import", BulkImportAsync).WithName("BulkImportVehicles").RequireAuthorization()
            .Accepts<IFormFile>("multipart/form-data");

        return group;
    }

    // ── Create ─────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateAsync(
        HttpContext ctx, IMediator mediator, CreateVehicleRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateVehicleCommand(
            PlateNumber: body.PlateNumber,
            PlateLetters: body.PlateLetters,
            PlateTypeCode: body.PlateTypeCode,
            Vin: body.Vin,
            EngineNumber: body.EngineNumber,
            Make: body.Make,
            Model: body.Model,
            ModelYear: body.ModelYear,
            Color: body.Color,
            FuelType: body.FuelType,
            TransmissionType: body.TransmissionType,
            BodyType: body.BodyType,
            Seats: body.Seats,
            LicenseExpiryDate: body.LicenseExpiryDate,
            InsuranceExpiryDate: body.InsuranceExpiryDate,
            InspectionExpiryDate: body.InspectionExpiryDate,
            InsuranceCompany: body.InsuranceCompany,
            InsurancePolicyNumber: body.InsurancePolicyNumber,
            OwnerBranchId: body.OwnerBranchId,
            CurrentKm: body.CurrentKm,
            PurchasePrice: body.PurchasePrice,
            PurchaseDate: body.PurchaseDate,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ── List (paged) ────────────────────────────────────────────────────────

    private static async Task<IResult> GetPagedAsync(
        IVehicleRepository vehicles, ITenantContext tenant,
        int page, int pageSize, string? search, int? status,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 20;

        var (items, total) = await vehicles.GetPagedAsync(tenant.TenantId, page, pageSize, search, status, ct);
        return Results.Ok(new VehiclePagedDto(
            Items: items.Select(ToDto).ToList(),
            TotalCount: total,
            Page: page,
            PageSize: pageSize));
    }

    // ── Get by ID ───────────────────────────────────────────────────────────

    private static async Task<IResult> GetByIdAsync(
        Guid id, IVehicleRepository vehicles, ITenantContext tenant, CancellationToken ct)
    {
        var vehicle = await vehicles.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (vehicle is null) return Results.NotFound();
        return Results.Ok(ToDto(vehicle));
    }

    // ── Update ──────────────────────────────────────────────────────────────

    private static async Task<IResult> UpdateAsync(
        Guid id, HttpContext ctx, IMediator mediator, UpdateVehicleRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new UpdateVehicleCommand(
            VehicleId: id,
            Color: body.Color,
            Seats: body.Seats,
            Make: body.Make,
            Model: body.Model,
            ModelYear: body.ModelYear,
            InsuranceCompany: body.InsuranceCompany,
            InsurancePolicyNumber: body.InsurancePolicyNumber,
            LicenseExpiryDate: body.LicenseExpiryDate,
            InsuranceExpiryDate: body.InsuranceExpiryDate,
            InspectionExpiryDate: body.InspectionExpiryDate,
            CurrentBranchId: body.CurrentBranchId,
            CurrentKm: body.CurrentKm,
            PurchasePrice: body.PurchasePrice,
            PurchaseDate: body.PurchaseDate,
            Notes: body.Notes,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ── Delete ──────────────────────────────────────────────────────────────

    private static async Task<IResult> DeleteAsync(
        Guid id, HttpContext ctx, IMediator mediator, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var cmd = new DeleteVehicleCommand(VehicleId: id, IdempotencyKey: idempotencyKey);
        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.NoContent()
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ── History ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetHistoryAsync(
        Guid id, IVehicleHistoryRepository history, ITenantContext tenant, CancellationToken ct)
    {
        var events = await history.GetByVehicleAsync(tenant.TenantId, id, ct);
        return Results.Ok(events.Select(e => new VehicleHistoryEventDto(
            Id: e.Id,
            VehicleId: e.VehicleId,
            EventType: e.EventType.ToString(),
            Description: e.Description,
            PreviousValue: e.PreviousValue,
            NewValue: e.NewValue,
            PerformedByName: e.PerformedByName,
            OccurredAtUtc: e.CreatedAtUtc)).ToList());
    }

    // ── Service Records ─────────────────────────────────────────────────────

    private static async Task<IResult> GetServiceRecordsAsync(
        Guid id, IVehicleServiceRecordRepository serviceRecords, ITenantContext tenant, CancellationToken ct)
    {
        var records = await serviceRecords.GetByVehicleAsync(tenant.TenantId, id, ct);
        return Results.Ok(records.Select(r => new VehicleServiceRecordDto(
            Id: r.Id,
            VehicleId: r.VehicleId,
            Type: r.Type.ToString(),
            ServiceCode: r.ServiceCode,
            Description: r.Description,
            ServicedAt: r.ServicedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            OdometerAtService: r.OdometerAtService,
            CostSar: r.CostSar,
            Branch: r.Branch,
            Technician: r.Technician,
            PartsReplaced: r.PartsReplaced,
            NextServiceOdometer: r.NextServiceOdometer,
            NextServiceDate: r.NextServiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Notes: r.Notes)).ToList());
    }

    private static async Task<IResult> CreateServiceRecordAsync(
        Guid id, HttpContext ctx, IMediator mediator, CreateServiceRecordRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateServiceRecordCommand(
            VehicleId: id,
            Type: body.Type,
            ServiceCode: body.ServiceCode,
            Description: body.Description,
            ServicedAt: body.ServicedAt,
            OdometerAtService: body.OdometerAtService,
            CostSar: body.CostSar,
            Branch: body.Branch,
            Technician: body.Technician,
            PartsReplaced: body.PartsReplaced,
            NextServiceOdometer: body.NextServiceOdometer,
            NextServiceDate: body.NextServiceDate,
            Notes: body.Notes,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ── Images ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetImagesAsync(
        Guid id, IVehicleImageRepository images, ITenantContext tenant, CancellationToken ct)
    {
        var list = await images.GetByVehicleAsync(tenant.TenantId, id, ct);
        return Results.Ok(list.Select(img => new VehicleImageDto(
            Id: img.Id,
            VehicleId: img.VehicleId,
            ImageUrl: img.ImageUrl,
            ThumbnailUrl: img.ThumbnailUrl,
            AltText: img.AltText,
            IsAiGenerated: img.IsAiGenerated,
            SortOrder: img.SortOrder)).ToList());
    }

    private static async Task<IResult> GenerateImageAsync(
        Guid id, HttpContext ctx, IMediator mediator, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var cmd = new GenerateVehicleImageCommand(VehicleId: id, IdempotencyKey: idempotencyKey);
        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ── Bulk Import ─────────────────────────────────────────────────────────

    private static async Task<IResult> BulkImportAsync(
        HttpContext ctx, IMediator mediator, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest("Expected multipart/form-data with a 'file' field.");

        var form = await ctx.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return Results.BadRequest("Missing or empty 'file' field.");

        var rows = new List<BulkVehicleRow>();
        var parseErrors = new List<BulkVehicleRowError>();

        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        var allText = await reader.ReadToEndAsync(ct);
        var allLines = allText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (allLines.Length == 0)
            return Results.BadRequest("File is empty.");

        // Skip header row (index 0)
        var rowIndex = 1;
        foreach (var rawLine in allLines.Skip(1))
        {
            var line = rawLine.Trim('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 14)
            {
                parseErrors.Add(new BulkVehicleRowError(rowIndex, "PARSE_ERROR", $"Row {rowIndex}: expected 14 columns, got {cols.Length}."));
                rowIndex++;
                continue;
            }

            if (!int.TryParse(cols[2].Trim(), out var plateTypeCode) ||
                !int.TryParse(cols[6].Trim(), out var modelYear) ||
                !int.TryParse(cols[8].Trim(), out var fuelType) ||
                !int.TryParse(cols[9].Trim(), out var transmissionType) ||
                !int.TryParse(cols[10].Trim(), out var bodyType) ||
                !int.TryParse(cols[11].Trim(), out var seats) ||
                !Guid.TryParse(cols[12].Trim(), out var ownerBranchId) ||
                !int.TryParse(cols[13].Trim(), out var currentKm))
            {
                parseErrors.Add(new BulkVehicleRowError(rowIndex, "PARSE_ERROR", $"Row {rowIndex}: one or more numeric/GUID columns could not be parsed."));
                rowIndex++;
                continue;
            }

            rows.Add(new BulkVehicleRow(
                PlateNumber: cols[0].Trim(),
                PlateLetters: cols[1].Trim(),
                PlateTypeCode: plateTypeCode,
                Vin: cols[3].Trim(),
                Make: cols[4].Trim(),
                Model: cols[5].Trim(),
                ModelYear: modelYear,
                Color: cols.Length > 7 ? cols[7].Trim() : null,
                FuelType: fuelType,
                TransmissionType: transmissionType,
                BodyType: bodyType,
                Seats: seats,
                OwnerBranchId: ownerBranchId,
                CurrentKm: currentKm));

            rowIndex++;
        }

        if (rows.Count == 0 && parseErrors.Count > 0)
            return Results.UnprocessableEntity(new { parseErrors });

        var cmd = new BulkCreateVehiclesCommand(Rows: rows, IdempotencyKey: idempotencyKey);
        var result = await mediator.Send(cmd, ct);

        return Results.Ok(new
        {
            result.Success,
            result.CreatedCount,
            result.SkippedCount,
            Errors = parseErrors.Concat(result.Errors).ToList()
        });
    }

    // ── DTO mappers ─────────────────────────────────────────────────────────

    private static VehicleDetailDto ToDto(Vehicle v) => new(
        Id: v.Id,
        TenantId: v.TenantId,
        Status: v.Status.ToString(),
        PlateNumber: v.PlateNumber,
        PlateLetters: v.PlateLetters,
        PlateTypeCode: v.PlateTypeCode,
        Vin: v.Vin,
        EngineNumber: v.EngineNumber,
        Make: v.Make,
        Model: v.Model,
        ModelYear: v.ModelYear,
        Color: v.Color,
        FuelType: v.FuelType.ToString(),
        TransmissionType: v.TransmissionType.ToString(),
        BodyType: v.BodyType.ToString(),
        Seats: v.Seats,
        LicenseExpiryDate: v.LicenseExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        InsuranceExpiryDate: v.InsuranceExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        InspectionExpiryDate: v.InspectionExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        InsuranceCompany: v.InsuranceCompany,
        InsurancePolicyNumber: v.InsurancePolicyNumber,
        OwnerBranchId: v.OwnerBranchId,
        CurrentBranchId: v.CurrentBranchId,
        CurrentKm: v.CurrentKm,
        PurchasePrice: v.PurchasePrice,
        PurchaseDate: v.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        CreatedAtUtc: v.CreatedAtUtc,
        UpdatedAtUtc: v.UpdatedAtUtc);
}

// ── Request / Response DTOs ────────────────────────────────────────────────

public sealed record CreateVehicleRequest(
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string? EngineNumber,
    string Make, string Model, int ModelYear, string? Color,
    int FuelType, int TransmissionType, int BodyType, int Seats,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    Guid OwnerBranchId, int CurrentKm,
    decimal? PurchasePrice, string? PurchaseDate);

public sealed record UpdateVehicleRequest(
    string? Color, int? Seats, string? Make, string? Model, int? ModelYear,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    Guid? CurrentBranchId, int? CurrentKm,
    decimal? PurchasePrice, string? PurchaseDate,
    string? Notes);

public sealed record CreateServiceRecordRequest(
    int Type, string ServiceCode, string Description,
    string ServicedAt, int OdometerAtService, decimal CostSar,
    string Branch, string Technician,
    IEnumerable<string>? PartsReplaced,
    int? NextServiceOdometer, string? NextServiceDate,
    string? Notes);

public sealed record VehicleDetailDto(
    Guid Id, Guid TenantId, string Status,
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string? EngineNumber,
    string Make, string Model, int ModelYear, string? Color,
    string FuelType, string TransmissionType, string BodyType, int Seats,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    Guid OwnerBranchId, Guid CurrentBranchId,
    int CurrentKm, decimal? PurchasePrice, string? PurchaseDate,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record VehiclePagedDto(
    IReadOnlyList<VehicleDetailDto> Items, int TotalCount, int Page, int PageSize);

public sealed record VehicleHistoryEventDto(
    Guid Id, Guid VehicleId,
    string EventType, string Description,
    string? PreviousValue, string? NewValue,
    string PerformedByName, DateTimeOffset OccurredAtUtc);

public sealed record VehicleServiceRecordDto(
    Guid Id, Guid VehicleId,
    string Type, string ServiceCode, string Description,
    string ServicedAt, int OdometerAtService, decimal CostSar,
    string Branch, string Technician,
    IReadOnlyList<string> PartsReplaced,
    int? NextServiceOdometer, string? NextServiceDate,
    string? Notes);

public sealed record VehicleImageDto(
    Guid Id, Guid VehicleId,
    string ImageUrl, string? ThumbnailUrl, string? AltText,
    bool IsAiGenerated, int SortOrder);
