using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

public static class ApprovalEndpoints
{
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/approvals")
            .WithTags("approvals")
            .RequireAuthorization();

        group.MapGet("/pending", ListPendingAsync).WithName("ListPendingApprovals");

        return routes;
    }

    private static async Task<IResult> ListPendingAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var pendingQuotations = await db.Quotations
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.Status == QuotationStatus.PendingApproval)
            .Include(q => q.Lines)
            .Include(q => q.Approvals)
            .OrderBy(q => q.SubmittedAtUtc)
            .Join(db.Customers.AsNoTracking(), q => q.CustomerId, c => c.Id, (q, c) => new { q, c })
            .Select(x => new
            {
                QuotationId = x.q.Id,
                x.q.DisplayId,
                x.q.QuoteNumber,
                x.q.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                x.q.Status,
                x.q.TotalSar,
                x.q.DiscountPercent,
                x.q.EstimatedDurationMonths,
                x.q.SubmittedAtUtc,
                x.q.CreatedAtUtc,
                LineCount = x.q.Lines.Count,
                Approvals = x.q.Approvals
                    .OrderBy(a => a.TierLevel)
                    .Select(a => new
                    {
                        a.TierLevel,
                        a.RequiredRoleCode,
                        Status = a.Status.ToString(),
                        a.Comment,
                        a.DecisionAtUtc,
                    })
                    .ToList(),
            })
            .ToListAsync(ct);

        return Results.Ok(new { items = pendingQuotations, totalCount = pendingQuotations.Count });
    }
}
