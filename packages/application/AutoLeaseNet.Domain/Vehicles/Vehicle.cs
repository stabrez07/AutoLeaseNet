using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Vehicles;

/// <summary>
/// Vehicle aggregate root. Carries fleet identification, regulatory (license / insurance /
/// MVPI inspection expiries), service schedule, financial (purchase price + date), and
/// telematics references — every field a fleet ops report or BI dashboard will eventually
/// roll up.
///
/// <para>
/// Plate triple (<see cref="PlateNumber"/>, <see cref="PlateLetters"/>,
/// <see cref="PlateTypeCode"/>) is captured in Tajeer's KSA format (Spec 03 §11.1);
/// presentation-layer conversion to legacy ENG-letter format is a separate helper.
/// </para>
/// </summary>
public sealed class Vehicle : Entity
{
    public VehicleStatus Status { get; private set; }

    // ─── Identification ─────────────────────────────────────────────────────
    /// <summary>Numeric portion of the plate (e.g. "1234").</summary>
    public string PlateNumber { get; private set; } = string.Empty;
    /// <summary>Letters portion in Tajeer's Arabic-letter format (Spec 03 §11.1).</summary>
    public string PlateLetters { get; private set; } = string.Empty;
    /// <summary>Plate type code (private, taxi, public-transport, …) — Tajeer lookup.</summary>
    public int PlateTypeCode { get; private set; }
    public string Vin { get; private set; } = string.Empty;
    public string? EngineNumber { get; private set; }

    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int ModelYear { get; private set; }
    public string? Color { get; private set; }
    public FuelType FuelType { get; private set; }
    public TransmissionType TransmissionType { get; private set; }
    public BodyType BodyType { get; private set; }
    public int Seats { get; private set; }

    // ─── Regulatory ─────────────────────────────────────────────────────────
    public DateOnly? LicenseExpiryDate { get; private set; }
    public DateOnly? InsuranceExpiryDate { get; private set; }
    public DateOnly? InspectionExpiryDate { get; private set; }
    public string? InsuranceCompany { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }

    // ─── Branch + assignment ────────────────────────────────────────────────
    public Guid OwnerBranchId { get; private set; }
    public Guid CurrentBranchId { get; private set; }

    // ─── Service / KM ───────────────────────────────────────────────────────
    public int CurrentKm { get; private set; }
    public int? LastServiceKm { get; private set; }
    public DateOnly? LastServiceDate { get; private set; }
    public int? NextServiceDueKm { get; private set; }
    public DateOnly? NextServiceDueDate { get; private set; }

    // ─── Financial ──────────────────────────────────────────────────────────
    public decimal? PurchasePrice { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public string? PurchaseInvoiceRef { get; private set; }
    public decimal? DepreciationPerMonth { get; private set; }
    public decimal? CurrentBookValue { get; private set; }

    // ─── Telematics (Phase 3 fields, present from day one for BI) ───────────
    public string? TelematicsProvider { get; private set; }
    public string? DeviceImei { get; private set; }
    public DateTimeOffset? LastTelemetryAtUtc { get; private set; }

    public string? Notes { get; private set; }

    private Vehicle() { }

    public static Vehicle Create(VehicleCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PlateNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PlateLetters);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Vin);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Make);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Model);
        ArgumentOutOfRangeException.ThrowIfLessThan(input.ModelYear, 1990);
        ArgumentOutOfRangeException.ThrowIfNegative(input.CurrentKm);

        return new Vehicle
        {
            TenantId = input.TenantId,
            Status = VehicleStatus.Available,
            PlateNumber = input.PlateNumber,
            PlateLetters = input.PlateLetters,
            PlateTypeCode = input.PlateTypeCode,
            Vin = input.Vin,
            EngineNumber = input.EngineNumber,
            Make = input.Make,
            Model = input.Model,
            ModelYear = input.ModelYear,
            Color = input.Color,
            FuelType = input.FuelType,
            TransmissionType = input.TransmissionType,
            BodyType = input.BodyType,
            Seats = input.Seats,
            LicenseExpiryDate = input.LicenseExpiryDate,
            InsuranceExpiryDate = input.InsuranceExpiryDate,
            InspectionExpiryDate = input.InspectionExpiryDate,
            InsuranceCompany = input.InsuranceCompany,
            InsurancePolicyNumber = input.InsurancePolicyNumber,
            OwnerBranchId = input.OwnerBranchId,
            CurrentBranchId = input.CurrentBranchId ?? input.OwnerBranchId,
            CurrentKm = input.CurrentKm,
            PurchasePrice = input.PurchasePrice,
            PurchaseDate = input.PurchaseDate,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void Reserve(DateTimeOffset nowUtc)
    {
        if (Status != VehicleStatus.Available)
            throw new InvalidOperationException($"Vehicle {Id} must be Available to Reserve (was {Status}).");
        Status = VehicleStatus.Reserved;
        UpdatedAtUtc = nowUtc;
    }

    public void StartRental(DateTimeOffset nowUtc)
    {
        if (Status != VehicleStatus.Reserved && Status != VehicleStatus.Available)
            throw new InvalidOperationException($"Vehicle {Id} must be Available/Reserved to start rental (was {Status}).");
        Status = VehicleStatus.OnRent;
        UpdatedAtUtc = nowUtc;
    }

    public void Return(int endKm, DateTimeOffset nowUtc)
    {
        if (Status != VehicleStatus.OnRent)
            throw new InvalidOperationException($"Vehicle {Id} must be OnRent to Return (was {Status}).");
        ArgumentOutOfRangeException.ThrowIfLessThan(endKm, CurrentKm);
        CurrentKm = endKm;
        Status = VehicleStatus.Available;
        UpdatedAtUtc = nowUtc;
    }

    public void EnterService(DateTimeOffset nowUtc)
    {
        Status = VehicleStatus.InService;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkDamaged(string notes, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        Status = VehicleStatus.Damaged;
        Notes = notes;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed record VehicleCreateInput
{
    public required Guid TenantId { get; init; }
    public required string PlateNumber { get; init; }
    public required string PlateLetters { get; init; }
    public required int PlateTypeCode { get; init; }
    public required string Vin { get; init; }
    public string? EngineNumber { get; init; }
    public required string Make { get; init; }
    public required string Model { get; init; }
    public required int ModelYear { get; init; }
    public string? Color { get; init; }
    public FuelType FuelType { get; init; } = FuelType.Petrol91;
    public TransmissionType TransmissionType { get; init; } = TransmissionType.Automatic;
    public BodyType BodyType { get; init; } = BodyType.Sedan;
    public int Seats { get; init; } = 5;
    public DateOnly? LicenseExpiryDate { get; init; }
    public DateOnly? InsuranceExpiryDate { get; init; }
    public DateOnly? InspectionExpiryDate { get; init; }
    public string? InsuranceCompany { get; init; }
    public string? InsurancePolicyNumber { get; init; }
    public required Guid OwnerBranchId { get; init; }
    public Guid? CurrentBranchId { get; init; }
    public int CurrentKm { get; init; }
    public decimal? PurchasePrice { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
