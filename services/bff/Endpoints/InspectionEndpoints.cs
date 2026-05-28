using AutoLeaseNet.Application.Operations;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// E-Check (<see cref="Inspection"/>) endpoints. State-changing POSTs require an
/// <c>Idempotency-Key</c> header (CLAUDE.md §8); reads use the standard dev JWT stub
/// for tenant resolution. Photo upload still takes a pre-computed blob URI — the real
/// upload flow through <c>Adapters.Storage</c> is a separate workstream.
/// </summary>
public static class InspectionEndpoints
{
    public static IEndpointRouteBuilder MapInspectionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/inspections").WithTags("inspections");

        group.MapPost("/", StartAsync).WithName("StartInspection").RequireAuthorization();
        group.MapPost("/{id:guid}/photos", AddPhotoAsync).WithName("AddInspectionPhoto").RequireAuthorization();
        group.MapPost("/{id:guid}/damage-markers", AddDamageMarkerAsync).WithName("AddInspectionDamageMarker").RequireAuthorization();
        group.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteInspection").RequireAuthorization();
        group.MapPost("/{id:guid}/abandon", AbandonAsync).WithName("AbandonInspection").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetInspectionById").RequireAuthorization();

        var lookups = routes.MapGroup("/lookups/inspections").WithTags("lookups");
        lookups.MapGet("/", SearchAsync).WithName("ListInspections").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> StartAsync(HttpContext ctx, IMediator mediator, StartInspectionRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();

        var command = new StartInspectionCommand
        {
            IdempotencyKey = idemKey,
            VehicleId = body.VehicleId,
            LeaseId = body.LeaseId,
            Type = body.Type,
            OdometerKm = body.OdometerKm,
            FuelLevel = body.FuelLevel,
            AcCondition = body.AcCondition,
            RadioStereoCondition = body.RadioStereoCondition,
            ScreenCondition = body.ScreenCondition,
            SpeedometerCondition = body.SpeedometerCondition,
            KeysCondition = body.KeysCondition,
            CarSeatsCondition = body.CarSeatsCondition,
            SafetyTriangleCondition = body.SafetyTriangleCondition,
            FireExtinguisherCondition = body.FireExtinguisherCondition,
            FirstAidKitCondition = body.FirstAidKitCondition,
            SpareTireToolsCondition = body.SpareTireToolsCondition,
            TiresCondition = body.TiresCondition,
            SpareTireCondition = body.SpareTireCondition,
            Other1 = body.Other1,
            Other2 = body.Other2,
            Notes = body.Notes,
            SketchInfoJson = body.SketchInfoJson,
            RenterSignatureBlobUri = body.RenterSignatureBlobUri,
            InitialPhotos = body.InitialPhotos,
            InitialDamageMarkers = body.InitialDamageMarkers,
        };
        var result = await mediator.Send(command, ct);
        return result.Success
            ? Results.Created($"/api/v1/inspections/{result.InspectionId}", new { id = result.InspectionId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> AddPhotoAsync(HttpContext ctx, IMediator mediator, Guid id, AddPhotoRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new AddInspectionPhotoCommand(idemKey, id, body.BlobUri, body.Sequence), ct);
        return result.Success ? Results.Ok(new { id = result.InspectionId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> AddDamageMarkerAsync(HttpContext ctx, IMediator mediator, Guid id, AddDamageMarkerRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new AddDamageMarkerCommand(idemKey, id, body.Type, body.PositionX, body.PositionY), ct);
        return result.Success ? Results.Ok(new { id = result.InspectionId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> CompleteAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new CompleteInspectionCommand(idemKey, id), ct);
        return result.Success ? Results.Ok(new { id = result.InspectionId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> AbandonAsync(HttpContext ctx, IMediator mediator, Guid id, AbandonInspectionRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new AbandonInspectionCommand(idemKey, id, body.Reason), ct);
        return result.Success ? Results.Ok(new { id = result.InspectionId, status = result.Status }) : ToProblem(result);
    }

    private static async Task<IResult> GetByIdAsync(IMediator mediator, Guid id, CancellationToken ct)
    {
        var detail = await mediator.Send(new GetInspectionByIdQuery(id), ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> SearchAsync(
        IMediator mediator,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50,
        Guid? vehicleId = null,
        Guid? leaseId = null,
        InspectionType? type = null,
        InspectionStatus? status = null)
    {
        var result = await mediator.Send(new SearchInspectionsQuery(page, pageSize, vehicleId, leaseId, type, status), ct);
        return Results.Ok(result);
    }

    private static bool TryReadIdempotencyKey(HttpContext ctx, out string key, out IResult error)
    {
        key = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "State-changing inspection endpoints require an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }
        error = Results.Ok();
        return true;
    }

    private static IResult MissingBody() => Results.Problem(
        title: "Missing request body",
        detail: "Inspection endpoint requires a JSON body.",
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult ToProblem(InspectionCommandResult result) =>
        Results.Problem(
            title: result.ErrorCode ?? "inspection.error",
            detail: result.ErrorMessage,
            statusCode: result.ErrorCode switch
            {
                "inspection.not_found" => StatusCodes.Status404NotFound,
                "inspection.illegal_transition" or "inspection.immutable" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status422UnprocessableEntity,
            });
}

public sealed record StartInspectionRequest
{
    public required Guid VehicleId { get; init; }
    public Guid? LeaseId { get; init; }
    public required InspectionType Type { get; init; }
    public required int OdometerKm { get; init; }
    public required FuelLevel FuelLevel { get; init; }
    public byte? AcCondition { get; init; }
    public byte? RadioStereoCondition { get; init; }
    public byte? ScreenCondition { get; init; }
    public byte? SpeedometerCondition { get; init; }
    public byte? KeysCondition { get; init; }
    public byte? CarSeatsCondition { get; init; }
    public byte? SafetyTriangleCondition { get; init; }
    public byte? FireExtinguisherCondition { get; init; }
    public byte? FirstAidKitCondition { get; init; }
    public byte? SpareTireToolsCondition { get; init; }
    public byte? TiresCondition { get; init; }
    public byte? SpareTireCondition { get; init; }
    public string? Other1 { get; init; }
    public string? Other2 { get; init; }
    public string? Notes { get; init; }
    public string? SketchInfoJson { get; init; }
    public string? RenterSignatureBlobUri { get; init; }
    public IReadOnlyList<string>? InitialPhotos { get; init; }
    public IReadOnlyList<InitialDamageMarker>? InitialDamageMarkers { get; init; }
}

public sealed record AddPhotoRequest(string BlobUri, int Sequence);
public sealed record AddDamageMarkerRequest(DamageMarkerType Type, decimal PositionX, decimal PositionY);
public sealed record AbandonInspectionRequest(string Reason);
