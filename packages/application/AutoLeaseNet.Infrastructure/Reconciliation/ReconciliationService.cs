using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Infrastructure.Reconciliation;

/// <summary>
/// Periodic scheduler for <see cref="IReconciliationCheck"/> implementations.
/// Mirror of the <c>OutboxDrainService</c> shape: cooperative cancellation,
/// per-cycle DI scope, per-check try/catch so one failing check doesn't take
/// down the whole cycle, infrastructure-exception swallowed at the loop level.
///
/// <para>Single-instance Phase 1; multi-instance Phase 2 needs a distributed
/// lock (Redis or SQL lease) so two replicas don't double-run the same check.</para>
/// </summary>
public sealed partial class ReconciliationService(
    IServiceProvider services,
    IOptions<ReconciliationOptions> options,
    ILogger<ReconciliationService> logger) : BackgroundService
{
    private static readonly Random JitterRng = Random.Shared;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            LogDisabled();
            return;
        }

        LogStarted(opts.IntervalSeconds, opts.JitterSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jitterMs = opts.JitterSeconds > 0 ? JitterRng.Next(0, opts.JitterSeconds * 1000) : 0;
                var delay = TimeSpan.FromSeconds(opts.IntervalSeconds) + TimeSpan.FromMilliseconds(jitterMs);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailure(ex);
            }
        }

        LogStopped();
    }

    /// <summary>Visible for testing — runs exactly one cycle of all registered checks.</summary>
    internal async Task RunCycleAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var checks = scope.ServiceProvider.GetServices<IReconciliationCheck>().ToArray();

        if (checks.Length == 0)
        {
            LogNoChecksRegistered();
            return;
        }

        var errors = 0;
        foreach (var check in checks)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await check.RunAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                LogCheckFailure(ex, check.Name);
            }
        }

        LogCycleCompleted(checks.Length, errors);
    }

    [LoggerMessage(EventId = 4301, Level = LogLevel.Information,
        Message = "ReconciliationService starting (intervalSec={IntervalSeconds}, jitterSec={JitterSeconds}).")]
    private partial void LogStarted(int intervalSeconds, int jitterSeconds);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Information,
        Message = "ReconciliationService disabled via Reconciliation:Enabled=false; nothing will run.")]
    private partial void LogDisabled();

    [LoggerMessage(EventId = 4303, Level = LogLevel.Information,
        Message = "ReconciliationService stopping.")]
    private partial void LogStopped();

    [LoggerMessage(EventId = 4304, Level = LogLevel.Information,
        Message = "Reconciliation cycle ran ({CheckCount} check(s), {ErrorCount} error(s)).")]
    private partial void LogCycleCompleted(int checkCount, int errorCount);

    [LoggerMessage(EventId = 4305, Level = LogLevel.Warning,
        Message = "Reconciliation check {CheckName} threw; cycle continues.")]
    private partial void LogCheckFailure(Exception ex, string checkName);

    [LoggerMessage(EventId = 4306, Level = LogLevel.Error,
        Message = "Reconciliation cycle failed with infrastructure error; loop continues.")]
    private partial void LogCycleFailure(Exception ex);

    [LoggerMessage(EventId = 4307, Level = LogLevel.Debug,
        Message = "Reconciliation cycle ran with no checks registered.")]
    private partial void LogNoChecksRegistered();
}
