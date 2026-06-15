using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class SubmitQuotationForApprovalCommandHandler(
    IQuotationRepository quotations,
    IApprovalTierRepository approvalTiers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<SubmitQuotationForApprovalCommandHandler> logger)
    : IRequestHandler<SubmitQuotationForApprovalCommand, QuotationCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<QuotationCommandResult> Handle(SubmitQuotationForApprovalCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = RequireTenant();

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:submit-quotation:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, ct).ConfigureAwait(false);
        if (quotation is null)
            return QuotationCommandResult.Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        var tiers = await approvalTiers.GetAllActiveAsync(tenantId, ct).ConfigureAwait(false);
        var requiredTiers = ApprovalTierEvaluator.RequiredTiers(quotation.TotalSar, tiers);

        try
        {
            quotation.SubmitForApproval(requiredTiers, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_state", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var tierLevels = requiredTiers.Select(t => t.TierLevel).ToList();
        var result = QuotationCommandResult.Ok(quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.SubTotalSar, quotation.TotalSar, tierLevels);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogSubmitted(quotation.Id, quotation.Status, tierLevels.Count);
        return result;
    }

    private Guid RequireTenant()
    {
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new InvalidOperationException("SubmitQuotationForApprovalCommand requires an authenticated tenant context.");
        return id;
    }

    [LoggerMessage(EventId = 8021, Level = LogLevel.Information,
        Message = "SubmitQuotationForApproval idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8022, Level = LogLevel.Information,
        Message = "Quotation {QuotationId} submitted; status={Status}, requiredTiers={TierCount}")]
    partial void LogSubmitted(Guid quotationId, QuotationStatus status, int tierCount);
}
