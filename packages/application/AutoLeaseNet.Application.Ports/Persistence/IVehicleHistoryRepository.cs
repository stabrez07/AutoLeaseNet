using AutoLeaseNet.Domain.Vehicles;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IVehicleHistoryRepository
{
    void Add(VehicleHistoryEvent historyEvent);
    Task<IReadOnlyList<VehicleHistoryEvent>> GetByVehicleAsync(Guid tenantId, Guid vehicleId, CancellationToken ct);
}
