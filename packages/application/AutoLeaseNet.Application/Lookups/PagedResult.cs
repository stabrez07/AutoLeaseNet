namespace AutoLeaseNet.Application.Lookups;

/// <summary>
/// Generic paged-list envelope used by every lookup query. Page is 1-based; PageSize is
/// clamped to <see cref="MaxPageSize"/> at the endpoint layer. TotalCount enables UI
/// pagination controls without a follow-up COUNT query.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}
