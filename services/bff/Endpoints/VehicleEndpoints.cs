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
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetVehicle").RequireAuthorization();

        return group;
    }

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

    private static async Task<IResult> GetByIdAsync(
        Guid id, IVehicleRepository vehicles, ITenantContext tenant, CancellationToken ct)
    {
        var vehicle = await vehicles.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (vehicle is null) return Results.NotFound();
        return Results.Ok(ToDto(vehicle));
    }

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

public sealed record CreateVehicleRequest(
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string? EngineNumber,
    string Make, string Model, int ModelYear, string? Color,
    int FuelType, int TransmissionType, int BodyType, int Seats,
    string? LicenseExpiryDate, string? InsuranceExpiryDate, string? InspectionExpiryDate,
    string? InsuranceCompany, string? InsurancePolicyNumber,
    Guid OwnerBranchId, int CurrentKm,
    decimal? PurchasePrice, string? PurchaseDate);

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
