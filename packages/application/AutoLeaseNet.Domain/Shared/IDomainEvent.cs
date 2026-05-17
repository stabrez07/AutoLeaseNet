namespace AutoLeaseNet.Domain.Shared;

/// <summary>
/// Marker interface for all domain events.
/// Domain events represent something that happened in the past and is significant to the domain.
/// Per doc 02 §5, every event carries: eventId, eventType, tenantId, aggregateType, aggregateId,
/// occurredAtUtc, actorUserId, correlationId, causationId, payload.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
