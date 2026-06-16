using AutoLeaseNet.Domain.Vehicles;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IVehicleImageRepository
{
    void Add(VehicleImage image);
    Task<IReadOnlyList<VehicleImage>> GetByVehicleAsync(Guid tenantId, Guid vehicleId, CancellationToken ct);
}
