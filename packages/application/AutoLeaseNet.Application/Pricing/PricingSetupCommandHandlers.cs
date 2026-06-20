using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Pricing;
using MediatR;

namespace AutoLeaseNet.Application.Pricing;

internal static class PricingSetupTenantGuard
{
    public static Guid RequireTenantId(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Pricing setup command requires an authenticated tenant context.");

        return tenant.TenantId;
    }
}

public sealed class CreatePricingVersionCommandHandler(
    IPricingVersionRepository pricingVersions,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<CreatePricingVersionCommand, PricingSetupCommandResult>
{
    public async Task<PricingSetupCommandResult> Handle(CreatePricingVersionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = PricingSetupTenantGuard.RequireTenantId(tenant);

        PricingVersion version;
        try
        {
            version = PricingVersion.CreateDraft(tenantId, request.Name, request.EffectiveFromUtc, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Fail("pricing.invalid_input", ex.Message);
        }

        pricingVersions.Add(version);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, version.Id, null, null);
    }

    private static PricingSetupCommandResult Fail(string code, string message) => new(false, null, code, message);
}

public sealed class PublishPricingVersionCommandHandler(
    IPricingVersionRepository pricingVersions,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<PublishPricingVersionCommand, PricingSetupCommandResult>
{
    public async Task<PricingSetupCommandResult> Handle(PublishPricingVersionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = PricingSetupTenantGuard.RequireTenantId(tenant);

        var version = await pricingVersions.GetByIdAsync(tenantId, request.PricingVersionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
            return Fail("pricing.version_not_found", $"Pricing version {request.PricingVersionId} was not found.");

        var now = clock.UtcNow;
        var active = await pricingVersions.GetActiveForAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        if (active is not null && active.Id != version.Id)
            active.Retire(now, now);

        try
        {
            version.Publish(now);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("pricing.invalid_transition", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, version.Id, null, null);
    }

    private static PricingSetupCommandResult Fail(string code, string message) => new(false, null, code, message);
}

public sealed class CreatePricingFormulaDefinitionCommandHandler(
    IPricingFormulaDefinitionRepository formulas,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<CreatePricingFormulaDefinitionCommand, PricingSetupCommandResult>
{
    public async Task<PricingSetupCommandResult> Handle(CreatePricingFormulaDefinitionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = PricingSetupTenantGuard.RequireTenantId(tenant);

        var existing = await formulas.GetByCodeAsync(tenantId, request.Code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return Fail("pricing.formula_duplicate", $"Formula code '{request.Code}' already exists.");

        PricingFormulaDefinition formula;
        try
        {
            formula = PricingFormulaDefinition.Create(
                tenantId,
                request.Code,
                request.Expression,
                request.OutputField,
                request.Precision,
                request.RoundingMode,
                clock.UtcNow,
                request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return Fail("pricing.invalid_input", ex.Message);
        }

        formulas.Add(formula);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, formula.Id, null, null);
    }

    private static PricingSetupCommandResult Fail(string code, string message) => new(false, null, code, message);
}

public sealed class UpsertPricingDiscountPolicyCommandHandler(
    IPricingDiscountPolicyRepository policies,
    IUnitOfWork uow,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<UpsertPricingDiscountPolicyCommand, PricingSetupCommandResult>
{
    public async Task<PricingSetupCommandResult> Handle(UpsertPricingDiscountPolicyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = PricingSetupTenantGuard.RequireTenantId(tenant);

        var policy = await policies.GetForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            try
            {
                policy = PricingDiscountPolicy.Create(tenantId, request.MaxDiscountPercent, request.AllowedPresets, clock.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return Fail("pricing.invalid_input", ex.Message);
            }

            policies.Add(policy);
        }
        else
        {
            try
            {
                policy.SetMaxDiscountPercent(request.MaxDiscountPercent, clock.UtcNow);
                policy.SetAllowedPresets(request.AllowedPresets, clock.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return Fail("pricing.invalid_input", ex.Message);
            }
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, policy.Id, null, null);
    }

    private static PricingSetupCommandResult Fail(string code, string message) => new(false, null, code, message);
}

public sealed class GetActivePricingSetupQueryHandler(
    IPricingVersionRepository pricingVersions,
    IPricingFormulaDefinitionRepository formulas,
    IPricingDiscountPolicyRepository policies,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<GetActivePricingSetupQuery, ActivePricingSetupDto>
{
    public async Task<ActivePricingSetupDto> Handle(GetActivePricingSetupQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = PricingSetupTenantGuard.RequireTenantId(tenant);

        var activeVersion = await pricingVersions.GetActiveForAsync(tenantId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        var activeFormulas = await formulas.GetActiveForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var discountPolicy = await policies.GetForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return new ActivePricingSetupDto(
            PricingVersionId: activeVersion?.Id,
            PricingVersionName: activeVersion?.Name,
            EffectiveFromUtc: activeVersion?.EffectiveFromUtc,
            ActiveFormulas: activeFormulas
                .Select(x => new PricingFormulaDto(x.Id, x.Code, x.Expression, x.OutputField, x.Precision, x.RoundingMode))
                .ToList(),
            DiscountPolicy: discountPolicy is null
                ? null
                : new PricingDiscountPolicyDto(discountPolicy.Id, discountPolicy.MaxDiscountPercent, discountPolicy.AllowedPresets.ToList()));
    }
}
