using System.Text.Json;
using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Outbox;
using AutoLeaseNet.Domain.Shared;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Infrastructure.Outbox;

/// <summary>
/// Drains <see cref="OutboxEvent"/> rows by publishing each via MediatR (wrapped in
/// <see cref="DomainEventNotification{TEvent}"/>) and marking them processed. Runs
/// at <c>OutboxOptions.DrainIntervalSeconds</c> when idle; on a non-empty batch
/// immediately loops again so a burst drains without artificial latency.
///
/// <para>The drain runs cross-tenant under <see cref="SystemTenancyScope"/> so the
/// repository query (and any handler-side queries) can see all tenants' rows.
/// <c>OutboxEvents</c> itself is not RLS-protected, but downstream handlers may
/// query tenant-scoped tables (e.g. <c>LeaseIssuedSmsHandler</c> reads
/// <c>Customers</c>) — the per-row scope below provides the right tenancy for each
/// publish.</para>
///
/// <para>Single-instance Phase 1. Multi-instance Phase 2 needs a distributed lock
/// (Redis or <c>WITH (UPDLOCK, READPAST)</c>) to prevent double-publish.</para>
/// </summary>
public sealed partial class OutboxDrainService(
    IServiceProvider services,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDrainService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Type NotificationOpenType = typeof(DomainEventNotification<>);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            LogDisabled();
            return;
        }

        LogStarted(opts.DrainIntervalSeconds, opts.BatchSize, opts.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await DrainOnceAsync(opts, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Don't let an infrastructure exception (e.g. SQL connection blip) kill the loop.
                LogUnexpectedFailure(ex);
                processed = 0;
            }

            if (processed == 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(opts.DrainIntervalSeconds), stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        LogStopped();
    }

    /// <summary>Visible for testing. Returns rows processed (success OR failed) this cycle.</summary>
    internal async Task<int> DrainOnceAsync(OutboxOptions opts, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IOutboxRepository>();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var publisher = sp.GetRequiredService<IPublisher>();
        var clock = sp.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var due = await repo.GetDueAsync(now, opts.BatchSize, opts.MaxAttempts, ct)
            .ConfigureAwait(false);

        if (due.Count == 0) return 0;

        foreach (var row in due)
        {
            using var tenantScope = SystemTenancyScope.For(row.TenantId);
            try
            {
                var notification = MaterializeNotification(row);
                await publisher.Publish(notification, ct).ConfigureAwait(false);
                row.MarkProcessed(clock.UtcNow);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var backoff = ComputeBackoff(row.Attempts + 1);
                row.MarkFailed(ex.Message, backoff, clock.UtcNow);
                LogPublishFailed(ex, row.Id, row.EventType, row.Attempts, backoff.TotalSeconds);
            }
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        LogDrained(due.Count);
        return due.Count;
    }

    private static INotification MaterializeNotification(OutboxEvent row)
    {
        var eventType = Type.GetType(row.EventType, throwOnError: true)
            ?? throw new InvalidOperationException($"Outbox event type '{row.EventType}' could not be resolved.");
        if (!typeof(IDomainEvent).IsAssignableFrom(eventType))
        {
            throw new InvalidOperationException($"Outbox event type '{row.EventType}' is not an IDomainEvent.");
        }

        var deserialized = JsonSerializer.Deserialize(row.PayloadJson, eventType, JsonOpts)
            ?? throw new InvalidOperationException($"Outbox event {row.Id} payload deserialized to null.");

        var notificationType = NotificationOpenType.MakeGenericType(eventType);
        return (INotification)Activator.CreateInstance(notificationType, deserialized)!;
    }

    /// <summary>Exponential backoff capped at 60s. 1→1s, 2→2s, 3→4s, 4→8s, 5→16s.</summary>
    internal static TimeSpan ComputeBackoff(int attempts)
    {
        if (attempts <= 0) return TimeSpan.Zero;
        var raw = Math.Pow(2, attempts - 1);
        var capped = Math.Min(raw, 60);
        return TimeSpan.FromSeconds(capped);
    }

    [LoggerMessage(EventId = 4201, Level = LogLevel.Information,
        Message = "OutboxDrainService starting (intervalSec={IntervalSeconds}, batchSize={BatchSize}, maxAttempts={MaxAttempts}).")]
    private partial void LogStarted(int intervalSeconds, int batchSize, int maxAttempts);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Information,
        Message = "OutboxDrainService disabled via Outbox:Enabled=false; nothing will drain.")]
    private partial void LogDisabled();

    [LoggerMessage(EventId = 4203, Level = LogLevel.Information,
        Message = "OutboxDrainService stopping.")]
    private partial void LogStopped();

    [LoggerMessage(EventId = 4204, Level = LogLevel.Debug,
        Message = "OutboxDrainService processed {Count} row(s) in this cycle.")]
    private partial void LogDrained(int count);

    [LoggerMessage(EventId = 4205, Level = LogLevel.Warning,
        Message = "OutboxDrainService publish failed for row {RowId} ({EventType}); attempt {Attempt} — next try in {NextBackoffSeconds}s.")]
    private partial void LogPublishFailed(Exception ex, Guid rowId, string eventType, int attempt, double nextBackoffSeconds);

    [LoggerMessage(EventId = 4206, Level = LogLevel.Error,
        Message = "OutboxDrainService cycle failed with infrastructure error; loop continues.")]
    private partial void LogUnexpectedFailure(Exception ex);
}
