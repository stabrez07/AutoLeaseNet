using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Vehicles;

public enum ServiceRecordType
{
    PMS = 1,
    CMS = 2,
}

/// <summary>
/// Persistent service history record for a vehicle.
/// PMS = Preventive Maintenance Service (scheduled).
/// CMS = Corrective Maintenance Service (unscheduled/repair).
/// </summary>
public sealed class VehicleServiceRecord : Entity
{
    public Guid VehicleId { get; private set; }
    public ServiceRecordType Type { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly ServicedAt { get; private set; }
    public int OdometerAtService { get; private set; }
    public decimal CostSar { get; private set; }
    public string Branch { get; private set; } = string.Empty;
    public string Technician { get; private set; } = string.Empty;
    /// <summary>Pipe-delimited list of replaced part names, e.g. "Oil Filter|Air Filter".</summary>
    public string PartsReplacedRaw { get; private set; } = string.Empty;
    public int? NextServiceOdometer { get; private set; }
    public DateOnly? NextServiceDate { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<string> PartsReplaced =>
        string.IsNullOrWhiteSpace(PartsReplacedRaw)
            ? Array.Empty<string>()
            : PartsReplacedRaw.Split('|', StringSplitOptions.RemoveEmptyEntries);

    private VehicleServiceRecord() { }

    public static VehicleServiceRecord Create(VehicleServiceRecordInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ServiceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Technician);
        ArgumentOutOfRangeException.ThrowIfNegative(input.OdometerAtService);
        ArgumentOutOfRangeException.ThrowIfNegative((double)input.CostSar);

        return new VehicleServiceRecord
        {
            TenantId = input.TenantId,
            VehicleId = input.VehicleId,
            Type = input.Type,
            ServiceCode = input.ServiceCode.ToUpperInvariant(),
            Description = input.Description,
            ServicedAt = input.ServicedAt,
            OdometerAtService = input.OdometerAtService,
            CostSar = input.CostSar,
            Branch = input.Branch,
            Technician = input.Technician,
            PartsReplacedRaw = string.Join('|', (input.PartsReplaced ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))),
            NextServiceOdometer = input.NextServiceOdometer,
            NextServiceDate = input.NextServiceDate,
            Notes = input.Notes,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }
}

public sealed record VehicleServiceRecordInput
{
    public required Guid TenantId { get; init; }
    public required Guid VehicleId { get; init; }
    public required ServiceRecordType Type { get; init; }
    public required string ServiceCode { get; init; }
    public required string Description { get; init; }
    public required DateOnly ServicedAt { get; init; }
    public required int OdometerAtService { get; init; }
    public decimal CostSar { get; init; }
    public required string Branch { get; init; }
    public required string Technician { get; init; }
    public IEnumerable<string>? PartsReplaced { get; init; }
    public int? NextServiceOdometer { get; init; }
    public DateOnly? NextServiceDate { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
