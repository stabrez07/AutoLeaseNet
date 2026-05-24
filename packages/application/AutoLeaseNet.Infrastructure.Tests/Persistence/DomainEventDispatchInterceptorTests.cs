using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// Verifies that <see cref="DomainEventDispatchInterceptor"/> walks the change tracker
/// after <c>SaveChangesAsync</c> commits and publishes each raised
/// <c>IDomainEvent</c> through MediatR as a <see cref="DomainEventNotification{TEvent}"/>,
/// then clears the events from the entity so a subsequent save doesn't re-publish.
///
/// Pre-interceptor, dispatch was hand-rolled inside one specific endpoint
/// (<c>TajeerWebhookEndpoints.DispatchDomainEventsAsync</c>) — meaning any other
/// caller of <c>SaveChangesAsync</c> would have silently dropped events. This test
/// exists to make sure that lift is permanent: dispatch is now a property of the
/// DbContext, not of one HTTP handler.
/// </summary>
public sealed class DomainEventDispatchInterceptorTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");

    [Fact]
    public async Task SaveChangesAsync_publishes_one_DomainEventNotification_per_raised_event()
    {
        var publisher = Substitute.For<IPublisher>();
        await using var db = NewDb(publisher);

        var nowUtc = DateTimeOffset.UtcNow;
        var lease = NewPendingLease(tajeerContractNumber: 1001, nowUtc: nowUtc);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();
        publisher.ClearReceivedCalls(); // the initial Add raised no events; ignore noise.

        lease.MarkIssued(startKm: 12_345, startFuelLevelCode: 4, conditionNotes: "clean", nowUtc: nowUtc);
        await db.SaveChangesAsync();

        await publisher.Received(1).Publish(
            Arg.Is<DomainEventNotification<LeaseIssuedDomainEvent>>(n =>
                n.Event.LeaseId == lease.Id &&
                n.Event.TajeerContractNumber == 1001L),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_clears_events_so_a_second_save_does_not_republish()
    {
        var publisher = Substitute.For<IPublisher>();
        await using var db = NewDb(publisher);

        var nowUtc = DateTimeOffset.UtcNow;
        var lease = NewPendingLease(tajeerContractNumber: 1002, nowUtc: nowUtc);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, nowUtc: nowUtc);
        await db.SaveChangesAsync();
        // A second save with no further state changes (e.g. an UpdatedAtUtc touch) must
        // NOT re-publish the LeaseIssued event.
        publisher.ClearReceivedCalls();
        await db.SaveChangesAsync();

        await publisher.DidNotReceive().Publish(
            Arg.Any<DomainEventNotification<LeaseIssuedDomainEvent>>(),
            Arg.Any<CancellationToken>());
        lease.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_when_no_events_raised_does_not_invoke_publisher()
    {
        var publisher = Substitute.For<IPublisher>();
        await using var db = NewDb(publisher);

        var lease = NewPendingLease(tajeerContractNumber: 1003, nowUtc: DateTimeOffset.UtcNow);
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        await publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    private static AutoLeaseNetDbContext NewDb(IPublisher publisher)
    {
        var interceptor = new DomainEventDispatchInterceptor(publisher);
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
