using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Sales;

public sealed class GetApprovalInboxQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetApprovalInboxQuery, PagedResult<ApprovalInboxItemDto>>
{
    public async Task<PagedResult<ApprovalInboxItemDto>> Handle(GetApprovalInboxQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("GetApprovalInboxQuery requires an authenticated tenant context.");

        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize < 1 ? PagedResult<ApprovalInboxItemDto>.DefaultPageSize : request.PageSize;
        if (size > PagedResult<ApprovalInboxItemDto>.MaxPageSize)
            size = PagedResult<ApprovalInboxItemDto>.MaxPageSize;

        var approvalQuery = db.QuotationApprovals
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Status == QuotationApprovalStatus.Pending);

        if (!string.IsNullOrWhiteSpace(request.RequiredRoleCode))
            approvalQuery = approvalQuery.Where(a => a.RequiredRoleCode == request.RequiredRoleCode);

        var total = await approvalQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await approvalQuery
            .Join(db.Quotations.AsNoTracking().Where(q => q.TenantId == tenantId && q.Status == QuotationStatus.PendingApproval),
                a => a.QuotationId,
                q => q.Id,
                (a, q) => new { Approval = a, Quotation = q })
            .Join(db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId),
                x => x.Quotation.CustomerId,
                c => c.Id,
                (x, c) => new { x.Approval, x.Quotation, Customer = c })
            .OrderBy(x => x.Quotation.SubmittedAtUtc)
            .ThenBy(x => x.Approval.TierLevel)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new ApprovalInboxItemDto(
                x.Quotation.Id,
                x.Quotation.QuoteNumber,
                x.Approval.TierLevel,
                x.Approval.RequiredRoleCode,
                x.Quotation.TotalSar,
                x.Customer.DisplayName,
                x.Quotation.SubmittedAtUtc!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ApprovalInboxItemDto>(rows, page, size, total);
    }
}
