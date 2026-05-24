using AutoLeaseNet.Domain.Shared;
using MediatR;

namespace AutoLeaseNet.Application.Notifications;

/// <summary>
/// Generic MediatR <see cref="INotification"/> wrapper around any <see cref="IDomainEvent"/>.
/// Keeps the domain layer free of MediatR while still letting application-layer handlers
/// subscribe per concrete event type via <c>INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;</c>.
///
/// <para>
/// Published from <c>DomainEventDispatchInterceptor</c> after every successful
/// <c>SaveChangesAsync</c>; subscribers run post-commit so they observe the persisted state.
/// </para>
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;
