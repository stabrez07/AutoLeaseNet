using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Vehicles;

public enum VehicleHistoryEventType
{
    StatusChanged = 1,
    ServiceRecorded = 2,
    DriverAssigned = 3,
    DriverUnassigned = 4,
    OdometerUpdated = 5,
    BranchTransferred = 6,
    NoteAdded = 7,
    ImageAdded = 8,
    FieldsUpdated = 9,
    BulkImported = 10,
}

/// <summary>
/// Append-only audit record attached to a Vehicle.  One row per event.
/// Never update or delete rows — this is the vehicle's chain of custody.
/// </summary>
public sealed class VehicleHistoryEvent : Entity
{
    public Guid VehicleId { get; private set; }
    public VehicleHistoryEventType EventType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }
    public string PerformedByName { get; private set; } = "System";

    private VehicleHistoryEvent() { }

    public static VehicleHistoryEvent Create(
        Guid tenantId,
        Guid vehicleId,
        VehicleHistoryEventType eventType,
        string description,
        DateTimeOffset nowUtc,
        string? previousValue = null,
        string? newValue = null,
        string? performedByName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new VehicleHistoryEvent
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            EventType = eventType,
            Description = description,
            PreviousValue = previousValue,
            NewValue = newValue,
            PerformedByName = performedByName ?? "Fleet Ops",
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
