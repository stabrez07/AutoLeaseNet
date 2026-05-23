using MediatR;

namespace AutoLeaseNet.Application.Lookups;

// ─── Query records ─────────────────────────────────────────────────────────────
// Handlers live in AutoLeaseNet.Infrastructure.Lookups so they can use DbContext
// directly without inverting the Application → Infrastructure dependency direction.

public sealed record GetBranchesQuery : IRequest<IReadOnlyList<BranchDto>>;
public sealed record GetRentPoliciesQuery : IRequest<IReadOnlyList<RentPolicyDto>>;
public sealed record GetExtendedCoveragesQuery : IRequest<IReadOnlyList<ExtendedCoverageDto>>;

public sealed record GetCustomersPagedQuery(int Page, int PageSize, string? Search)
    : IRequest<PagedResult<CustomerSummaryDto>>;

public sealed record GetVehiclesPagedQuery(int Page, int PageSize, string? Search, int? Status)
    : IRequest<PagedResult<VehicleSummaryDto>>;

public sealed record GetDriversPagedQuery(int Page, int PageSize, string? Search)
    : IRequest<PagedResult<DriverSummaryDto>>;
