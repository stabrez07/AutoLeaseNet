using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Infrastructure.Persistence;

namespace AutoLeaseNet.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services: DbContext, UnitOfWork, IClock, repositories.
    /// Called from the BFF composition root.
    /// </summary>
    public static IServiceCollection AddAutoLeaseNetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AutoLeaseNet")
            ?? throw new InvalidOperationException("Missing connection string 'AutoLeaseNet'");

        services.AddDbContext<AutoLeaseNetDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(2), errorNumbersToAdd: null);
                sql.MigrationsAssembly(typeof(AutoLeaseNetDbContext).Assembly.FullName);
            }));

        services.AddScoped<IUnitOfWork>(sp => new EfUnitOfWork(sp.GetRequiredService<AutoLeaseNetDbContext>()));
        services.AddSingleton<IClock, SystemClock>();

        // Repository registrations will be added per aggregate as Phase 1 features land.

        return services;
    }

    private sealed class EfUnitOfWork(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }
}
