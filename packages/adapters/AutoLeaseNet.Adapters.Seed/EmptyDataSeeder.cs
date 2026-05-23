using AutoLeaseNet.Application.Ports.Seeding;

namespace AutoLeaseNet.Adapters.Seed;

/// <summary>No-op seeder used in non-Demo modes (Staging / Production).</summary>
public sealed class EmptyDataSeeder(SeedOptions options) : IDataSeeder
{
    public Guid TenantId { get; } = options.TenantId;

    public Task SeedAsync(CancellationToken ct) => Task.CompletedTask;
}
