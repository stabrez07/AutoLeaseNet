namespace AutoLeaseNet.Domain.Operations;

/// <summary>
/// Tajeer's discrete fuel-level lookup (Spec 01 §5.6 — column <c>FuelLevel TINYINT</c>).
/// Values match the wire-protocol codes so the adapter passes them through unchanged.
/// </summary>
public enum FuelLevel : byte
{
    Full = 1,
    ThreeQuarter = 2,
    Half = 3,
    Quarter = 4,
    Empty = 5,
}
