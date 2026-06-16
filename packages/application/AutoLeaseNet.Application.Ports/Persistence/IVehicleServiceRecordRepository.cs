using AutoLeaseNet.Domain.Vehicles;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IVehicleServiceRecordRepository
{
    void Add(VehicleServiceRecord record);
    Task<IReadOnlyList<VehicleServiceRecord>> GetByVehicleAsync(Guid tenantId, Guid vehicleId, CancellationToken ct);
}
