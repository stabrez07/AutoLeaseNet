using AutoLeaseNet.Application.Sales;
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

        group.MapPost("/{id:guid}/send-pdf", SendPdfAsync).WithName("SendQuotationPdf").RequireAuthorization();

        return group;
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
}

public sealed record SendQuotePdfRequest(string RecipientEmail);
