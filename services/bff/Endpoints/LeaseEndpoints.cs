using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Day-19 check-in saga endpoint. Wraps <see cref="CheckInLeaseCommand"/> behind a
/// dev-JWT-stub-authenticated POST that requires <c>Idempotency-Key</c> (CLAUDE.md §8).
/// Tajeer <c>CalculateContractPayment</c> + <c>CloseContract</c> calls land later;
/// this endpoint is the local-only commit slice for the saga.
/// </summary>
public static class LeaseEndpoints
{
    public static IEndpointRouteBuilder MapLeaseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/leases").WithTags("leases");

        group.MapPost("/{id:guid}/check-in", CheckInAsync)
            .WithName("CheckInLease")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CheckInAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        CheckInLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/check-in requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/check-in requires a JSON body with at least odometerKm + fuelLevel + closureMainReasonCode.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new CheckInLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
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
            Notes = body.Notes,
            SketchInfoJson = body.SketchInfoJson,
            DamagesObserved = body.DamagesObserved,
            ReturnConditionNotes = body.ReturnConditionNotes,
            ClosureMainReasonCode = body.ClosureMainReasonCode,
            ClosureSubReasonCode = body.ClosureSubReasonCode,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.check_in.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            inspectionId = result.InspectionId,
            status = result.LeaseStatus,
        });
    }
}

public sealed record CheckInLeaseRequest
{
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
    public string? Notes { get; init; }
    public string? SketchInfoJson { get; init; }
    public string? DamagesObserved { get; init; }
    public string? ReturnConditionNotes { get; init; }
    public required int ClosureMainReasonCode { get; init; }
    public int? ClosureSubReasonCode { get; init; }
}
