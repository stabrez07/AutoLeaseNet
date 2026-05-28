using AutoLeaseNet.Application.Operations;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Incident (Spec 01 §5.6) endpoints. State-changing POSTs/PATCH require an
/// <c>Idempotency-Key</c> header (CLAUDE.md §8); reads use the standard dev JWT
/// stub for tenant resolution.
/// </summary>
public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/incidents").WithTags("incidents");

        group.MapPost("/", ReportAsync).WithName("ReportIncident").RequireAuthorization();
        group.MapPost("/{id:guid}/investigate", InvestigateAsync).WithName("StartIncidentInvestigation").RequireAuthorization();
        group.MapPost("/{id:guid}/resolve", ResolveAsync).WithName("ResolveIncident").RequireAuthorization();
        group.MapPost("/{id:guid}/close", CloseAsync).WithName("CloseIncident").RequireAuthorization();
        group.MapPatch("/{id:guid}/claim", UpdateClaimAsync).WithName("UpdateIncidentClaim").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetIncidentById").RequireAuthorization();

        var lookups = routes.MapGroup("/lookups/incidents").WithTags("lookups");
        lookups.MapGet("/", SearchAsync).WithName("ListIncidents").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ReportAsync(HttpContext ctx, IMediator mediator, ReportIncidentRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();

        var command = new ReportIncidentCommand
        {
            IdempotencyKey = idemKey,
            VehicleId = body.VehicleId,
            LeaseId = body.LeaseId,
            ReportedByPersonId = body.ReportedByPersonId,
            Type = body.Type,
            Severity = body.Severity,
            IncidentTimeUtc = body.IncidentTimeUtc,
            Description = body.Description,
            LocationLat = body.LocationLat,
            LocationLng = body.LocationLng,
            LocationDescription = body.LocationDescription,
            PoliceReportNumber = body.PoliceReportNumber,
            InsuranceClaimNumber = body.InsuranceClaimNumber,
        };
        var result = await mediator.Send(command, ct);
        return result.Success
            ? Results.Created($"/api/v1/incidents/{result.IncidentId}", new { id = result.IncidentId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> InvestigateAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new StartIncidentInvestigationCommand(idemKey, id), ct);
        return result.Success ? Results.Ok(new { id = result.IncidentId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> ResolveAsync(HttpContext ctx, IMediator mediator, Guid id, ResolveIncidentRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new ResolveIncidentCommand(idemKey, id, body.ResolutionNotes), ct);
        return result.Success ? Results.Ok(new { id = result.IncidentId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> CloseAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new CloseIncidentCommand(idemKey, id), ct);
        return result.Success ? Results.Ok(new { id = result.IncidentId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> UpdateClaimAsync(HttpContext ctx, IMediator mediator, Guid id, UpdateIncidentClaimRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(
            new UpdateIncidentClaimCommand(idemKey, id, body.PoliceReportNumber, body.InsuranceClaimNumber), ct);
        return result.Success ? Results.Ok(new { id = result.IncidentId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> GetByIdAsync(IMediator mediator, Guid id, CancellationToken ct)
    {
        var detail = await mediator.Send(new GetIncidentByIdQuery(id), ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> SearchAsync(
        IMediator mediator,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50,
        Guid? leaseId = null,
        Guid? vehicleId = null,
        IncidentStatus? status = null,
        IncidentSeverity? severity = null)
    {
        var result = await mediator.Send(new SearchIncidentsQuery(page, pageSize, leaseId, vehicleId, status, severity), ct);
        return Results.Ok(result);
    }

    private static bool TryReadIdempotencyKey(HttpContext ctx, out string key, out IResult error)
    {
        key = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "State-changing incident endpoints require an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }
        error = Results.Ok();
        return true;
    }

    private static IResult MissingBody() => Results.Problem(
        title: "Missing request body",
        detail: "Incident endpoint requires a JSON body.",
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult ToProblem(IncidentCommandResult result) =>
        Results.Problem(
            title: result.ErrorCode ?? "incident.error",
            detail: result.ErrorMessage,
            statusCode: result.ErrorCode switch
            {
                "incident.not_found" => StatusCodes.Status404NotFound,
                "incident.invalid_transition" or "incident.immutable" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status422UnprocessableEntity,
            });
}

public sealed record ReportIncidentRequest
{
    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required Guid ReportedByPersonId { get; init; }
    public required IncidentType Type { get; init; }
    public required IncidentSeverity Severity { get; init; }
    public required DateTimeOffset IncidentTimeUtc { get; init; }
    public required string Description { get; init; }
    public decimal? LocationLat { get; init; }
    public decimal? LocationLng { get; init; }
    public string? LocationDescription { get; init; }
    public string? PoliceReportNumber { get; init; }
    public string? InsuranceClaimNumber { get; init; }
}

public sealed record ResolveIncidentRequest(string ResolutionNotes);
public sealed record UpdateIncidentClaimRequest(string? PoliceReportNumber, string? InsuranceClaimNumber);
