using AutoLeaseNet.Domain.Leases;
using MediatR;

namespace AutoLeaseNet.Application.Leases.Notifications;

/// <summary>
/// MediatR <see cref="INotification"/> wrapper for <see cref="LeaseIssuedDomainEvent"/>.
/// The wrapper keeps Domain free of any MediatR dependency — the BFF webhook handler
/// scans <c>Lease.DomainEvents</c> after <c>SaveChangesAsync</c> and publishes wrappers
/// for each known event type.
/// </summary>
public sealed record LeaseIssuedNotification(LeaseIssuedDomainEvent Event) : INotification;
