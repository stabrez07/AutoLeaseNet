using System.Security.Claims;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
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
                // T2.2 — also echo the resolved ITenantContext to prove DI wiring works.
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

        // T5.5 — POST /dev/save-contract — issues a Tajeer SaveContract using the supplied
        // V9.7 request body. Requires Idempotency-Key header per CLAUDE.md §8.
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

                if (body?.Request is null)
                {
                    return Results.Problem(
                        title: "Missing request body",
                        detail: "Body must contain a 'request' field with the Tajeer V9.7 SaveContract payload.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var command = new SaveContractCommand(idempotencyKey, body.CustomerId, body.Request);
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
        .WithSummary("Dev-only: POST a Tajeer V9.7 SaveContract payload and persist a Lease (Development only)");

        return dev;
    }
}

/// <summary>
/// Body shape for <c>POST /dev/save-contract</c>. <see cref="Request"/> is the verbatim
/// Tajeer V9.7 payload (so clients can paste a known-good staging body without translation);
/// <see cref="CustomerId"/> is an optional B2B association.
/// </summary>
public sealed record SaveContractDevRequest(
    Guid? CustomerId,
    SaveContractRequest Request);

