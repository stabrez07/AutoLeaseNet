using AutoLeaseNet.Application.Lookups;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Day F — read-only lookup endpoints under <c>/api/v1/lookups/*</c>. Every endpoint is
/// tenant-scoped via <c>ITenantContext</c> (TenancyMiddleware sets it from the JWT/dev
/// stub) and requires authentication. Paged endpoints accept <c>page</c>, <c>pageSize</c>
/// (max 200), and an optional <c>search</c> filter (case-insensitive name/code contains).
/// </summary>
public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder routes)
    {
        var lookups = routes.MapGroup("/lookups").WithTags("lookups").RequireAuthorization();

        lookups.MapGet("/branches", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetBranchesQuery(), ct)))
            .WithName("GetBranches")
            .WithSummary("Active branches for the current tenant.");

        lookups.MapGet("/rent-policies", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetRentPoliciesQuery(), ct)))
            .WithName("GetRentPolicies")
            .WithSummary("Active rent policies for the current tenant.");

        lookups.MapGet("/extended-coverages", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetExtendedCoveragesQuery(), ct)))
            .WithName("GetExtendedCoverages")
            .WithSummary("Active extended coverages for the current tenant.");

        lookups.MapGet("/customers",
            async (IMediator mediator, int? page, int? pageSize, string? search, CancellationToken ct) =>
            {
                var result = await mediator.Send(
                    new GetCustomersPagedQuery(page ?? 1, pageSize ?? 50, search),
                    ct);
                return Results.Ok(result);
            })
            .WithName("GetCustomers")
            .WithSummary("Paged customers (B2B + B2C) for the current tenant.");

        lookups.MapGet("/vehicles",
            async (IMediator mediator, int? page, int? pageSize, string? search, int? status, CancellationToken ct) =>
            {
                var result = await mediator.Send(
                    new GetVehiclesPagedQuery(page ?? 1, pageSize ?? 50, search, status),
                    ct);
                return Results.Ok(result);
            })
            .WithName("GetVehicles")
            .WithSummary("Paged vehicles for the current tenant. Filter by status (1=Available, 2=Reserved, ...).");

        lookups.MapGet("/drivers",
            async (IMediator mediator, int? page, int? pageSize, string? search, CancellationToken ct) =>
            {
                var result = await mediator.Send(
                    new GetDriversPagedQuery(page ?? 1, pageSize ?? 50, search),
                    ct);
                return Results.Ok(result);
            })
            .WithName("GetDrivers")
            .WithSummary("Paged drivers for the current tenant.");

        return lookups;
    }
}
