using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoLeaseNet.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> / <c>database update</c>.
/// Lets the EF tools construct the DbContext without booting the full BFF startup pipeline.
///
/// <para>
/// Connection string resolution order: <c>AUTOLEASENET_MIGRATIONS_CONNECTION</c> env var
/// → local-dev fallback (per [[local-dev-infra]] memory — SQL Server 2019 Developer,
/// Windows Integrated Auth, database <c>AutoLeaseNet_Dev</c>).
/// </para>
/// </summary>
public sealed class AutoLeaseNetDbContextFactory : IDesignTimeDbContextFactory<AutoLeaseNetDbContext>
{
    private const string LocalDevConnection =
        "Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";

    public AutoLeaseNetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTOLEASENET_MIGRATIONS_CONNECTION")
            ?? LocalDevConnection;

        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AutoLeaseNetDbContext).Assembly.FullName);
            })
            .Options;

        return new AutoLeaseNetDbContext(options);
    }
}
