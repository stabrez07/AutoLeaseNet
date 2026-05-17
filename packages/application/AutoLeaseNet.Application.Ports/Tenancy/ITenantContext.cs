namespace AutoLeaseNet.Application.Ports.Tenancy;

/// <summary>
/// Provides the current request's tenancy context (TenantId, CustomerId, UserType, UserId).
/// Resolved from JWT claims by the BFF's TenancyMiddleware (per doc 01 §3.5).
/// All domain queries flow through this context for RLS enforcement.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? CustomerId { get; }
    Guid? UserId { get; }
    string UserType { get; }
    IReadOnlyList<Guid> BranchIds { get; }
    bool IsInternalStaff { get; }
    bool IsSystem { get; }
}
