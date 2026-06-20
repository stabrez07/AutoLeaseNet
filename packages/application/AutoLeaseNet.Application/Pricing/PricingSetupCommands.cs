using AutoLeaseNet.Domain.Pricing;
using MediatR;

namespace AutoLeaseNet.Application.Pricing;

public sealed record CreatePricingVersionCommand(
    string Name,
    DateTimeOffset EffectiveFromUtc) : IRequest<PricingSetupCommandResult>;

public sealed record PublishPricingVersionCommand(
    Guid PricingVersionId) : IRequest<PricingSetupCommandResult>;

public sealed record CreatePricingFormulaDefinitionCommand(
    string Code,
    string Expression,
    string OutputField,
    int Precision,
    MidpointRounding RoundingMode,
    bool IsActive = true) : IRequest<PricingSetupCommandResult>;

public sealed record UpsertPricingDiscountPolicyCommand(
    decimal MaxDiscountPercent,
    IReadOnlyList<decimal> AllowedPresets) : IRequest<PricingSetupCommandResult>;

public sealed record GetActivePricingSetupQuery : IRequest<ActivePricingSetupDto>;

public sealed record PricingSetupCommandResult(
    bool Success,
    Guid? EntityId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record ActivePricingSetupDto(
    Guid? PricingVersionId,
    string? PricingVersionName,
    DateTimeOffset? EffectiveFromUtc,
    IReadOnlyList<PricingFormulaDto> ActiveFormulas,
    PricingDiscountPolicyDto? DiscountPolicy);

public sealed record PricingFormulaDto(
    Guid Id,
    string Code,
    string Expression,
    string OutputField,
    int Precision,
    MidpointRounding RoundingMode);

public sealed record PricingDiscountPolicyDto(
    Guid Id,
    decimal MaxDiscountPercent,
    IReadOnlyList<decimal> AllowedPresets);
