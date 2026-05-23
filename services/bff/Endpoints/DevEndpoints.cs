using System.Security.Claims;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Bff.Authentication;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Development-only endpoints. Gated by <c>app.Environment.IsDevelopment()</c> at registration
/// time so they're never exposed in staging or production.
/// </summary>
public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder routes)
    {
        var dev = routes.MapGroup("/dev").WithTags("dev");

        dev.MapGet("/whoami", (HttpContext ctx, ITenantContext tenancy) =>
        {
            var user = ctx.User;
            if (user.Identity is null || !user.Identity.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                IsAuthenticated = true,
                TenantId = user.FindFirst(DevJwtStubHandler.ClaimTenantId)?.Value,
                CustomerId = user.FindFirst(DevJwtStubHandler.ClaimCustomerId)?.Value,
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserType = user.FindFirst(DevJwtStubHandler.ClaimUserType)?.Value,
                BranchIds = user.FindAll(DevJwtStubHandler.ClaimBranchId).Select(c => c.Value).ToArray(),
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                Tenancy = new
                {
                    TenantId = tenancy.TenantId.ToString(),
                    CustomerId = tenancy.CustomerId?.ToString(),
                    UserId = tenancy.UserId?.ToString(),
                    tenancy.UserType,
                    BranchIds = tenancy.BranchIds.Select(g => g.ToString()).ToArray(),
                    tenancy.IsInternalStaff,
                    tenancy.IsSystem,
                },
            });
        })
        .RequireAuthorization()
        .WithName("DevWhoami")
        .WithSummary("Echoes the current authenticated user's tenancy claims (Development only)");

        // T5.5 / Day D — POST /dev/save-contract — issues a Tajeer SaveContract.
        // Day-D reshape: body is now domain-shaped (CustomerId / VehicleId / DriverId /
        // RentPolicyId / BranchIds + contract terms). The handler resolves aggregates and
        // builds the Tajeer V9.7 DTO internally. Requires Idempotency-Key header per CLAUDE.md §8.
        dev.MapPost("/save-contract",
            async (
                HttpContext ctx,
                IMediator mediator,
                SaveContractDevRequest body,
                CancellationToken ct) =>
            {
                var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
                if (string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    return Results.Problem(
                        title: "Missing Idempotency-Key",
                        detail: "POST /dev/save-contract requires an 'Idempotency-Key' header (any opaque string the client controls).",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (body is null)
                {
                    return Results.Problem(
                        title: "Missing request body",
                        detail: "POST /dev/save-contract requires a JSON body with domain references and contract terms.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var command = new SaveContractCommand
                {
                    IdempotencyKey = idempotencyKey,
                    CustomerId = body.CustomerId,
                    VehicleId = body.VehicleId,
                    PrimaryDriverId = body.PrimaryDriverId,
                    ExtraDriverId = body.ExtraDriverId,
                    AuthorizedDriverId = body.AuthorizedDriverId,
                    RentPolicyId = body.RentPolicyId,
                    ExtendedCoverageId = body.ExtendedCoverageId,
                    WorkingBranchId = body.WorkingBranchId,
                    ReceiveBranchId = body.ReceiveBranchId,
                    ReturnBranchId = body.ReturnBranchId,
                    ContractStartUtc = body.ContractStartUtc,
                    ContractEndUtc = body.ContractEndUtc,
                    ContractTypeCode = body.ContractTypeCode,
                    AllowedKmPerHour = body.AllowedKmPerHour,
                    AllowedKmPerDay = body.AllowedKmPerDay,
                    UnlimitedKm = body.UnlimitedKm,
                    AllowedLateHours = body.AllowedLateHours,
                    RentAmount = body.RentAmount,
                    PaidAmount = body.PaidAmount,
                    PaymentMethodCode = body.PaymentMethodCode,
                    DiscountType = body.DiscountType,
                    DiscountValue = body.DiscountValue,
                };
                var result = await mediator.Send(command, ct);

                if (!result.Success)
                {
                    var status = result.IsTransient
                        ? StatusCodes.Status503ServiceUnavailable
                        : StatusCodes.Status422UnprocessableEntity;
                    return Results.Problem(
                        title: result.ErrorCode ?? "tajeer.error",
                        detail: result.ErrorMessage,
                        statusCode: status);
                }

                return Results.Accepted(
                    uri: $"/api/v1/leases/{result.LeaseId}",
                    value: new
                    {
                        leaseId = result.LeaseId,
                        tajeerContractNumber = result.TajeerContractNumber,
                        issuanceUrl = result.IssuanceUrl,
                    });
            })
        .RequireAuthorization()
        .WithName("DevSaveContract")
        .WithSummary("Dev-only: post a domain-shaped Save Contract request (Development only)");

        return dev;
    }
}

/// <summary>
/// Domain-shaped body for <c>POST /dev/save-contract</c>. The handler resolves the
/// aggregate references and BUILDS the Tajeer V9.7 wire payload internally.
/// </summary>
public sealed record SaveContractDevRequest
{
    public required Guid CustomerId { get; init; }
    public required Guid VehicleId { get; init; }
    public required Guid PrimaryDriverId { get; init; }
    public Guid? ExtraDriverId { get; init; }
    public Guid? AuthorizedDriverId { get; init; }
    public required Guid RentPolicyId { get; init; }
    public Guid? ExtendedCoverageId { get; init; }
    public required Guid WorkingBranchId { get; init; }
    public required Guid ReceiveBranchId { get; init; }
    public required Guid ReturnBranchId { get; init; }
    public required DateTimeOffset ContractStartUtc { get; init; }
    public required DateTimeOffset ContractEndUtc { get; init; }
    public required int ContractTypeCode { get; init; }
    public int AllowedKmPerHour { get; init; }
    public int AllowedKmPerDay { get; init; }
    public bool UnlimitedKm { get; init; }
    public int AllowedLateHours { get; init; }
    public required decimal RentAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public required int PaymentMethodCode { get; init; }
    public int? DiscountType { get; init; }
    public decimal? DiscountValue { get; init; }
}
