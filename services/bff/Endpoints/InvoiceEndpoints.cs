using AutoLeaseNet.Application.Billing;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("", ListInvoicesAsync)
            .WithName("ListInvoices")
            .WithDescription("List all invoices for the tenant")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapGet("{id:guid}", GetInvoiceByIdAsync)
            .WithName("GetInvoiceById")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapGet("by-lease/{leaseId:guid}", GetByLeaseAsync)
            .WithName("GetInvoiceByLease")
            .WithDescription("Fetch invoice for a specific lease")
            .WithOpenApi();

        group.MapPost("{id:guid}/submit-zatca", SubmitToZatcaAsync)
            .WithName("SubmitInvoiceToZatca")
            .WithDescription("Trigger ZATCA clearance submission for an invoice. Idempotent.")
            .WithOpenApi()
            .RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> ListInvoicesAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20,
        string? status = null)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Billing.InvoiceStatus>(status, true, out var st))
            query = query.Where(i => i.Status == st);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Customers.AsNoTracking(), i => i.CustomerId, c => c.Id, (i, c) => new { i, c })
            .Join(db.Leases.AsNoTracking(), x => x.i.LeaseId, l => l.Id, (x, l) => new { x.i, x.c, l })
            .Select(x => new
            {
                x.i.Id,
                InvoiceNumber = x.i.InvoiceNumber,
                LeaseId = x.i.LeaseId,
                CustomerId = x.i.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                Status = x.i.Status.ToString(),
                IssueDateUtc = x.i.IssueDateUtc,
                DueDateUtc = x.i.DueDateUtc,
                BaseAmountSar = x.i.BaseAmountSar,
                VatSar = x.i.VatSar,
                TotalSar = x.i.TotalSar,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    private static async Task<IResult> GetInvoiceByIdAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var inv = await db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Id == id)
            .Join(db.Customers.AsNoTracking(), i => i.CustomerId, c => c.Id, (i, c) => new { i, c })
            .Select(x => new
            {
                x.i.Id,
                x.i.InvoiceNumber,
                x.i.LeaseId,
                x.i.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                Status = x.i.Status.ToString(),
                x.i.IssueDateUtc,
                x.i.DueDateUtc,
                x.i.BaseAmountSar,
                x.i.VatSar,
                x.i.TotalSar,
            })
            .FirstOrDefaultAsync(ct);

        return inv is null ? Results.NotFound() : Results.Ok(inv);
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

    private static async Task<IResult> SubmitToZatcaAsync(
        Guid id,
        IMediator mediator,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Results.Unauthorized();

        if (id == Guid.Empty)
            return Results.BadRequest("Invalid invoice ID.");

        var command = new SubmitInvoiceToZatcaCommand(tenantId, id);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.Message, title: "zatca.submission_failed", statusCode: 502);
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
