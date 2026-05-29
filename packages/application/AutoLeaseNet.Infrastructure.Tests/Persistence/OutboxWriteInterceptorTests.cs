using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Outbox;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// Behavioural contract for <see cref="OutboxWriteInterceptor"/>. Runs in
/// <c>SavingChangesAsync</c> so the captured <see cref="OutboxEvent"/> row commits
/// in the SAME transaction as the business change — atomicity is the whole point of
/// the outbox. These tests use EF InMemory so the "same transaction" semantic is
/// approximated as "same DbContext.SaveChanges call"; the real ACID guarantee comes
/// from SQL Server in production.
/// </summary>
public sealed class OutboxWriteInterceptorTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");

    [Fact]
    public async Task SavingChangesAsync_captures_one_OutboxEvent_per_raised_domain_event()
    {
        var nowUtc = new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(nowUtc);
        await using var db = NewDb(clock);

        var lease = NewPendingLease(tajeerContractNumber: 5001, nowUtc: nowUtc);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();
        // The initial Add raised no events; assert outbox is empty before the issuance.
        (await db.OutboxEvents.CountAsync()).Should().Be(0);

        lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, nowUtc: nowUtc);
        await db.SaveChangesAsync();

        var rows = await db.OutboxEvents.ToListAsync();
        rows.Should().HaveCount(1);
        var row = rows[0];
        row.TenantId.Should().Be(TenantId, because: "tenancy is read off the event's TenantId property");
        row.EventType.Should().StartWith("AutoLeaseNet.Domain.Leases.LeaseIssuedDomainEvent");
        row.EventType.Should().EndWith(", AutoLeaseNet.Domain",
            because: "assembly-qualified name (without version/culture/key) is what the drain uses to resolve the type");
        row.PayloadJson.Should().Contain("\"leaseId\"")
            .And.Contain("\"tajeerContractNumber\":5001");
        row.ProcessedAtUtc.Should().BeNull(because: "drain hasn't run yet");
        row.Attempts.Should().Be(0);
        row.AvailableAtUtc.Should().Be(nowUtc, because: "no backoff at capture time");
    }

    [Fact]
    public async Task SavingChangesAsync_clears_DomainEvents_so_a_second_save_does_not_capture_twice()
    {
        var nowUtc = new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(nowUtc);
        await using var db = NewDb(clock);

        var lease = NewPendingLease(tajeerContractNumber: 5002, nowUtc: nowUtc);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, nowUtc: nowUtc);
        await db.SaveChangesAsync();

        // Touch + re-save — must not re-capture.
        lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, nowUtc: nowUtc); // idempotent
        await db.SaveChangesAsync();

        (await db.OutboxEvents.CountAsync()).Should().Be(1);
        lease.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChangesAsync_when_no_events_raised_writes_no_OutboxEvents()
    {
        var nowUtc = new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(nowUtc);
        await using var db = NewDb(clock);

        var lease = NewPendingLease(tajeerContractNumber: 5003, nowUtc: nowUtc);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        (await db.OutboxEvents.CountAsync()).Should().Be(0);
    }

    private static AutoLeaseNetDbContext NewDb(IClock clock)
    {
        var interceptor = new OutboxWriteInterceptor(clock, NullLogger<OutboxWriteInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new AutoLeaseNetDbContext(options);
    }

    private static Lease NewPendingLease(long tajeerContractNumber, DateTimeOffset nowUtc) =>
        Lease.CreatePending(new CreatePendingInput
        {
            TenantId = TenantId,
            TajeerContractNumber = tajeerContractNumber,
            IssuanceUrl = $"https://tajeerstg.logisti.sa/#/public-contract/{tajeerContractNumber}/tok",
            ContractTypeCode = 1,
            ContractStartUtc = nowUtc,
            ContractEndUtc = nowUtc.AddDays(2),
            RentAmount = 200m,
            PaymentMethodCode = 1,
            NowUtc = nowUtc,
        });
}
