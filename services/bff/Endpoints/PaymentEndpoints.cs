using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/payments")
            .WithName("Payments")
            .WithOpenApi();

        group.MapGet("", ListPaymentsAsync)
            .WithName("ListPayments")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetPaymentByIdAsync)
            .WithName("GetPaymentById")
            .RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> ListPaymentsAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 30)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.AdvancePayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        var total = await query.CountAsync(ct);
        var payments = await query
            .OrderByDescending(p => p.ReceivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Customers.AsNoTracking(), p => p.CustomerId, c => c.Id, (p, c) => new { p, c })
            .Select(x => new
            {
                x.p.Id,
                x.p.DisplayId,
                x.p.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                x.p.Amount,
                x.p.PaymentMethod,
                x.p.ReceivedDate,
                x.p.ReferenceNumber,
                x.p.Notes,
                x.p.RemainingBalance,
                x.p.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var paymentIds = payments.Select(p => p.Id).ToList();
        var allocations = await db.PaymentAllocations
            .AsNoTracking()
            .Where(a => paymentIds.Contains(a.AdvancePaymentId))
            .Select(a => new
            {
                a.Id,
                a.AdvancePaymentId,
                a.InvoiceId,
                a.InvoiceNumber,
                a.AllocatedAmountSar,
                a.AllocatedAtUtc,
            })
            .ToListAsync(ct);

        var items = payments.Select(p => new
        {
            p.Id,
            p.CustomerId,
            p.CustomerDisplayName,
            p.Amount,
            p.PaymentMethod,
            p.ReceivedDate,
            p.ReferenceNumber,
            p.Notes,
            p.RemainingBalance,
            Allocations = allocations
                .Where(a => a.AdvancePaymentId == p.Id)
                .Select(a => new { a.Id, a.InvoiceId, a.InvoiceNumber, a.AllocatedAmountSar, a.AllocatedAtUtc })
                .ToList(),
            p.CreatedAtUtc,
        }).ToList();

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    private static async Task<IResult> GetPaymentByIdAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var payment = await db.AdvancePayments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Join(db.Customers.AsNoTracking(), p => p.CustomerId, c => c.Id, (p, c) => new { p, c })
            .Select(x => new
            {
                x.p.Id,
                x.p.DisplayId,
                x.p.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                x.p.Amount,
                x.p.PaymentMethod,
                x.p.ReceivedDate,
                x.p.ReferenceNumber,
                x.p.Notes,
                x.p.RemainingBalance,
                x.p.CreatedAtUtc,
            })
            .FirstOrDefaultAsync(ct);

        if (payment is null) return Results.NotFound();

        var allocs = await db.PaymentAllocations.AsNoTracking()
            .Where(a => a.AdvancePaymentId == id)
            .Select(a => new
            {
                a.Id,
                a.InvoiceId,
                a.InvoiceNumber,
                a.AllocatedAmountSar,
                a.AllocatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            payment.Id,
            payment.DisplayId,
            payment.CustomerId,
            payment.CustomerDisplayName,
            payment.Amount,
            payment.PaymentMethod,
            payment.ReceivedDate,
            payment.ReferenceNumber,
            payment.Notes,
            payment.RemainingBalance,
            Allocations = allocs,
            payment.CreatedAtUtc,
        });
    }
}
