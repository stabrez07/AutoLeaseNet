using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Quotation CRUD + approval workflow endpoints (Spec 01 §5.4, Spec 02 §6.1).
/// All state-changing POSTs require an <c>Idempotency-Key</c> header (CLAUDE.md §8).
/// </summary>
public static class QuotationEndpoints
{
    public static IEndpointRouteBuilder MapQuotationEndpoints(this IEndpointRouteBuilder routes)
    {
        var q = routes.MapGroup("/quotations").WithTags("quotations");

        q.MapPost("/", CreateAsync).WithName("CreateQuotation").RequireAuthorization();
        q.MapPost("/{id:guid}/lines", AddLineAsync).WithName("AddQuotationLine").RequireAuthorization();
        q.MapPost("/{id:guid}/submit", SubmitAsync).WithName("SubmitQuotationForApproval").RequireAuthorization();
        q.MapPost("/{id:guid}/approve", ApproveAsync).WithName("ApproveQuotation").RequireAuthorization();
        q.MapPost("/{id:guid}/reject", RejectAsync).WithName("RejectQuotation").RequireAuthorization();
        q.MapPost("/{id:guid}/send", SendAsync).WithName("MarkQuotationSentToCustomer").RequireAuthorization();
        q.MapPost("/{id:guid}/recall", RecallAsync).WithName("RecallQuotation").RequireAuthorization();

        var inbox = routes.MapGroup("/approvals").WithTags("approvals");
        inbox.MapGet("/pending", GetInboxAsync).WithName("GetApprovalInbox").RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> CreateAsync(HttpContext ctx, IMediator mediator, CreateQuotationRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();

        var command = new CreateQuotationCommand
        {
            IdempotencyKey = idemKey,
            CustomerId = body.CustomerId,
            ValidUntilDate = body.ValidUntilDate,
            ContractType = body.ContractType,
            EstimatedDurationMonths = body.EstimatedDurationMonths,
            DiscountPercent = body.DiscountPercent,
            TermsAndConditionsMd = body.TermsAndConditionsMd,
            Lines = body.Lines?.Select(l => new CreateQuotationLineDto(
                l.ItemType, l.Description, l.VehicleSpecRef, l.Quantity, l.UnitPriceSar, l.DiscountPercent)).ToList()
                ?? [],
        };
        var result = await mediator.Send(command, ct);
        if (!result.Success) return ToProblem(result);
        return Results.Created(
            $"/api/v1/quotations/{result.QuotationId}",
            new { id = result.QuotationId, quoteNumber = result.QuoteNumber, status = result.Status });
    }

    private static async Task<IResult> AddLineAsync(HttpContext ctx, IMediator mediator, Guid id, AddQuotationLineRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();

        var result = await mediator.Send(new AddQuotationLineCommand
        {
            IdempotencyKey = idemKey,
            QuotationId = id,
            ItemType = body.ItemType,
            Description = body.Description,
            VehicleSpecRef = body.VehicleSpecRef,
            Quantity = body.Quantity,
            UnitPriceSar = body.UnitPriceSar,
            DiscountPercent = body.DiscountPercent,
        }, ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status, subTotalSar = result.SubTotalSar, totalSar = result.TotalSar })
            : ToProblem(result);
    }

    private static async Task<IResult> SubmitAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new SubmitQuotationForApprovalCommand(idemKey, id), ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status, requiredTierLevels = result.RequiredTierLevels })
            : ToProblem(result);
    }

    private static async Task<IResult> ApproveAsync(HttpContext ctx, IMediator mediator, Guid id, ApprovalDecisionRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new RecordApprovalDecisionCommand
        {
            IdempotencyKey = idemKey,
            QuotationId = id,
            TierLevel = body.TierLevel,
            Approved = true,
            Notes = body.Notes,
        }, ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> RejectAsync(HttpContext ctx, IMediator mediator, Guid id, ApprovalDecisionRequest body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        if (body is null) return MissingBody();
        var result = await mediator.Send(new RecordApprovalDecisionCommand
        {
            IdempotencyKey = idemKey,
            QuotationId = id,
            TierLevel = body.TierLevel,
            Approved = false,
            Notes = body.Notes,
        }, ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> SendAsync(HttpContext ctx, IMediator mediator, Guid id, SendQuotationRequest? body, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new MarkQuotationSentToCustomerCommand
        {
            IdempotencyKey = idemKey,
            QuotationId = id,
            PdfBlobUri = body?.PdfBlobUri,
        }, ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> RecallAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        if (!TryReadIdempotencyKey(ctx, out var idemKey, out var err)) return err;
        var result = await mediator.Send(new RecallQuotationCommand(idemKey, id), ct);
        return result.Success
            ? Results.Ok(new { id = result.QuotationId, status = result.Status })
            : ToProblem(result);
    }

    private static async Task<IResult> GetInboxAsync(
        IMediator mediator,
        CancellationToken ct,
        int page = 1,
        int pageSize = 50,
        string? roleCode = null)
    {
        var result = await mediator.Send(new GetApprovalInboxQuery(page, pageSize, roleCode), ct);
        return Results.Ok(result);
    }

    private static bool TryReadIdempotencyKey(HttpContext ctx, out string key, out IResult error)
    {
        key = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "State-changing quotation endpoints require an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }
        error = Results.Ok();
        return true;
    }

    private static IResult MissingBody() => Results.Problem(
        title: "Missing request body",
        detail: "Quotation endpoint requires a JSON body.",
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult ToProblem(QuotationCommandResult result) =>
        Results.Problem(
            title: result.ErrorCode ?? "quotation.error",
            detail: result.ErrorMessage,
            statusCode: result.ErrorCode switch
            {
                "quotation.not_found" => StatusCodes.Status404NotFound,
                "quotation.invalid_state" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status422UnprocessableEntity,
            });
}

// ─── Request records ──────────────────────────────────────────────────────────

public sealed record CreateQuotationRequest
{
    public required Guid CustomerId { get; init; }
    public required DateOnly ValidUntilDate { get; init; }
    public required QuotationContractType ContractType { get; init; }
    public int EstimatedDurationMonths { get; init; }
    public decimal DiscountPercent { get; init; }
    public string? TermsAndConditionsMd { get; init; }
    public IReadOnlyList<CreateQuotationLineRequest>? Lines { get; init; }
}

public sealed record CreateQuotationLineRequest(
    QuotationItemType ItemType,
    string Description,
    string? VehicleSpecRef,
    int Quantity,
    decimal UnitPriceSar,
    decimal DiscountPercent);

public sealed record AddQuotationLineRequest
{
    public required QuotationItemType ItemType { get; init; }
    public required string Description { get; init; }
    public string? VehicleSpecRef { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPriceSar { get; init; }
    public decimal DiscountPercent { get; init; }
}

public sealed record ApprovalDecisionRequest(byte TierLevel, string? Notes);
public sealed record SendQuotationRequest(string? PdfBlobUri);
