namespace AutoLeaseNet.Adapters.Seed;

/// <summary>
/// Tunables for the seeding adapter — bound from <c>Seed</c> config section.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary><c>Demo</c> (BogusDataSeeder) | <c>Empty</c> (no-op) | <c>ImportedFile</c> (future data-management module).</summary>
    public SeedMode Mode { get; init; } = SeedMode.Empty;

    /// <summary>Single tenant id seeded in Week 1 — matches the demo tenant the BFF uses.</summary>
    public Guid TenantId { get; init; } = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");

    /// <summary>Bogus seed for reproducible runs across machines.</summary>
    public int RandomSeed { get; init; } = 20260524;

    /// <summary>Target customer rows to seed (minimum 20).</summary>
    public int CustomerCount { get; init; } = 200;

    /// <summary>Target vehicle rows to seed (minimum 60).</summary>
    public int VehicleCount { get; init; } = 250;

    /// <summary>Target driver rows to seed (minimum 80).</summary>
    public int DriverCount { get; init; } = 300;

    /// <summary>Target lease rows to seed (minimum 10).</summary>
    public int LeaseCount { get; init; } = 120;
}

public enum SeedMode
{
    Empty = 0,
    Demo = 1,
    ImportedFile = 2,
}
