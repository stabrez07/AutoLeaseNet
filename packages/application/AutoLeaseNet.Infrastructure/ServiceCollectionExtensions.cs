using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Sales;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Interceptors;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using AutoLeaseNet.Infrastructure.Sales;

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

        services.AddAutoLeaseNetDbContext(opt => opt.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(2), errorNumbersToAdd: null);
            sql.MigrationsAssembly(typeof(AutoLeaseNetDbContext).Assembly.FullName);
        }));

        services.AddScoped<IUnitOfWork>(sp => new EfUnitOfWork(sp.GetRequiredService<AutoLeaseNetDbContext>()));
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ILeaseRepository, EfLeaseRepository>();
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<IVehicleRepository, EfVehicleRepository>();
        services.AddScoped<IDriverRepository, EfDriverRepository>();
        services.AddScoped<IBranchRepository, EfBranchRepository>();
        services.AddScoped<IRentPolicyRepository, EfRentPolicyRepository>();
        services.AddScoped<IExtendedCoverageRepository, EfExtendedCoverageRepository>();
        services.AddScoped<IWebhookLogRepository, EfWebhookLogRepository>();
        services.AddScoped<IInspectionRepository, EfInspectionRepository>();
        services.AddScoped<IIncidentRepository, EfIncidentRepository>();
        services.AddScoped<IQuotationRepository, EfQuotationRepository>();
        services.AddScoped<IApprovalTierRepository, EfApprovalTierRepository>();
        services.AddScoped<IZatcaChainStateRepository, EfZatcaChainStateRepository>();
        services.AddScoped<IQuoteNumberGenerator, SequentialQuoteNumberGenerator>();

        return services;
    }

    /// <summary>
    /// Single entry-point for wiring <see cref="AutoLeaseNetDbContext"/>. Registers the
    /// DbContext with the caller-supplied provider configuration AND every
    /// AutoLeaseNet-owned EF Core interceptor (today: domain-event dispatch; future:
    /// SESSION_CONTEXT tenancy, audit, soft-delete). Production and test composition
    /// both call through here so a new interceptor lands in one place � preventing
    /// the "test factory forgot to re-bind the interceptor" class of bug
    /// (see Plans/workstreams/2026-05-25-dbcontext-interceptor-domain-events/retrospective.md).
    /// </summary>
    /// <param name="services">The DI container being built.</param>
    /// <param name="configureProvider">
    /// Provider-specific configuration (e.g.
    /// <c>opt =&gt; opt.UseSqlServer(connectionString, ...)</c> for production,
    /// <c>opt =&gt; opt.UseInMemoryDatabase(name)</c> for tests). Runs BEFORE
    /// <c>AddInterceptors</c> so the provider is settled first.
    /// </param>
    public static IServiceCollection AddAutoLeaseNetDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProvider);

        // OutboxWriteInterceptor replaced DomainEventDispatchInterceptor on 2026-05-29
        // (workstream: 2026-05-29-outbox-drain). Capture runs in the same UoW as the
        // business change; async dispatch is now OutboxDrainService's job.
        services.TryAddScoped<OutboxWriteInterceptor>();
        services.TryAddScoped<TenancyConnectionInterceptor>();

        services.AddDbContext<AutoLeaseNetDbContext>((sp, opt) =>
        {
            configureProvider(opt);
            opt.AddInterceptors(
                sp.GetRequiredService<OutboxWriteInterceptor>(),
                sp.GetRequiredService<TenancyConnectionInterceptor>());
        });

        return services;
    }

    private sealed class EfUnitOfWork(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }
}
