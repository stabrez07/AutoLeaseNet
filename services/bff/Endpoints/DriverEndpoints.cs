using AutoLeaseNet.Application.Drivers;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Drivers;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace AutoLeaseNet.Bff.Endpoints;

public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/drivers").WithTags("drivers");

        group.MapPost("/", CreateAsync).WithName("CreateDriver").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetDriver").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext ctx, IMediator mediator, CreateDriverRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateDriverCommand(
            PersonNameEn: body.PersonNameEn,
            PersonNameAr: body.PersonNameAr,
            IdTypeCode: body.IdTypeCode,
            PersonIdNumber: body.PersonIdNumber,
            DateOfBirth: body.DateOfBirth,
            NationalityCode: body.NationalityCode,
            DriverLicenseNumber: body.DriverLicenseNumber,
            LicenseClass: body.LicenseClass,
            LicenseExpiryDate: body.LicenseExpiryDate,
            Mobile: body.Mobile,
            Email: body.Email,
            NationalAddress: body.NationalAddress,
            CustomerId: body.CustomerId,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, IDriverRepository drivers, ITenantContext tenant, CancellationToken ct)
    {
        var driver = await drivers.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (driver is null) return Results.NotFound();
        return Results.Ok(ToDto(driver));
    }

    private static DriverDetailDto ToDto(Driver d) => new(
        Id: d.Id,
        TenantId: d.TenantId,
        Status: d.Status.ToString(),
        CustomerId: d.CustomerId,
        PersonNameEn: d.PersonNameEn,
        PersonNameAr: d.PersonNameAr,
        IdTypeCode: d.IdTypeCode,
        PersonIdNumber: d.PersonIdNumber,
        DateOfBirth: d.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        NationalityCode: d.NationalityCode,
        DriverLicenseNumber: d.DriverLicenseNumber,
        LicenseClass: d.LicenseClass,
        LicenseExpiryDate: d.LicenseExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Mobile: d.Mobile,
        Email: d.Email,
        NationalAddress: d.NationalAddress,
        TammAuthorizationStatus: d.TammAuthorizationStatus.ToString(),
        CreatedAtUtc: d.CreatedAtUtc,
        UpdatedAtUtc: d.UpdatedAtUtc);
}

public sealed record CreateDriverRequest(
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber,
    string? DateOfBirth,
    string? NationalityCode, string DriverLicenseNumber, int LicenseClass,
    string LicenseExpiryDate,
    string? Mobile, string? Email, string? NationalAddress,
    Guid? CustomerId);

public sealed record DriverDetailDto(
    Guid Id, Guid TenantId, string Status,
    Guid? CustomerId,
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber, string? DateOfBirth, string? NationalityCode,
    string DriverLicenseNumber, int LicenseClass, string LicenseExpiryDate,
    string? Mobile, string? Email, string? NationalAddress,
    string TammAuthorizationStatus,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
