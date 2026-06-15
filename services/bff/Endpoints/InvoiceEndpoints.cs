using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// BFF invoice endpoints (Phase 1 read-only). Clients fetch invoices for a lease.
/// Future: POST for payment recording, draft preview, etc.
/// </summary>
public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/invoices")
            .WithName("Invoices")
            .WithOpenApi();

        group.MapGet("by-lease/{leaseId:guid}", GetByLeaseAsync)
            .WithName("GetInvoiceByLease")
            .WithDescription("Fetch invoice for a specific lease")
            .WithOpenApi();

        return routes;
    }

    private static async Task<IResult> GetByLeaseAsync(
        Guid leaseId,
        IInvoiceRepository invoices,
        ILeaseRepository leases,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (leaseId == Guid.Empty)
            return Results.BadRequest("Invalid lease ID.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Results.Unauthorized();

        // Verify lease exists
        var lease = await leases.GetByIdAsync(tenantId, leaseId, ct);
        if (lease is null)
            return Results.NotFound($"Lease {leaseId} not found.");

        // Fetch invoice
        var invoice = await invoices.GetByLeaseIdAsync(tenantId, leaseId, ct);
        if (invoice is null)
            return Results.NotFound($"No invoice exists for lease {leaseId} yet. Invoice is auto-generated when lease transitions to Active.");

        // Return invoice DTO (Phase 1: basic fields)
        var dto = new InvoiceDto(
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            LeaseId: invoice.LeaseId,
            CustomerId: invoice.CustomerId,
            Status: invoice.Status.ToString(),
            IssueDateUtc: invoice.IssueDateUtc,
            DueDateUtc: invoice.DueDateUtc,
            BaseAmountSar: invoice.BaseAmountSar,
            VatSar: invoice.VatSar,
            TotalSar: invoice.TotalSar,
            SubmissionAttempts: invoice.SubmissionAttempts,
            LastErrorMessage: invoice.LastErrorMessage,
            ClearedAtUtc: invoice.ClearedAtUtc);

        return Results.Ok(dto);
    }
}

/// <summary>Invoice response DTO (Phase 1).</summary>
public sealed record InvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid LeaseId,
    Guid CustomerId,
    string Status,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    decimal BaseAmountSar,
    decimal VatSar,
    decimal TotalSar,
    int SubmissionAttempts,
    string? LastErrorMessage,
    DateTimeOffset? ClearedAtUtc);
