using AutoLeaseNet.Application.Me;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Endpoints scoped to the **current authenticated user** under <c>/api/v1/me/*</c>.
/// Customer Portal consumes these; web-portal (internal staff) typically uses the
/// equivalent lookups. RLS does the CustomerId scoping at the DB layer; no
/// app-side filter is layered on top.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/me").WithTags("me").RequireAuthorization();

        group.MapGet("/leases", async (IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var result = await mediator.Send(new GetMyLeasesQuery(), ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // Missing customer context → 400. Lookup-style queries throw this
                // when the principal is incomplete (e.g. INTERNAL_STAFF on a /me route).
                return Results.Problem(
                    title: "me.requires_customer_context",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("GetMyLeases")
        .WithSummary("Leases visible to the current authenticated customer (RLS-scoped).");

        group.MapGet("/vehicles", async (IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var result = await mediator.Send(new GetMyVehiclesQuery(), ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "me.requires_customer_context",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("GetMyVehicles")
        .WithSummary("Vehicles the current authenticated customer currently has (Active/Extended/Suspended leases).");

        return group;
    }
}
