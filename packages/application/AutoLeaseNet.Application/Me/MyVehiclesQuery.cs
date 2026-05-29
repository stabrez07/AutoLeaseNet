using MediatR;

namespace AutoLeaseNet.Application.Me;

/// <summary>
/// Returns the vehicles the current authenticated customer currently has — i.e. vehicles
/// attached to leases in <c>Active</c>, <c>Extended</c>, or <c>Suspended</c> state. Closed,
/// Cancelled, Expired, and PendingIssuance leases are excluded ("currently driving" only).
///
/// <para>
/// Vehicles RLS is internal-staff-only by Day-9 design (the policy passes <c>NULL</c> for
/// CustomerId, blocking external reads). The handler bridges that gap by deriving the
/// vehicle id set from RLS-scoped Leases (which DO honour CustomerId) and then re-reading
/// the Vehicles table inside a <c>SystemTenancyScope</c> bounded to the id set. See the
/// handler for the trust-boundary discussion and the Phase-2 follow-up (extend RLS on
/// Vehicles with a customer-derived predicate).
/// </para>
/// </summary>
public sealed record GetMyVehiclesQuery : IRequest<IReadOnlyList<MyVehicleDto>>;

/// <summary>
/// Minimal vehicle projection for the Customer Portal "My Vehicles" page.
/// Plate triple is rendered in Tajeer's KSA Arabic-letter format (Spec 03 §11.1) —
/// presentation-layer conversion to legacy ENG-letter format is a future helper.
/// </summary>
public sealed record MyVehicleDto(
    Guid Id,
    string PlateNumber,
    string PlateLetters,
    int PlateTypeCode,
    string Make,
    string Model,
    int ModelYear,
    string? Color,
    int CurrentKm,
    DateOnly? LicenseExpiryDate,
    DateOnly? InsuranceExpiryDate);
