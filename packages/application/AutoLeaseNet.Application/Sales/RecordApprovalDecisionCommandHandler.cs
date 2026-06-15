using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class RecordApprovalDecisionCommandHandler(
    IQuotationRepository quotations,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<RecordApprovalDecisionCommandHandler> logger)
    : IRequestHandler<RecordApprovalDecisionCommand, QuotationCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<QuotationCommandResult> Handle(RecordApprovalDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = RequireTenant();

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:approval-decision:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, ct).ConfigureAwait(false);
        if (quotation is null)
            return QuotationCommandResult.Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        var deciderId = tenant.UserId ?? Guid.Empty;
        if (deciderId == Guid.Empty)
            return QuotationCommandResult.Fail("quotation.decider_required", "Approver user identity (UserId) is required to record a decision.");

        try
        {
            quotation.RecordApproval(request.TierLevel, request.Approved, deciderId, request.Notes, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_state", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = QuotationCommandResult.Ok(quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.SubTotalSar, quotation.TotalSar);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogDecision(quotation.Id, request.TierLevel, request.Approved, quotation.Status);
        return result;
    }

    private Guid RequireTenant()
    {
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new InvalidOperationException("RecordApprovalDecisionCommand requires an authenticated tenant context.");
        return id;
    }

    [LoggerMessage(EventId = 8031, Level = LogLevel.Information,
        Message = "RecordApprovalDecision idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8032, Level = LogLevel.Information,
        Message = "Quotation {QuotationId} tier {TierLevel} decision={Approved}; new status={Status}")]
    partial void LogDecision(Guid quotationId, byte tierLevel, bool approved, QuotationStatus status);
}
