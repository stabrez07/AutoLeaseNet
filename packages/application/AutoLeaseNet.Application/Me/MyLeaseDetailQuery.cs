using MediatR;

namespace AutoLeaseNet.Application.Me;

/// <summary>
/// Returns the full lease detail visible to the current authenticated customer,
/// or <c>null</c> if the lease id isn't visible (so the endpoint can map to 404
/// without leaking the existence of leases the caller doesn't own).
///
/// <para>
/// Vehicles RLS is internal-staff-only by Day-9 design, so when the lease has a
/// <c>VehicleId</c> the handler opens a bounded <c>SystemTenancyScope</c> for the
/// vehicle read. The lease's own <c>VehicleId</c> column is the trust anchor —
/// see <see cref="MyVehiclesQuery"/> handler for the same pattern.
/// </para>
/// </summary>
public sealed record GetMyLeaseDetailQuery(Guid LeaseId) : IRequest<MyLeaseDetailDto?>;

/// <summary>
/// Per-lease projection for the Customer Portal "lease detail" page. Surface is
/// larger than the list-row DTO (<see cref="MyLeaseDto"/>): adds contract terms,
/// the full payment block, the lifecycle timeline (Saved / Issued / Suspended /
/// Resumed / Closed / Cancelled / Expired), and an optional vehicle summary.
/// </summary>
public sealed record MyLeaseDetailDto(
    Guid Id,
    long? TajeerContractNumber,
    int Status,
    int ContractTypeCode,
    DateTimeOffset ContractStartUtc,
    DateTimeOffset ContractEndUtc,
    DateTimeOffset? ActualReturnUtc,
    int AllowedKmPerHour,
    int AllowedKmPerDay,
    bool UnlimitedKm,
    int AllowedLateHours,
    int ExtensionCount,
    // Payment block (mirrored from Tajeer mainPaymentDetails).
    decimal RentAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    decimal VatAmount,
    decimal TotalAmount,
    int PaymentMethodCode,
    int? DiscountType,
    decimal? DiscountValue,
    // Timeline.
    DateTimeOffset? SavedAtUtc,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? ResumedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    // Optional reason codes when the lease is suspended/closed.
    int? SuspensionReasonCode,
    int? ClosureMainReasonCode,
    int? ClosureSubReasonCode,
    // Nested vehicle, null when Lease.VehicleId is null.
    LeaseVehicleSummaryDto? Vehicle);

/// <summary>Compact vehicle snapshot shown on the lease detail page.</summary>
public sealed record LeaseVehicleSummaryDto(
    Guid Id,
    string PlateNumber,
    string PlateLetters,
    int PlateTypeCode,
    string Make,
    string Model,
    int ModelYear,
    string? Color);
