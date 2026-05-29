using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Infrastructure.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OutboxOptions"/>, <see cref="IOutboxRepository"/>, and the
    /// <see cref="OutboxDrainService"/> hosted service. The capture-side interceptor
    /// (<c>OutboxWriteInterceptor</c>) is registered separately by
    /// <c>AddAutoLeaseNetDbContext</c>; this method covers everything else.
    /// </summary>
    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.Configure<OutboxOptions>(section);
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        services.AddHostedService<OutboxDrainService>();
        return services;
    }
}
