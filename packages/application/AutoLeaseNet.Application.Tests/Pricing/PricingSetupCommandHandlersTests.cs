using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Application.Pricing;
using AutoLeaseNet.Domain.Pricing;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Pricing;

public sealed class PricingSetupCommandHandlersTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa3333-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_pricing_version_persists_draft()
    {
        var repo = Substitute.For<IPricingVersionRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new CreatePricingVersionCommandHandler(
            repo,
            uow,
            new StubTenantContext(TenantId),
            new FixedClock(Now));

        var result = await handler.Handle(
            new CreatePricingVersionCommand("Summer 2026", Now.AddDays(1)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EntityId.Should().NotBeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        repo.Received(1).Add(Arg.Is<PricingVersion>(x => x.Name == "Summer 2026" && x.Status == PricingVersionStatus.Draft));
    }

    [Fact]
    public async Task Publish_pricing_version_retires_existing_active_version()
    {
        var current = PricingVersion.CreateDraft(TenantId, "Current", Now.AddDays(-10), Now);
        current.Publish(Now.AddDays(-10));

        var target = PricingVersion.CreateDraft(TenantId, "Target", Now.AddDays(1), Now);

        var versions = Substitute.For<IPricingVersionRepository>();
        versions.GetByIdAsync(TenantId, target.Id, Arg.Any<CancellationToken>()).Returns(target);
        versions.GetActiveForAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(current);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new PublishPricingVersionCommandHandler(
            versions,
            uow,
            new StubTenantContext(TenantId),
            new FixedClock(Now));

        var result = await handler.Handle(new PublishPricingVersionCommand(target.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        current.Status.Should().Be(PricingVersionStatus.Retired);
        target.Status.Should().Be(PricingVersionStatus.Published);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upsert_discount_policy_updates_existing_policy()
    {
        var existing = PricingDiscountPolicy.CreateDefault(TenantId, Now);

        var policies = Substitute.For<IPricingDiscountPolicyRepository>();
        policies.GetForTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new UpsertPricingDiscountPolicyCommandHandler(
            policies,
            uow,
            new StubTenantContext(TenantId),
            new FixedClock(Now));

        var result = await handler.Handle(
            new UpsertPricingDiscountPolicyCommand(
                MaxDiscountPercent: 30m,
                AllowedPresets: [10m, 20m, 30m]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        existing.MaxDiscountPercent.Should().Be(30m);
        existing.AllowedPresets.Should().BeEquivalentTo([10m, 20m, 30m]);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_active_pricing_setup_returns_version_formula_and_policy()
    {
        var version = PricingVersion.CreateDraft(TenantId, "Published", Now.AddDays(-1), Now);
        version.Publish(Now);

        var formula = PricingFormulaDefinition.Create(
            tenantId: TenantId,
            code: "BASE_AMOUNT",
            expression: "Quantity * UnitRate * DurationFactor",
            outputField: "BaseAmountSar",
            precision: 2,
            roundingMode: MidpointRounding.AwayFromZero,
            nowUtc: Now,
            isActive: true);

        var policy = PricingDiscountPolicy.CreateDefault(TenantId, Now);

        var versions = Substitute.For<IPricingVersionRepository>();
        versions.GetActiveForAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(version);

        var formulas = Substitute.For<IPricingFormulaDefinitionRepository>();
        formulas.GetActiveForTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns([formula]);

        var policies = Substitute.For<IPricingDiscountPolicyRepository>();
        policies.GetForTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(policy);

        var handler = new GetActivePricingSetupQueryHandler(
            versions,
            formulas,
            policies,
            new StubTenantContext(TenantId),
            new FixedClock(Now));

        var dto = await handler.Handle(new GetActivePricingSetupQuery(), CancellationToken.None);

        dto.PricingVersionName.Should().Be("Published");
        dto.ActiveFormulas.Should().ContainSingle(x => x.Code == "BASE_AMOUNT");
        dto.DiscountPolicy.Should().NotBeNull();
        dto.DiscountPolicy!.AllowedPresets.Should().BeEquivalentTo([10m, 20m]);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class StubTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid? CustomerId => null;
        public Guid? UserId => null;
        public string UserType => "INTERNAL_STAFF";
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public bool IsInternalStaff => true;
        public bool IsSystem => false;
    }
}
