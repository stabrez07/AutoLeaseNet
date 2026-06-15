using AutoLeaseNet.Application.Ports.Sales;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Sales;

/// <summary>
/// Generates <c>Q-{yyyyMMdd}-{n:D4}</c> numbers by counting existing quotations for that
/// tenant on that day. Not globally unique under extreme concurrency — Phase 1 is
/// single-tenant with low quote volume; a DB sequence replaces this in Phase 2.
/// </summary>
internal sealed class SequentialQuoteNumberGenerator(AutoLeaseNetDbContext db, IClock clock) : IQuoteNumberGenerator
{
    public async Task<string> GenerateAsync(Guid tenantId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var prefix = $"Q-{today:yyyyMMdd}-";

        var count = await db.Quotations
            .CountAsync(q => q.TenantId == tenantId && q.QuoteNumber.StartsWith(prefix), ct)
            .ConfigureAwait(false);

        return $"{prefix}{(count + 1):D4}";
    }
}
