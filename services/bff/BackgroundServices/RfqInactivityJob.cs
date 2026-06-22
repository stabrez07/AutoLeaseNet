using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.BackgroundServices;

public sealed partial class RfqInactivityJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RfqInactivityJob> logger) : BackgroundService
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (Exception ex) { LogJobFailed(ex); }
            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var now = DateTimeOffset.UtcNow;

        var stale = await db.Rfqs
            .Where(r => r.Stage != RfqStage.Won && r.Stage != RfqStage.Lost && r.UpdatedAtUtc < cutoff)
            .ToListAsync(ct);

        foreach (var r in stale)
            r.MarkLost("Auto-closed: 90-day inactivity", SystemUserId, now);

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            LogClosed(stale.Count);
        }
    }

    [LoggerMessage(EventId = 9711, Level = LogLevel.Error, Message = "RfqInactivityJob failed")]
    partial void LogJobFailed(Exception ex);

    [LoggerMessage(EventId = 9712, Level = LogLevel.Information, Message = "RfqInactivityJob: closed {Count} stale RFQ(s)")]
    partial void LogClosed(int count);
}
