using AutoLeaseNet.Infrastructure;
using AutoLeaseNet.Infrastructure.Persistence;
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
}
