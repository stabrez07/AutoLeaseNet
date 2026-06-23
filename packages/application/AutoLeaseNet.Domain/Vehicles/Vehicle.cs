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

    // ─── Allocation (links vehicle to customer + contract for fleet ops) ────
    public Guid? AllocatedToCustomerId { get; private set; }
    public Guid? AllocatedToContractId { get; private set; }

    public void AllocateToContract(Guid customerId, Guid contractId, DateTimeOffset nowUtc)
    {
        AllocatedToCustomerId = customerId;
        AllocatedToContractId = contractId;
        UpdatedAtUtc = nowUtc;
    }

    public void Deallocate(DateTimeOffset nowUtc)
    {
        AllocatedToCustomerId = null;
        AllocatedToContractId = null;
        UpdatedAtUtc = nowUtc;
    }

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

    public void ReleaseReservation(DateTimeOffset nowUtc)
    {
        if (Status == VehicleStatus.Available) return;
        if (Status != VehicleStatus.Reserved)
            throw new InvalidOperationException($"Vehicle {Id} must be Reserved to release reservation (was {Status}).");
        Status = VehicleStatus.Available;
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

    public void Sell(DateTimeOffset nowUtc)
    {
        if (Status == VehicleStatus.OnRent)
            throw new InvalidOperationException($"Vehicle {Id} cannot be sold while on active rental.");
        Status = VehicleStatus.Sold;
        UpdatedAtUtc = nowUtc;
    }

    public void Dispose(DateTimeOffset nowUtc)
    {
        if (Status == VehicleStatus.OnRent)
            throw new InvalidOperationException($"Vehicle {Id} cannot be disposed while on active rental.");
        Status = VehicleStatus.Disposed;
        UpdatedAtUtc = nowUtc;
    }

    public void TransferBranch(Guid newBranchId, DateTimeOffset nowUtc)
    {
        if (newBranchId == Guid.Empty) throw new ArgumentException("BranchId required.", nameof(newBranchId));
        if (Status == VehicleStatus.OnRent)
            throw new InvalidOperationException($"Vehicle {Id} cannot be transferred while on active rental.");
        CurrentBranchId = newBranchId;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateOdometer(int newKm, DateTimeOffset nowUtc)
    {
        if (newKm < CurrentKm)
            throw new ArgumentException($"New odometer {newKm} is less than current {CurrentKm}.", nameof(newKm));
        CurrentKm = newKm;
        UpdatedAtUtc = nowUtc;
    }

    public void RecordServiceCompletion(DateOnly servicedAt, int odometerAtService, int? nextServiceKm, DateOnly? nextServiceDate, DateTimeOffset nowUtc)
    {
        LastServiceKm = odometerAtService;
        LastServiceDate = servicedAt;
        NextServiceDueKm = nextServiceKm;
        NextServiceDueDate = nextServiceDate;
        if (odometerAtService > CurrentKm) CurrentKm = odometerAtService;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(VehicleUpdateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Color is not null) Color = input.Color;
        if (input.Seats.HasValue) Seats = input.Seats.Value;
        if (input.Make is not null) Make = input.Make;
        if (input.Model is not null) Model = input.Model;
        if (input.ModelYear.HasValue) ModelYear = input.ModelYear.Value;
        if (input.InsuranceCompany is not null) InsuranceCompany = input.InsuranceCompany;
        if (input.InsurancePolicyNumber is not null) InsurancePolicyNumber = input.InsurancePolicyNumber;
        if (input.LicenseExpiryDate.HasValue) LicenseExpiryDate = input.LicenseExpiryDate;
        if (input.InsuranceExpiryDate.HasValue) InsuranceExpiryDate = input.InsuranceExpiryDate;
        if (input.InspectionExpiryDate.HasValue) InspectionExpiryDate = input.InspectionExpiryDate;
        if (input.CurrentBranchId.HasValue) CurrentBranchId = input.CurrentBranchId.Value;
        if (input.CurrentKm.HasValue && input.CurrentKm.Value >= CurrentKm) CurrentKm = input.CurrentKm.Value;
        if (input.PurchasePrice.HasValue) PurchasePrice = input.PurchasePrice;
        if (input.PurchaseDate.HasValue) PurchaseDate = input.PurchaseDate;
        if (input.Notes is not null) Notes = input.Notes;
        UpdatedAtUtc = input.NowUtc;
        UpdatedBy = input.UpdatedBy;
    }
}

public sealed record VehicleUpdateInput
{
    public string? Color { get; init; }
    public int? Seats { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public int? ModelYear { get; init; }
    public string? InsuranceCompany { get; init; }
    public string? InsurancePolicyNumber { get; init; }
    public DateOnly? LicenseExpiryDate { get; init; }
    public DateOnly? InsuranceExpiryDate { get; init; }
    public DateOnly? InspectionExpiryDate { get; init; }
    public Guid? CurrentBranchId { get; init; }
    public int? CurrentKm { get; init; }
    public decimal? PurchasePrice { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
    public required Guid UpdatedBy { get; init; }
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
