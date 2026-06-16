using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Quotation (Spec 02 §6.1) endpoints for PDF generation and distribution.
/// </summary>
public static class QuotationEndpoints
{
    public static IEndpointRouteBuilder MapQuotationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/quotations").WithTags("quotations");

        group.MapPost("/{id:guid}/submit-approval", SubmitApprovalAsync).WithName("SubmitQuotationApproval").RequireAuthorization();
        group.MapPost("/{id:guid}/approvals/{tierLevel:int}/decision", RecordApprovalDecisionAsync).WithName("RecordQuotationApprovalDecision").RequireAuthorization();
        group.MapGet("/approvals/pending", GetPendingApprovalsAsync).WithName("GetPendingQuotationApprovals").RequireAuthorization();
        group.MapPost("/{id:guid}/send-pdf", SendPdfAsync).WithName("SendQuotationPdf").RequireAuthorization();
        group.MapPost("/{id:guid}/accept", AcceptAsync).WithName("AcceptQuotation").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> SubmitApprovalAsync(HttpContext ctx, IMediator mediator, Guid id, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var command = new SubmitQuotationForApprovalCommand(idempotencyKey, id);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> RecordApprovalDecisionAsync(
        HttpContext ctx,
        IMediator mediator,
        IQuotationRepository quotations,
        ITenantContext tenant,
        Guid id,
        int tierLevel,
        QuotationApprovalDecisionRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");
        if (tierLevel is < 1 or > 3)
            return Results.BadRequest("tierLevel must be 1, 2, or 3.");

        var quotation = await quotations.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (quotation is null)
            return Results.NotFound($"Quotation {id} not found.");

        var approval = quotation.Approvals.SingleOrDefault(a => a.TierLevel == tierLevel);
        if (approval is null)
            return Results.NotFound($"Quotation {id} has no approval tier {tierLevel}.");
        if (approval.Status != QuotationApprovalStatus.Pending)
            return Results.Conflict($"Tier {tierLevel} is already {approval.Status}.");
        if (!ctx.User.IsInRole(approval.RequiredRoleCode))
            return Results.Forbid();

        var command = new RecordQuotationApprovalDecisionCommand(
            IdempotencyKey: idempotencyKey,
            QuotationId: id,
            TierLevel: (byte)tierLevel,
            Approved: body.Approved,
            Comment: body.Comment);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetPendingApprovalsAsync(IMediator mediator, CancellationToken ct)
    {
        var pending = await mediator.Send(new GetPendingQuotationApprovalsQuery(), ct);
        return Results.Ok(pending);
    }

    private static async Task<IResult> SendPdfAsync(HttpContext ctx, IMediator mediator, Guid id, SendQuotePdfRequest body, CancellationToken ct)
    {
        if (body is null) return Results.BadRequest("Missing request body.");

        var idemKey = ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key) ? key.ToString() : Guid.NewGuid().ToString();

        var command = new SendQuotePdfCommand(idemKey, id, body.RecipientEmail);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Accepted()
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> AcceptAsync(HttpContext ctx, IMediator mediator, Guid id, AcceptQuotationRequest? body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var command = new AcceptQuotationCommand(
            QuotationId: id,
            CustomerSignature: body?.CustomerSignature,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }
}

public sealed record SendQuotePdfRequest(string RecipientEmail);
public sealed record QuotationApprovalDecisionRequest(bool Approved, string? Comment);
public sealed record AcceptQuotationRequest(string? CustomerSignature);
