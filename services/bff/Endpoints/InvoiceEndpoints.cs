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
        string? status = null,
        Guid? leaseId = null,
        Guid? customerId = null)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (leaseId.HasValue && leaseId.Value != Guid.Empty)
            query = query.Where(i => i.LeaseId == leaseId.Value);

        if (customerId.HasValue && customerId.Value != Guid.Empty)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Billing.InvoiceStatus>(status, true, out var st))
            query = query.Where(i => i.Status == st);

        var total = await query.CountAsync(ct);
        var invoices = await query
            .OrderByDescending(i => i.IssueDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Customers.AsNoTracking(), i => i.CustomerId, c => c.Id, (i, c) => new { i, c })
            .Join(db.Leases.AsNoTracking(), x => x.i.LeaseId, l => l.Id, (x, l) => new { x.i, x.c, l })
            .Join(db.Vehicles.AsNoTracking(), x => x.l.VehicleId, v => v.Id, (x, v) => new { x.i, x.c, x.l, v })
            .Select(x => new
            {
                x.i.Id,
                x.i.DisplayId,
                x.i.InvoiceNumber,
                x.i.LeaseId,
                LeaseDisplayId = x.l.DisplayId,
                x.i.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                VehicleMakeModel = x.v.Make + " " + x.v.Model,
                VehiclePlate = x.v.PlateNumber,
                VehiclePlateAr = x.v.PlateLetters + " " + x.v.PlateNumber,
                Status = x.i.Status.ToString(),
                IssuedDate = x.i.IssueDateUtc,
                DueDate = x.i.DueDateUtc,
                SubTotalSar = x.i.BaseAmountSar,
                VatAmountSar = x.i.VatSar,
                x.i.TotalSar,
            })
            .ToListAsync(ct);

        var invoiceIds = invoices.Select(inv => inv.Id).ToList();
        var allocs = await db.PaymentAllocations.AsNoTracking()
            .Where(a => invoiceIds.Contains(a.InvoiceId))
            .Join(db.AdvancePayments.AsNoTracking(), a => a.AdvancePaymentId, p => p.Id, (a, p) => new { a, p })
            .Select(x => new
            {
                x.a.InvoiceId,
                PaymentId = x.p.Id,
                PaymentDisplayId = x.p.DisplayId,
                ReferenceNumber = x.p.ReferenceNumber ?? ("P-" + x.p.DisplayId),
                Amount = x.a.AllocatedAmountSar,
                Date = x.p.ReceivedDate,
                x.p.PaymentMethod,
            })
            .ToListAsync(ct);

        var items = invoices.Select(inv =>
        {
            var ia = allocs.Where(a => a.InvoiceId == inv.Id).ToList();
            var paid = ia.Sum(a => a.Amount);
            return new
            {
                inv.Id,
                inv.DisplayId,
                inv.InvoiceNumber,
                inv.LeaseId,
                LeaseNumber = "L-" + inv.LeaseDisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                inv.CustomerId,
                inv.CustomerDisplayName,
                inv.VehicleMakeModel,
                inv.VehiclePlate,
                inv.VehiclePlateAr,
                inv.Status,
                inv.IssuedDate,
                inv.DueDate,
                inv.SubTotalSar,
                inv.VatAmountSar,
                inv.TotalSar,
                PaidAmountSar = paid,
                BalanceSar = inv.TotalSar - paid,
                Allocations = ia.Select(a => new
                {
                    a.PaymentId,
                    a.PaymentDisplayId,
                    a.ReferenceNumber,
                    a.Amount,
                    a.Date,
                    a.PaymentMethod,
                }),
            };
        }).ToList();

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
            .Join(db.Leases.AsNoTracking(), x => x.i.LeaseId, l => l.Id, (x, l) => new { x.i, x.c, l })
            .Join(db.Vehicles.AsNoTracking(), x => x.l.VehicleId, v => v.Id, (x, v) => new { x.i, x.c, x.l, v })
            .Select(x => new
            {
                x.i.Id,
                x.i.DisplayId,
                x.i.InvoiceNumber,
                x.i.LeaseId,
                LeaseDisplayId = x.l.DisplayId,
                x.i.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                VehicleMakeModel = x.v.Make + " " + x.v.Model,
                VehiclePlate = x.v.PlateNumber,
                VehiclePlateAr = x.v.PlateLetters + " " + x.v.PlateNumber,
                SupplierName = "Auto Lead Company",
                SupplierCrNo = "1010123456",
                SupplierVatNo = "300012345600003",
                QuotationNumber = (string?)null,
                PoNumber = (string?)null,
                BillingPeriodStart = x.i.IssueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                BillingPeriodEnd = x.i.DueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                Status = x.i.Status.ToString(),
                IssuedDate = x.i.IssueDateUtc,
                DueDate = x.i.DueDateUtc,
                Lines = new object[0],
                SubTotalSar = x.i.BaseAmountSar,
                VatAmountSar = x.i.VatSar,
                x.i.TotalSar,
                Notes = (string?)null,
                x.i.CreatedAtUtc,
            })
            .FirstOrDefaultAsync(ct);

        if (inv is null) return Results.NotFound();

        var invAllocs = await db.PaymentAllocations.AsNoTracking()
            .Where(a => a.InvoiceId == id)
            .Join(db.AdvancePayments.AsNoTracking(), a => a.AdvancePaymentId, p => p.Id, (a, p) => new { a, p })
            .Select(x => new
            {
                PaymentId = x.p.Id,
                PaymentDisplayId = x.p.DisplayId,
                ReferenceNumber = x.p.ReferenceNumber ?? ("P-" + x.p.DisplayId),
                Amount = x.a.AllocatedAmountSar,
                Date = x.p.ReceivedDate,
                x.p.PaymentMethod,
            })
            .ToListAsync(ct);

        var paidAmount = invAllocs.Sum(a => a.Amount);

        return Results.Ok(new
        {
            inv.Id,
            inv.DisplayId,
            inv.InvoiceNumber,
            inv.LeaseId,
            LeaseNumber = "L-" + inv.LeaseDisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            inv.CustomerId,
            inv.CustomerDisplayName,
            inv.VehicleMakeModel,
            inv.VehiclePlate,
            inv.VehiclePlateAr,
            inv.SupplierName,
            inv.SupplierCrNo,
            inv.SupplierVatNo,
            inv.QuotationNumber,
            inv.PoNumber,
            inv.BillingPeriodStart,
            inv.BillingPeriodEnd,
            inv.Status,
            inv.IssuedDate,
            inv.DueDate,
            inv.Lines,
            inv.SubTotalSar,
            inv.VatAmountSar,
            inv.TotalSar,
            PaidAmountSar = paidAmount,
            BalanceSar = inv.TotalSar - paidAmount,
            ZatcaInvoiceNumber = (string?)null,
            inv.Notes,
            inv.CreatedAtUtc,
            Allocations = invAllocs,
        });
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
