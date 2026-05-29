using AutoLeaseNet.Application.Ports.Seeding;
using AutoLeaseNet.Infrastructure;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoLeaseNet.Bff.Tests.Support;

/// <summary>
/// One source of truth for BFF <c>WebApplicationFactory&lt;Program&gt;</c> test
/// hosts. Replaces the ~30-line `ConfigureAppConfiguration` + DbContext swap
/// dance each factory used to copy verbatim. Factories layer per-test overrides
/// on top of the dictionaries returned here.
///
/// <para>
/// Why this exists: five consecutive workstream retros (Outbox, Reconciliation,
/// customer-portal scaffold, Tajeer GetAsync, My Vehicles) flagged the
/// copy-paste as overdue technical debt. Centralising makes the per-factory
/// surface focus on what's actually different per scenario (seed mode, webhook
/// secret, outbox toggle) instead of restating the always-shared defaults.
/// </para>
/// </summary>
public static class BffTestHostDefaults
{
    /// <summary>
    /// Connection string sentinel — every test host swaps the DbContext to
    /// InMemory, so this value is never actually opened. Kept non-empty so
    /// any code path that reads the configuration for shape gets a string.
    /// </summary>
    public const string PlaceholderConnectionString = "Server=replaced-by-in-memory;Database=ignored;";

    /// <summary>Default webhook shared secret used by every host except the webhook tests themselves.</summary>
    public const string DefaultWebhookSharedSecret = "test-secret";

    /// <summary>
    /// Always-shared in-memory config: SQL connection sentinel, Tajeer dev placeholders,
    /// Tajeer:Mode=InMemory, Outbox/Reconciliation disabled, Seed:Mode=Empty. Callers
    /// mutate the returned dictionary in-place to layer their per-factory overrides
    /// (e.g. flip Seed:Mode to Demo, change the webhook secret).
    /// </summary>
    public static Dictionary<string, string?> Defaults()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:AutoLeaseNet"] = PlaceholderConnectionString,
            ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
            ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
            ["Tajeer:AppId"] = "test-app",
            ["Tajeer:AppKey"] = "test-key",
            ["Tajeer:AuthorizationToken"] = "Basic test",
            ["Tajeer:BranchId"] = "1",
            ["Tajeer:TimeoutSeconds"] = "10",
            ["Tajeer:WebhookSharedSecret"] = DefaultWebhookSharedSecret,
            ["Tajeer:Mode"] = "InMemory",
            // ZATCA Phase-1 — every BFF test host runs against the in-memory fake.
            // Required fields per [ZatcaOptions] data annotations: BaseUrl + AuthorizationToken.
            ["Zatca:BaseUrl"] = "https://gw-fatoora-sandbox.example/clearance",
            ["Zatca:Environment"] = "Sandbox",
            ["Zatca:AuthorizationToken"] = "Bearer test",
            ["Zatca:Mode"] = "InMemory",
            ["Outbox:Enabled"] = "false",
            ["Reconciliation:Enabled"] = "false",
            ["Seed:Mode"] = "Empty",
        };
    }

    /// <summary>
    /// Convenience: <see cref="Defaults"/> plus the three keys needed to drive the
    /// Bogus demo seeder (Mode=Demo, the per-factory tenant id, deterministic random
    /// seed). For test hosts that want pre-seeded fixture data via the demo path.
    /// </summary>
    public static Dictionary<string, string?> DemoSeedDefaults(Guid tenantId, string randomSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(randomSeed);
        var d = Defaults();
        d["Seed:Mode"] = "Demo";
        d["Seed:TenantId"] = tenantId.ToString();
        d["Seed:RandomSeed"] = randomSeed;
        return d;
    }

    /// <summary>
    /// Standard <c>DbContextOptions&lt;AutoLeaseNetDbContext&gt;</c> swap to EF Core
    /// InMemory used by every factory. Equivalent to the
    /// <c>RemoveAll&lt;DbContextOptions&gt; + AddAutoLeaseNetDbContext(InMemory)</c>
    /// pair each factory used to copy.
    /// </summary>
    public static void ReplaceDbContextWithInMemory(IServiceCollection services, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>();
        services.AddAutoLeaseNetDbContext(opt => opt.UseInMemoryDatabase(databaseName));
    }

    /// <summary>Default demo-seed deadline. Long enough to ride out contended runners; short enough that a real hang fails the test rather than the suite timeout.</summary>
    public static readonly TimeSpan DefaultSeedTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Boots the test host, runs the configured <see cref="IDataSeeder"/>, and polls until
    /// <paramref name="readinessCheck"/> succeeds. Used by every demo-seeded factory so the
    /// 15-line "create probe client → resolve seeder → SeedAsync → deadline loop" dance lives
    /// in one place.
    ///
    /// <para>
    /// <paramref name="entityName"/> is for the timeout error message — pass the table /
    /// concept the check is waiting on (e.g. <c>"Customers"</c>, <c>"Active Lease"</c>) so the
    /// failure clearly points at what didn't materialise.
    /// </para>
    ///
    /// <para>
    /// <paramref name="buildTimeoutDetail"/> is an opt-in escape hatch for factories that want
    /// to enrich the timeout message with diagnostic snapshot (e.g. configured Seed:Mode,
    /// row counts, db name). Runs once if the readiness check never succeeds; the returned
    /// string is appended to the standard error.
    /// </para>
    /// </summary>
    public static async Task EnsureDemoSeededAsync(
        WebApplicationFactory<Program> factory,
        Func<AutoLeaseNetDbContext, Task<bool>> readinessCheck,
        string entityName,
        TimeSpan? timeout = null,
        Func<AutoLeaseNetDbContext, IServiceProvider, Task<string>>? buildTimeoutDetail = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(readinessCheck);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        // Probe client forces the host to build so Services + the Development startup
        // hook (which itself awaits seeder.SeedAsync) have run by the time we resolve.
        using var probe = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var effectiveTimeout = timeout ?? DefaultSeedTimeout;
        var deadline = DateTime.UtcNow.Add(effectiveTimeout);
        while (DateTime.UtcNow < deadline)
        {
            if (await readinessCheck(db)) return;
            await Task.Delay(100);
        }

        var baseMessage = $"Seeder did not produce '{entityName}' within {effectiveTimeout.TotalSeconds:0}s.";
        if (buildTimeoutDetail is not null)
        {
            var detail = await buildTimeoutDetail(db, scope.ServiceProvider);
            throw new InvalidOperationException($"{baseMessage} {detail}");
        }
        throw new InvalidOperationException(baseMessage);
    }
}
