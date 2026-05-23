using AutoLeaseNet.Adapters.Sms.InMemory;
using AutoLeaseNet.Application.Leases.Notifications;
using AutoLeaseNet.Application.Ports.Messaging;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Leases;

/// <summary>
/// T7.x — LeaseIssued SMS dispatch. Covers handler happy paths (Ar / En), no-customer
/// short-circuit, missing-mobile short-circuit, customer-not-found, provider failure
/// swallowed, and the InMemorySmsSender capture behaviour itself.
/// </summary>
public sealed class LeaseIssuedSmsHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public InMemorySmsSender Sms { get; }
        public LeaseIssuedSmsHandler Sut { get; }

        public Harness()
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            Sms = new InMemorySmsSender();
            var customers = new EfCustomerRepository(Db);
            Sut = new LeaseIssuedSmsHandler(customers, Sms, NullLogger<LeaseIssuedSmsHandler>.Instance);
        }

        public Customer SeedB2CCustomer(PreferredLanguage lang, string? mobile = "+966500001234")
        {
            var c = Customer.CreateB2C(new B2CCreateInput
            {
                TenantId = TenantId,
                PersonNameEn = "Ahmed",
                IdTypeCode = 1, PersonIdNumber = "1234567890",
                Mobile = mobile, Email = "ahmed@example.sa",
                PreferredLanguage = lang,
                NowUtc = Now,
            });
            Db.Customers.Add(c);
            Db.SaveChanges();
            return c;
        }

        public void Dispose() => Db.Dispose();
    }

    private static LeaseIssuedNotification BuildNotification(Guid? customerId, long contractNumber = 4242) =>
        new(new LeaseIssuedDomainEvent(
            LeaseId: Guid.NewGuid(),
            TenantId: TenantId,
            CustomerId: customerId,
            TajeerContractNumber: contractNumber,
            IssuanceUrl: $"https://tajeerstg.logisti.sa/#/public-contract/{contractNumber}/tok",
            IssuedAtUtc: Now));

    [Fact]
    public async Task Handle_dispatches_Arabic_template_to_customer_mobile_when_preferred_language_is_Ar()
    {
        using var harness = new Harness();
        var customer = harness.SeedB2CCustomer(PreferredLanguage.Ar, mobile: "+966501112222");

        await harness.Sut.Handle(BuildNotification(customer.Id, contractNumber: 9876543210), CancellationToken.None);

        harness.Sms.Sent.Should().HaveCount(1);
        var msg = harness.Sms.Sent.Single();
        msg.ToE164.Should().Be("+966501112222");
        msg.Body.Should().Contain("9876543210");
        msg.Body.Should().Contain("https://tajeerstg.logisti.sa/#/public-contract/9876543210/tok");
        msg.Body.Should().Contain("عقد التأجير", because: "Ar template should include this phrase");
        msg.Tags.Should().NotBeNull();
        msg.Tags!["template"].Should().Be(LeaseIssuedSmsTemplates.TemplateKeyAr);
        msg.Tags["tajeerContractNumber"].Should().Be("9876543210");
    }

    [Fact]
    public async Task Handle_dispatches_English_template_when_preferred_language_is_En()
    {
        using var harness = new Harness();
        var customer = harness.SeedB2CCustomer(PreferredLanguage.En, mobile: "+966509998888");

        await harness.Sut.Handle(BuildNotification(customer.Id, contractNumber: 12345), CancellationToken.None);

        var msg = harness.Sms.Sent.Single();
        msg.Body.Should().Contain("Your lease contract 12345 has been issued");
        msg.Body.Should().Contain("Complete the formalities at:");
        msg.Tags!["template"].Should().Be(LeaseIssuedSmsTemplates.TemplateKeyEn);
    }

    [Fact]
    public async Task Handle_short_circuits_when_event_has_no_CustomerId()
    {
        using var harness = new Harness();

        await harness.Sut.Handle(BuildNotification(customerId: null), CancellationToken.None);

        harness.Sms.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_short_circuits_when_Customer_not_found()
    {
        using var harness = new Harness();

        await harness.Sut.Handle(BuildNotification(customerId: Guid.NewGuid()), CancellationToken.None);

        harness.Sms.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_short_circuits_when_Customer_has_no_mobile()
    {
        using var harness = new Harness();
        var customer = harness.SeedB2CCustomer(PreferredLanguage.Ar, mobile: null);

        await harness.Sut.Handle(BuildNotification(customer.Id), CancellationToken.None);

        harness.Sms.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_swallows_provider_failure_so_issuance_flow_continues()
    {
        using var harness = new Harness();
        var customer = harness.SeedB2CCustomer(PreferredLanguage.Ar);
        harness.Sms.RespondWith = _ => new SmsSendResult(
            Success: false,
            ProviderMessageId: null,
            FailureReason: SmsFailureReason.ProviderUnavailable,
            FailureDetail: "Unifonic 5xx");

        var act = () => harness.Sut.Handle(BuildNotification(customer.Id), CancellationToken.None);

        await act.Should().NotThrowAsync(because: "SMS failures must not interrupt the issuance transaction");
        harness.Sms.Sent.Should().HaveCount(1, because: "the message attempt is still captured even when the simulated provider fails");
    }

    [Fact]
    public async Task Handle_swallows_provider_exception_so_issuance_flow_continues()
    {
        using var harness = new Harness();
        var customer = harness.SeedB2CCustomer(PreferredLanguage.Ar);
        harness.Sms.RespondWith = _ => throw new InvalidOperationException("provider explosion");

        var act = () => harness.Sut.Handle(BuildNotification(customer.Id), CancellationToken.None);

        await act.Should().NotThrowAsync(because: "any unexpected SMS-adapter exception must be logged + swallowed");
    }

    [Fact]
    public async Task InMemorySmsSender_captures_every_message_sent()
    {
        var sender = new InMemorySmsSender();
        var msg = new SmsMessage(ToE164: "+966500000001", Body: "test", SenderId: "TAJEER");

        var result = await sender.SendAsync(msg, CancellationToken.None);

        sender.Sent.Should().ContainSingle().Which.Should().Be(msg);
        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().StartWith("in-mem-");
    }
}
