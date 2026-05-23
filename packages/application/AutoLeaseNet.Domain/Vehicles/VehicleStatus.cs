namespace AutoLeaseNet.Domain.Vehicles;

public enum VehicleStatus
{
    Available = 1,
    Reserved = 2,
    OnRent = 3,
    InService = 4,
    Damaged = 5,
    Sold = 6,
    Disposed = 7,
}

public enum FuelType
{
    Petrol91 = 1,
    Petrol95 = 2,
    Diesel = 3,
    Hybrid = 4,
    Electric = 5,
}

public enum TransmissionType
{
    Automatic = 1,
    Manual = 2,
    CVT = 3,
}

public enum BodyType
{
    Sedan = 1,
    Suv = 2,
    Hatchback = 3,
    Pickup = 4,
    Van = 5,
    Bus = 6,
    Coupe = 7,
}
