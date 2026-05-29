using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Outbox;
using AutoLeaseNet.Infrastructure.Outbox;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Outbox;

/// <summary>
/// Behavioural contract for <see cref="OutboxDrainService"/>. We exercise the public
/// <c>DrainOnceAsync</c> directly (one cycle, deterministic) rather than booting the
/// hosted-service loop — much cheaper, same code path. The drain composes its own
/// scope per cycle, so we build a tiny <see cref="IServiceProvider"/> with just the
/// pieces it resolves: <see cref="IOutboxRepository"/>, <see cref="IUnitOfWork"/>,
/// <see cref="IPublisher"/>, <see cref="IClock"/>.
/// </summary>
public sealed class OutboxDrainServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset NowUtc = new(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DrainOnceAsync_publishes_and_marks_processed_for_every_due_row()
    {
        var publisher = Substitute.For<IPublisher>();
        var drain = NewDrainService(publisher, out var sp);

        await SeedOutboxAsync(sp, count: 3);

        var processed = await drain.DrainOnceAsync(DefaultOptions(), CancellationToken.None);

        processed.Should().Be(3);
        await publisher.Received(3).Publish(
            Arg.Any<DomainEventNotification<LeaseIssuedDomainEvent>>(),
            Arg.Any<CancellationToken>());

        using var verifyScope = sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        (await db.OutboxEvents.CountAsync(o => o.ProcessedAtUtc != null)).Should().Be(3);
    }

    [Fact]
    public async Task DrainOnceAsync_returns_zero_and_does_not_publish_when_no_rows_are_due()
    {
        var publisher = Substitute.For<IPublisher>();
        var drain = NewDrainService(publisher, out _);

        var processed = await drain.DrainOnceAsync(DefaultOptions(), CancellationToken.None);

        processed.Should().Be(0);
        await publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DrainOnceAsync_on_publish_failure_increments_attempts_and_pushes_AvailableAtUtc_out()
    {
        var publisher = Substitute.For<IPublisher>();
        publisher
            .When(p => p.Publish(Arg.Any<DomainEventNotification<LeaseIssuedDomainEvent>>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("SMS provider 503"));

        var drain = NewDrainService(publisher, out var sp);
        await SeedOutboxAsync(sp, count: 1);

        await drain.DrainOnceAsync(DefaultOptions(), CancellationToken.None);

        using var verifyScope = sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var row = await db.OutboxEvents.SingleAsync();
        row.Attempts.Should().Be(1);
        row.ProcessedAtUtc.Should().BeNull();
        row.LastError.Should().Contain("SMS provider 503");
        row.AvailableAtUtc.Should().BeAfter(NowUtc, because: "exponential backoff pushed it into the future");
    }

    [Fact]
    public async Task DrainOnceAsync_skips_rows_parked_beyond_max_attempts()
    {
        var publisher = Substitute.For<IPublisher>();
        var drain = NewDrainService(publisher, out var sp);
        await SeedParkedOutboxAsync(sp, attempts: 5);

        var opts = DefaultOptions();
        opts.MaxAttempts = 5; // exactly equal — repo predicate is Attempts < MaxAttempts → excluded.
        var processed = await drain.DrainOnceAsync(opts, CancellationToken.None);

        processed.Should().Be(0);
        await publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ComputeBackoff_uses_exponential_curve_capped_at_60_seconds()
    {
        OutboxDrainService.ComputeBackoff(0).Should().Be(TimeSpan.Zero);
        OutboxDrainService.ComputeBackoff(1).Should().Be(TimeSpan.FromSeconds(1));
        OutboxDrainService.ComputeBackoff(2).Should().Be(TimeSpan.FromSeconds(2));
        OutboxDrainService.ComputeBackoff(3).Should().Be(TimeSpan.FromSeconds(4));
        OutboxDrainService.ComputeBackoff(5).Should().Be(TimeSpan.FromSeconds(16));
        OutboxDrainService.ComputeBackoff(8).Should().Be(TimeSpan.FromSeconds(60),
            because: "2^7=128 is capped at 60");
        OutboxDrainService.ComputeBackoff(20).Should().Be(TimeSpan.FromSeconds(60));
    }

    // ---------- helpers ----------

    private static OutboxOptions DefaultOptions() => new()
    {
        Enabled = true,
        DrainIntervalSeconds = 5,
        BatchSize = 50,
        MaxAttempts = 5,
    };

    private static OutboxDrainService NewDrainService(IPublisher publisher, out IServiceProvider sp)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddSingleton(publisher);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(NowUtc);
        services.AddSingleton(clock);

        services.AddDbContext<AutoLeaseNetDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        services.AddScoped<IUnitOfWork, TestUnitOfWork>();

        sp = services.BuildServiceProvider();
        var options = Options.Create(DefaultOptions());
        return new OutboxDrainService(sp, options, NullLogger<OutboxDrainService>.Instance);
    }

    private static async Task SeedOutboxAsync(IServiceProvider sp, int count)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        for (var i = 0; i < count; i++)
        {
            var domainEvent = new LeaseIssuedDomainEvent(
                LeaseId: Guid.NewGuid(),
                TenantId: TenantId,
                CustomerId: null,
                TajeerContractNumber: 7000 + i,
                IssuanceUrl: "https://example/issuance",
                IssuedAtUtc: NowUtc.AddSeconds(-1 - i));
            var payload = System.Text.Json.JsonSerializer.Serialize(domainEvent);
            var eventTypeName = $"{typeof(LeaseIssuedDomainEvent).FullName}, {typeof(LeaseIssuedDomainEvent).Assembly.GetName().Name}";
            db.OutboxEvents.Add(OutboxEvent.Capture(TenantId, eventTypeName, payload, null, NowUtc.AddSeconds(-1 - i)));
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedParkedOutboxAsync(IServiceProvider sp, int attempts)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoLeaseNetDbContext>();
        var domainEvent = new LeaseIssuedDomainEvent(
            LeaseId: Guid.NewGuid(),
            TenantId: TenantId,
            CustomerId: null,
            TajeerContractNumber: 9999,
            IssuanceUrl: "https://example/issuance",
            IssuedAtUtc: NowUtc.AddSeconds(-10));
        var payload = System.Text.Json.JsonSerializer.Serialize(domainEvent);
        var eventTypeName = $"{typeof(LeaseIssuedDomainEvent).FullName}, {typeof(LeaseIssuedDomainEvent).Assembly.GetName().Name}";
        var row = OutboxEvent.Capture(TenantId, eventTypeName, payload, null, NowUtc.AddSeconds(-10));
        // Simulate `attempts` past failures.
        for (var i = 0; i < attempts; i++)
        {
            row.MarkFailed("prior failure", TimeSpan.FromSeconds(1), NowUtc.AddSeconds(-9));
        }
        db.OutboxEvents.Add(row);
        await db.SaveChangesAsync();
    }

    private sealed class TestUnitOfWork(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }
}
