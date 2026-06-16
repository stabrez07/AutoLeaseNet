using AutoLeaseNet.Application.Customers;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Customers;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace AutoLeaseNet.Bff.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/customers").WithTags("customers");

        group.MapPost("/b2b", CreateB2BAsync).WithName("CreateCustomerB2B").RequireAuthorization();
        group.MapPost("/b2c", CreateB2CAsync).WithName("CreateCustomerB2C").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetCustomer").RequireAuthorization();
        group.MapPost("/{id:guid}/status", UpdateStatusAsync).WithName("UpdateCustomerStatus").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CreateB2BAsync(
        HttpContext ctx, IMediator mediator, CreateCustomerB2BRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateCustomerB2BCommand(
            LegalName: body.LegalName,
            LegalNameAr: body.LegalNameAr,
            CommercialRegistration: body.CommercialRegistration,
            VatNumber: body.VatNumber,
            Email: body.Email,
            Mobile: body.Mobile,
            NationalAddress: body.NationalAddress,
            BillingAddress: body.BillingAddress,
            CreditLimit: body.CreditLimit,
            CreditCurrency: body.CreditCurrency,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> CreateB2CAsync(
        HttpContext ctx, IMediator mediator, CreateCustomerB2CRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateCustomerB2CCommand(
            PersonNameEn: body.PersonNameEn,
            PersonNameAr: body.PersonNameAr,
            IdTypeCode: body.IdTypeCode,
            PersonIdNumber: body.PersonIdNumber,
            DateOfBirth: body.DateOfBirth,
            NationalityCode: body.NationalityCode,
            Email: body.Email,
            Mobile: body.Mobile,
            NationalAddress: body.NationalAddress,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, ICustomerRepository customers, ITenantContext tenant, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (customer is null) return Results.NotFound();
        return Results.Ok(ToDto(customer));
    }

    private static async Task<IResult> UpdateStatusAsync(
        HttpContext ctx, IMediator mediator, Guid id, UpdateCustomerStatusRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new UpdateCustomerStatusCommand(
            CustomerId: id,
            Action: body.Action,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static CustomerDetailDto ToDto(Customer c) => new(
        Id: c.Id,
        TenantId: c.TenantId,
        Type: c.Type.ToString(),
        Status: c.Status.ToString(),
        DisplayName: c.DisplayName,
        DisplayNameAr: c.DisplayNameAr,
        Email: c.Email,
        Mobile: c.Mobile,
        NationalAddress: c.NationalAddress,
        PreferredLanguage: c.PreferredLanguage.ToString(),
        LegalName: c.LegalName,
        LegalNameAr: c.LegalNameAr,
        CommercialRegistration: c.CommercialRegistration,
        VatNumber: c.VatNumber,
        BillingAddress: c.BillingAddress,
        CreditLimit: c.CreditLimit,
        CreditCurrency: c.CreditCurrency,
        PersonNameEn: c.PersonNameEn,
        PersonNameAr: c.PersonNameAr,
        IdTypeCode: c.IdTypeCode,
        PersonIdNumber: c.PersonIdNumber,
        DateOfBirth: c.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        NationalityCode: c.NationalityCode,
        KycVerified: c.KycVerified,
        KycVerifiedAtUtc: c.KycVerifiedAtUtc,
        KycVerifiedBy: c.KycVerifiedBy,
        CreatedAtUtc: c.CreatedAtUtc,
        UpdatedAtUtc: c.UpdatedAtUtc);
}

public sealed record CreateCustomerB2BRequest(
    string LegalName, string? LegalNameAr,
    string CommercialRegistration, string? VatNumber,
    string? Email, string? Mobile, string? NationalAddress, string? BillingAddress,
    decimal? CreditLimit, string? CreditCurrency);

public sealed record CreateCustomerB2CRequest(
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber,
    string? DateOfBirth,
    string? NationalityCode, string? Email, string? Mobile, string? NationalAddress);

public sealed record UpdateCustomerStatusRequest(string Action);

public sealed record CustomerDetailDto(
    Guid Id, Guid TenantId,
    string Type, string Status,
    string DisplayName, string? DisplayNameAr,
    string? Email, string? Mobile, string? NationalAddress, string PreferredLanguage,
    string? LegalName, string? LegalNameAr,
    string? CommercialRegistration, string? VatNumber, string? BillingAddress,
    decimal? CreditLimit, string? CreditCurrency,
    string? PersonNameEn, string? PersonNameAr,
    int? IdTypeCode, string? PersonIdNumber, string? DateOfBirth, string? NationalityCode,
    bool KycVerified, DateTimeOffset? KycVerifiedAtUtc, string? KycVerifiedBy,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
