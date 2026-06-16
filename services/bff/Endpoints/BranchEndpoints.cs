using AutoLeaseNet.Application.Branches;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Branches;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

public static class BranchEndpoints
{
    public static IEndpointRouteBuilder MapBranchEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/branches").WithTags("branches");

        group.MapPost("/", CreateAsync).WithName("CreateBranch").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetBranch").RequireAuthorization();
        group.MapPost("/{id:guid}/status", UpdateStatusAsync).WithName("UpdateBranchStatus").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext ctx, IMediator mediator, CreateBranchRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateBranchCommand(
            Code: body.Code,
            NameEn: body.NameEn,
            NameAr: body.NameAr,
            CityEn: body.CityEn,
            CityAr: body.CityAr,
            RegionEn: body.RegionEn,
            RegionAr: body.RegionAr,
            Address: body.Address,
            PhoneNumber: body.PhoneNumber,
            LicenseNumber: body.LicenseNumber,
            Latitude: body.Latitude,
            Longitude: body.Longitude,
            TajeerBranchId: body.TajeerBranchId,
            TajeerOperatorId: body.TajeerOperatorId,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, IBranchRepository branches, ITenantContext tenant, CancellationToken ct)
    {
        var branch = await branches.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (branch is null) return Results.NotFound();
        return Results.Ok(ToDto(branch));
    }

    private static async Task<IResult> UpdateStatusAsync(
        HttpContext ctx, IMediator mediator, Guid id, UpdateBranchStatusRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new UpdateBranchStatusCommand(
            BranchId: id,
            Activate: body.Activate,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static BranchDetailDto ToDto(Branch b) => new(
        Id: b.Id,
        TenantId: b.TenantId,
        Code: b.Code,
        NameEn: b.NameEn,
        NameAr: b.NameAr,
        CityEn: b.CityEn,
        CityAr: b.CityAr,
        RegionEn: b.RegionEn,
        RegionAr: b.RegionAr,
        LicenseNumber: b.LicenseNumber,
        Address: b.Address,
        PhoneNumber: b.PhoneNumber,
        Latitude: b.Latitude,
        Longitude: b.Longitude,
        TajeerBranchId: b.TajeerBranchId,
        TajeerOperatorId: b.TajeerOperatorId,
        IsActive: b.IsActive,
        CreatedAtUtc: b.CreatedAtUtc,
        UpdatedAtUtc: b.UpdatedAtUtc);
}

public sealed record CreateBranchRequest(
    string Code, string NameEn, string NameAr,
    string? CityEn, string? CityAr, string? RegionEn, string? RegionAr,
    string? Address, string? PhoneNumber, string? LicenseNumber,
    decimal? Latitude, decimal? Longitude,
    int TajeerBranchId, long TajeerOperatorId);

public sealed record UpdateBranchStatusRequest(bool Activate);

public sealed record BranchDetailDto(
    Guid Id, Guid TenantId,
    string Code, string NameEn, string NameAr,
    string? CityEn, string? CityAr, string? RegionEn, string? RegionAr,
    string? LicenseNumber, string? Address, string? PhoneNumber,
    decimal? Latitude, decimal? Longitude,
    int TajeerBranchId, long TajeerOperatorId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
