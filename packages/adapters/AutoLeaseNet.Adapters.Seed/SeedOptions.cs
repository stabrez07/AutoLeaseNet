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
}

public enum SeedMode
{
    Empty = 0,
    Demo = 1,
    ImportedFile = 2,
}
