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
/// The handler calls Tajeer <c>CalculatePayment</c> + <c>CloseContract</c> before
/// the local commit; the money preview comes back in the response <c>payment</c> block.
/// </summary>
public static class LeaseEndpoints
{
    public static IEndpointRouteBuilder MapLeaseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/leases").WithTags("leases");

        group.MapPost("/{id:guid}/check-in", CheckInAsync)
            .WithName("CheckInLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/extend", ExtendAsync)
            .WithName("ExtendLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/suspend", SuspendAsync)
            .WithName("SuspendLease")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ExtendAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        ExtendLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/extend requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/extend requires a JSON body with at least newContractEndUtc.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new ExtendLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
            NewContractEndUtc = body.NewContractEndUtc,
            ExtensionReasonCode = body.ExtensionReasonCode,
            AdditionalCharges = body.AdditionalCharges,
            PaymentMethodCode = body.PaymentMethodCode,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.extend.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.extend.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            status = result.LeaseStatus,
            newContractEndUtc = result.NewContractEndUtc,
            extensionCount = result.ExtensionCount,
            charges = result.Charges is null ? null : new
            {
                totalDue = result.Charges.TotalDue,
                vatAmount = result.Charges.VatAmount,
                grandTotal = result.Charges.GrandTotal,
            },
        });
    }

    private static async Task<IResult> SuspendAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        SuspendLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/suspend requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/suspend requires a JSON body with at least suspensionReasonCode.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new SuspendLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
            SuspensionReasonCode = body.SuspensionReasonCode,
            Notes = body.Notes,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.suspend.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.suspend.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            status = result.LeaseStatus,
            suspensionReasonCode = result.SuspensionReasonCode,
            suspendedAtUtc = result.SuspendedAtUtc,
        });
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
            ExtraKm = body.ExtraKm,
            AdditionalCharges = body.AdditionalCharges,
            DiscountAmount = body.DiscountAmount,
            FinalPaidAmount = body.FinalPaidAmount,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.calculate.transient" or "tajeer.close.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.check_in.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            inspectionId = result.InspectionId,
            status = result.LeaseStatus,
            payment = result.Payment is null ? null : new
            {
                rentAmount = result.Payment.RentAmount,
                paidAmount = result.Payment.PaidAmount,
                lateHoursFee = result.Payment.LateHoursFee,
                extraKmFee = result.Payment.ExtraKmFee,
                damagesFee = result.Payment.DamagesFee,
                discountAmount = result.Payment.DiscountAmount,
                totalDue = result.Payment.TotalDue,
                vatAmount = result.Payment.VatAmount,
                grandTotal = result.Payment.GrandTotal,
                finalPaidAmount = result.Payment.FinalPaidAmount,
            },
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

    /// <summary>Caller-declared extra-km overage. Optional — Tajeer can compute from contract allowance.</summary>
    public int? ExtraKm { get; init; }
    /// <summary>Caller-declared additional charges (damages, cleaning, refuelling, etc.).</summary>
    public decimal? AdditionalCharges { get; init; }
    /// <summary>Discount applied at close — Tajeer validates server-side.</summary>
    public decimal? DiscountAmount { get; init; }
    /// <summary>What ops actually collected at the counter — passed to Tajeer's CloseContract.</summary>
    public decimal? FinalPaidAmount { get; init; }
}

public sealed record ExtendLeaseRequest
{
    /// <summary>New UTC contract end — must be strictly after the current one.</summary>
    public required DateTimeOffset NewContractEndUtc { get; init; }
    public int? ExtensionReasonCode { get; init; }
    public decimal? AdditionalCharges { get; init; }
    public int? PaymentMethodCode { get; init; }
}

public sealed record SuspendLeaseRequest
{
    public required int SuspensionReasonCode { get; init; }
    public string? Notes { get; init; }
}
