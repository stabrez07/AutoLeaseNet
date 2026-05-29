using System.Text.Json;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Outbox;
using AutoLeaseNet.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Replaces the previous inline <c>DomainEventDispatchInterceptor</c>. Runs in
/// <see cref="SaveChangesInterceptor.SavingChangesAsync"/> — i.e. BEFORE the
/// transaction commits — so the captured <see cref="OutboxEvent"/> rows
/// participate in the same UoW as the business change. If the business
/// <c>SaveChangesAsync</c> rolls back, the outbox rows roll back too. If it
/// commits, both succeed together. That atomicity is the whole point.
///
/// <para>
/// Asynchronous dispatch happens in <c>OutboxDrainService</c>; this interceptor
/// never invokes <c>IPublisher.Publish</c> itself. Replay of in-flight rows
/// resolves the event type via <c>Type.GetType(EventType, throwOnError: true)</c>
/// using assembly-qualified names recorded here.
/// </para>
/// </summary>
public sealed partial class OutboxWriteInterceptor(IClock clock, ILogger<OutboxWriteInterceptor> logger)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is null) return ValueTask.FromResult(result);

        var entitiesWithEvents = eventData.Context.ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToArray();

        if (entitiesWithEvents.Length == 0) return ValueTask.FromResult(result);

        var nowUtc = clock.UtcNow;
        var outboxSet = eventData.Context.Set<OutboxEvent>();
        var captured = 0;

        foreach (var entity in entitiesWithEvents)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();

            foreach (var domainEvent in events)
            {
                var (tenantId, eventType, payloadJson) = SerializeForOutbox(entity, domainEvent);
                if (tenantId == Guid.Empty)
                {
                    LogSkippedNoTenant(eventType, domainEvent.EventId);
                    continue;
                }

                outboxSet.Add(OutboxEvent.Capture(
                    tenantId: tenantId,
                    eventType: eventType,
                    payloadJson: payloadJson,
                    correlationId: null,
                    nowUtc: nowUtc));
                captured++;
            }
        }

        if (captured > 0) LogCaptured(captured);
        return ValueTask.FromResult(result);
    }

    private static (Guid TenantId, string EventType, string PayloadJson) SerializeForOutbox(
        Entity raisingEntity,
        IDomainEvent domainEvent)
    {
        var type = domainEvent.GetType();
        // Assembly-qualified without version/culture/key so future rebuilds resolve cleanly.
        var typeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
        var json = JsonSerializer.Serialize(domainEvent, type, JsonOpts);

        // Tenancy resolution: prefer a TenantId property on the event itself (every
        // current domain event carries one); fall back to the raising entity's TenantId.
        var tenantProp = type.GetProperty("TenantId");
        var tenantFromEvent = tenantProp?.GetValue(domainEvent) as Guid?;
        var tenantId = tenantFromEvent.GetValueOrDefault();
        if (tenantId == Guid.Empty) tenantId = raisingEntity.TenantId;

        return (tenantId, typeName, json);
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Debug,
        Message = "OutboxWriteInterceptor captured {Count} domain event(s) into OutboxEvents.")]
    private partial void LogCaptured(int count);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning,
        Message = "Domain event {EventType} (id {EventId}) had no resolvable TenantId; skipping outbox capture.")]
    private partial void LogSkippedNoTenant(string eventType, Guid eventId);
}
