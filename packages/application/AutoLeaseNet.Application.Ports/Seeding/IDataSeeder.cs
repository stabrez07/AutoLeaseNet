namespace AutoLeaseNet.Application.Ports.Seeding;

/// <summary>
/// Pattern A port for populating the application database with vendor-shaped demo data
/// on a fresh deployment (per [[feedback-production-ready-data]]). Implementations:
/// <list type="bullet">
///   <item><c>BogusDataSeeder</c> in <c>Adapters.Seed</c> — real-sounding KSA seed.</item>
///   <item><c>EmptyDataSeeder</c> in <c>Adapters.Seed</c> — no-op for non-Demo environments.</item>
/// </list>
/// The composition root picks the implementation via <c>Seed:Mode</c> config
/// (<c>Demo</c> | <c>Empty</c> | <c>ImportedFile</c>). The future data-management module
/// replaces the seeder without code change — same port, different adapter.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Tenant id this seeder populates.</summary>
    Guid TenantId { get; }

    /// <summary>
    /// Idempotent populate. Implementations must check whether seed data already exists
    /// (typically <c>ICustomerRepository.AnyAsync</c>) and short-circuit if so.
    /// </summary>
    Task SeedAsync(CancellationToken ct);
}
