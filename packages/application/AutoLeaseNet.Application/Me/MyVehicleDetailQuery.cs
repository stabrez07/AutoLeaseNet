using MediatR;

namespace AutoLeaseNet.Application.Me;

/// <summary>
/// Returns the full vehicle detail visible to the current authenticated customer,
/// or <c>null</c> when they don't currently have it (matching the "currently holding"
/// scope of <see cref="MyVehiclesQuery"/> — Active / Extended / Suspended only). The
/// endpoint maps null → 404 so "doesn't exist" and "you don't have it" look the same
/// to the caller.
///
/// <para>
/// Same trust shape as <see cref="MyVehiclesQuery"/>: the lease-side EXISTS check
/// runs under the natural request scope, and the Vehicle read happens inside a
/// bounded <c>SystemTenancyScope</c> (Vehicles RLS is internal-staff-only by Day-9
/// design).
/// </para>
/// </summary>
public sealed record GetMyVehicleDetailQuery(Guid VehicleId) : IRequest<MyVehicleDetailDto?>;

/// <summary>
/// Per-vehicle projection for the Customer Portal "vehicle detail" page. Customer-visible
/// fields only — VIN, engine number, branch refs, financials, telematics, and notes are
/// operator-only and intentionally excluded.
/// </summary>
public sealed record MyVehicleDetailDto(
    Guid Id,
    string PlateNumber,
    string PlateLetters,
    int PlateTypeCode,
    string Make,
    string Model,
    int ModelYear,
    string? Color,
    int FuelTypeCode,
    int TransmissionTypeCode,
    int BodyTypeCode,
    int Seats,
    int CurrentKm,
    DateOnly? LicenseExpiryDate,
    DateOnly? InsuranceExpiryDate,
    DateOnly? InspectionExpiryDate,
    string? InsuranceCompany,
    string? InsurancePolicyNumber,
    int? NextServiceDueKm,
    DateOnly? NextServiceDueDate);
