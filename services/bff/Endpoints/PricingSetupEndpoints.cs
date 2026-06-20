using AutoLeaseNet.Application.Pricing;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Administration endpoints for internal pricing setup (versions, formulas, discount policy).
/// </summary>
public static class PricingSetupEndpoints
{
    public static IEndpointRouteBuilder MapPricingSetupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/pricing-setup").WithTags("admin-pricing-setup");

        group.MapGet("/active", GetActiveSetupAsync).WithName("GetActivePricingSetup").RequireAuthorization();
        group.MapPost("/versions", CreateVersionAsync).WithName("CreatePricingVersion").RequireAuthorization();
        group.MapPost("/versions/{id:guid}/publish", PublishVersionAsync).WithName("PublishPricingVersion").RequireAuthorization();
        group.MapPost("/formulas", CreateFormulaAsync).WithName("CreatePricingFormula").RequireAuthorization();
        group.MapPut("/discount-policy", UpsertDiscountPolicyAsync).WithName("UpsertPricingDiscountPolicy").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetActiveSetupAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetActivePricingSetupQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateVersionAsync(IMediator mediator, CreatePricingVersionRequest body, CancellationToken ct)
    {
        if (body is null) return Results.BadRequest("Missing request body.");

        var command = new CreatePricingVersionCommand(
            Name: body.Name,
            EffectiveFromUtc: body.EffectiveFromUtc);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.Success
            ? Results.Created($"/api/v1/admin/pricing-setup/versions/{result.EntityId}", result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> PublishVersionAsync(IMediator mediator, Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new PublishPricingVersionCommand(id), ct).ConfigureAwait(false);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> CreateFormulaAsync(IMediator mediator, CreatePricingFormulaRequest body, CancellationToken ct)
    {
        if (body is null) return Results.BadRequest("Missing request body.");

        var command = new CreatePricingFormulaDefinitionCommand(
            Code: body.Code,
            Expression: body.Expression,
            OutputField: body.OutputField,
            Precision: body.Precision,
            RoundingMode: body.RoundingMode,
            IsActive: body.IsActive);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.Success
            ? Results.Created($"/api/v1/admin/pricing-setup/formulas/{result.EntityId}", result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> UpsertDiscountPolicyAsync(IMediator mediator, UpsertPricingDiscountPolicyRequest body, CancellationToken ct)
    {
        if (body is null) return Results.BadRequest("Missing request body.");

        var command = new UpsertPricingDiscountPolicyCommand(
            MaxDiscountPercent: body.MaxDiscountPercent,
            AllowedPresets: body.AllowedPresets);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }
}

public sealed record CreatePricingVersionRequest(string Name, DateTimeOffset EffectiveFromUtc);

public sealed record CreatePricingFormulaRequest(
    string Code,
    string Expression,
    string OutputField,
    int Precision,
    MidpointRounding RoundingMode,
    bool IsActive = true);

public sealed record UpsertPricingDiscountPolicyRequest(decimal MaxDiscountPercent, IReadOnlyList<decimal> AllowedPresets);
