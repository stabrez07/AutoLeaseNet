using AutoLeaseNet.Domain.Vehicles;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IVehicleRepository
{
    void Add(Vehicle vehicle);
    Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Vehicle?> GetByPlateAsync(Guid tenantId, string plateNumber, string plateLetters, CancellationToken ct);
    Task<Vehicle?> FindAvailableReplacementAsync(
        Guid tenantId,
        Guid excludedVehicleId,
        Guid preferredBranchId,
        BodyType bodyType,
        int seats,
        CancellationToken ct);
}
