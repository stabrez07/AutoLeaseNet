using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Domain.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutoLeaseNet.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core save-changes interceptor that publishes every domain event raised by tracked
/// entities once <c>SaveChangesAsync</c> succeeds. Replaces the hand-rolled scan that
/// previously lived in <c>TajeerWebhookEndpoints.DispatchDomainEventsAsync</c>, so any
/// caller of the DbContext gets transparent dispatch (sagas, dev endpoints, future
/// background workers).
///
/// <para>
/// Publishing happens in <see cref="SavedChangesAsync"/> — i.e. AFTER the DB transaction
/// commits — so handlers observe the persisted state. If a handler throws it is its
/// responsibility to log + swallow (see <c>LeaseIssuedSmsHandler</c>); we deliberately do
/// not roll back the save because the persisted state is already correct.
/// </para>
///
/// <para>
/// Events are snapshotted and cleared from each entity before dispatch so a subsequent
/// <c>SaveChangesAsync</c> (e.g. a UpdatedAtUtc touch) does not re-publish.
/// </para>
/// </summary>
public sealed class DomainEventDispatchInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    private static readonly Type NotificationOpenType = typeof(DomainEventNotification<>);

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is null) return result;

        var entitiesWithEvents = eventData.Context.ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToArray();

        if (entitiesWithEvents.Length == 0) return result;

        foreach (var entity in entitiesWithEvents)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();
            foreach (var domainEvent in events)
            {
                var notificationType = NotificationOpenType.MakeGenericType(domainEvent.GetType());
                var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
                await publisher.Publish(notification, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }
}
