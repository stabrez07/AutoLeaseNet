using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Me;

/// <summary>
/// Handler for <see cref="GetMyLeasesQuery"/>. Trusts the Day-9 RLS predicate to
/// filter rows to the current customer's leases — no app-side
/// <c>WHERE CustomerId = …</c> needed (and including one would be redundant
/// with the DB-side rule).
/// </summary>
public sealed class GetMyLeasesQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetMyLeasesQuery, IReadOnlyList<MyLeaseDto>>
{
    public async Task<IReadOnlyList<MyLeaseDto>> Handle(
        GetMyLeasesQuery request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("/me/leases requires an authenticated tenant context.");
        }
        if (tenant.CustomerId is not Guid customerId || customerId == Guid.Empty)
        {
            // An external user without a CustomerId claim has no portal context;
            // returning an empty list would be misleading. Throw so the BFF maps to 400.
            throw new InvalidOperationException("/me/leases requires a customer context (X-Dev-Customer-Id or customer_id claim).");
        }

        return await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == tenant.TenantId)
            .OrderByDescending(l => l.UpdatedAtUtc)
            .Select(l => new MyLeaseDto(
                l.Id,
                l.TajeerContractNumber,
                (int)l.Status,
                l.ContractStartUtc,
                l.ContractEndUtc,
                l.IssuedAtUtc,
                l.ClosedAtUtc,
                l.RentAmount,
                l.TotalAmount))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
