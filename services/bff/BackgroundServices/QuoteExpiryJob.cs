using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.BackgroundServices;

public sealed partial class QuoteExpiryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuoteExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (Exception ex) { LogJobFailed(ex); }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var expired = await db.Quotations
            .Where(q => q.Status == QuotationStatus.SentToCustomer && q.ValidUntilDate < today)
            .ToListAsync(ct);

        foreach (var q in expired) q.MarkExpired(now);

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            LogExpired(expired.Count);
        }
    }

    [LoggerMessage(EventId = 9701, Level = LogLevel.Error, Message = "QuoteExpiryJob failed")]
    partial void LogJobFailed(Exception ex);

    [LoggerMessage(EventId = 9702, Level = LogLevel.Information, Message = "QuoteExpiryJob: marked {Count} quotation(s) as expired")]
    partial void LogExpired(int count);
}
