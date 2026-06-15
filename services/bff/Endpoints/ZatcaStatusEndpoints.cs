using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Zatca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// BFF endpoints for ZATCA submission state (read-only Phase 1).
/// GET /api/v1/invoices/{id}/zatca-status — retrieve submission state + transaction ID + clearance timestamp.
/// </summary>
public static class ZatcaStatusEndpoints
{
    public static IEndpointRouteBuilder MapZatcaStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/v1/invoices/{id:guid}/zatca-status")
            .WithName("ZATCA Status")
            .WithOpenApi();

        group.MapGet(string.Empty, GetZatcaStatusAsync)
            .WithName("GetZatcaStatus")
            .WithSummary("Get ZATCA submission status for invoice")
            .WithDescription("Returns submission state (Draft, Submitted, Cleared), transaction ID, and clearance timestamp.")
            .Produces<ZatcaStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> GetZatcaStatusAsync(
        Guid id,
        IZatcaSubmissionRepository submissionRepository,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.GetByInvoiceIdAsync(id, cancellationToken);

        if (submission == null)
            return Results.NotFound(new { message = "ZATCA submission not found for this invoice." });

        var dto = new ZatcaStatusDto(
            SubmissionId: submission.Id,
            InvoiceId: submission.InvoiceId,
            Status: submission.Status.ToString(),
            TransactionId: submission.ZatcaTransactionId,
            ReportingStatus: submission.ZatcaReportingStatus,
            ClearedAtUtc: submission.ClearedAtUtc,
            ErrorMessage: submission.LastErrorMessage,
            SubmissionAttempts: submission.SubmissionAttempts);

        return Results.Ok(dto);
    }
}

/// <summary>Response DTO for ZATCA submission status.</summary>
public sealed record ZatcaStatusDto(
    Guid SubmissionId,
    Guid InvoiceId,
    string Status,
    string? TransactionId,
    string? ReportingStatus,
    DateTimeOffset? ClearedAtUtc,
    string? ErrorMessage,
    int SubmissionAttempts);
