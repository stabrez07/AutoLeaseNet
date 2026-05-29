using MediatR;

namespace AutoLeaseNet.Application.Me;

/// <summary>
/// Returns the current authenticated customer's leases. Caller's
/// <c>ITenantContext.CustomerId</c> identifies the customer; the handler
/// applies no app-side <c>WHERE CustomerId = …</c> because the Day-9 RLS
/// predicate enforces it at the row level (defense in depth — see
/// <c>ai_context.md</c> Architecture decision #12).
///
/// <para>Handler in <c>AutoLeaseNet.Infrastructure.Customer</c> so it can use
/// <c>DbContext</c> directly without inverting the Application →
/// Infrastructure dependency.</para>
/// </summary>
public sealed record GetMyLeasesQuery : IRequest<IReadOnlyList<MyLeaseDto>>;

/// <summary>
/// Minimal lease projection for the Customer Portal "My Leases" page.
/// Deliberately small surface — the portal lists leases by status; deeper
/// detail comes from a future per-lease detail endpoint.
/// </summary>
public sealed record MyLeaseDto(
    Guid Id,
    long? TajeerContractNumber,
    int Status,                            // LeaseStatus enum value
    DateTimeOffset ContractStartUtc,
    DateTimeOffset ContractEndUtc,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    decimal RentAmount,
    decimal? TotalAmount);
