using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

internal static class QuotationApprovalIdempotency
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static IdempotencyKey Key(Guid tenantId, string op, string clientKey) =>
        new($"tenant:{tenantId:N}:quotation-approval-{op}:{clientKey}");

    public static Guid RequireTenantId(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Quotation approval command requires an authenticated tenant context.");
        return tenant.TenantId;
    }

    internal static class QuotationApprovalMapper
    {
        public static QuotationApprovalCommandResult ToResult(Quotation quotation)
        {
            var nextPending = quotation.Approvals
                .Where(a => a.Status == QuotationApprovalStatus.Pending)
                .OrderBy(a => a.TierLevel)
                .FirstOrDefault();

            return new(
                Success: true,
                QuotationId: quotation.Id,
                Status: quotation.Status,
                NextTierLevel: nextPending?.TierLevel,
                NextRequiredRoleCode: nextPending?.RequiredRoleCode,
                NextAssignedUserId: nextPending?.AssignedUserId,
                ErrorCode: null,
                ErrorMessage: null);
        }
    }
}

public sealed partial class SubmitQuotationForApprovalCommandHandler(
    IQuotationRepository quotations,
    IApprovalTierRepository approvalTiers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<SubmitQuotationForApprovalCommandHandler> logger)
    : IRequestHandler<SubmitQuotationForApprovalCommand, QuotationApprovalCommandResult>
{
    public async Task<QuotationApprovalCommandResult> Handle(SubmitQuotationForApprovalCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("approval.idempotency_required", "Submit quotation approval requires an Idempotency-Key.");

        var tenantId = QuotationApprovalIdempotency.RequireTenantId(tenant);
        var idemKey = QuotationApprovalIdempotency.Key(tenantId, $"submit:{request.QuotationId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<QuotationApprovalCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, cancellationToken).ConfigureAwait(false);
        if (quotation is null)
            return Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        var nowUtc = clock.UtcNow;
        IReadOnlyList<ApprovalTier> requiredTiers;
        IReadOnlyDictionary<byte, Guid>? assignedApproverByTier = null;

        if (request.NamedApprovers is { Count: > 0 } namedApprovers)
        {
            if (namedApprovers.Count is < 2 or > 5)
                return Fail("approval.named_approvers_invalid_count", "Named approvers must be between 2 and 5 people.");
            if (namedApprovers.Any(a => a.UserId == Guid.Empty || string.IsNullOrWhiteSpace(a.Name)))
                return Fail("approval.named_approvers_invalid", "Each named approver must include a valid user id and name.");
            if (namedApprovers.Select(a => a.UserId).Distinct().Count() != namedApprovers.Count)
                return Fail("approval.named_approvers_duplicate", "Named approvers must be unique.");

            requiredTiers = namedApprovers
                .Select((_, i) => ApprovalTier.Create(tenantId, (byte)(i + 1), "ASSIGNED_APPROVER", 0m, nowUtc))
                .ToList();

            assignedApproverByTier = namedApprovers
                .Select((a, i) => new { TierLevel = (byte)(i + 1), a.UserId })
                .ToDictionary(x => x.TierLevel, x => x.UserId);
        }
        else
        {
            var tiers = await approvalTiers.GetActiveForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
            requiredTiers = ApprovalTierEvaluator.RequiredTiers(quotation.TotalSar, tiers);
        }

        try
        {
            quotation.SubmitForApproval(requiredTiers, nowUtc, assignedApproverByTier);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("quotation.invalid_transition", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = QuotationApprovalIdempotency.QuotationApprovalMapper.ToResult(quotation);
        await idempotency.SetAsync(idemKey, result, QuotationApprovalIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static QuotationApprovalCommandResult Fail(string code, string message) =>
        new(false, null, null, null, null, null, code, message);

    [LoggerMessage(EventId = 9301, Level = LogLevel.Information, Message = "Quotation approval submit idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class RecordQuotationApprovalDecisionCommandHandler(
    IQuotationRepository quotations,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<RecordQuotationApprovalDecisionCommandHandler> logger)
    : IRequestHandler<RecordQuotationApprovalDecisionCommand, QuotationApprovalCommandResult>
{
    public async Task<QuotationApprovalCommandResult> Handle(RecordQuotationApprovalDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("approval.idempotency_required", "Approval decision requires an Idempotency-Key.");

        var tenantId = QuotationApprovalIdempotency.RequireTenantId(tenant);
        if (tenant.UserId is null || tenant.UserId == Guid.Empty)
            return Fail("approval.user_missing", "Approval decision requires an authenticated user id.");

        var idemKey = QuotationApprovalIdempotency.Key(tenantId, $"decide:{request.QuotationId:N}:{request.TierLevel}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<QuotationApprovalCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, cancellationToken).ConfigureAwait(false);
        if (quotation is null)
            return Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        try
        {
            quotation.RecordApproval(request.TierLevel, request.Approved, tenant.UserId.Value, request.Comment, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("approval.invalid_transition", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Fail("approval.invalid_input", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = QuotationApprovalIdempotency.QuotationApprovalMapper.ToResult(quotation);
        await idempotency.SetAsync(idemKey, result, QuotationApprovalIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static QuotationApprovalCommandResult Fail(string code, string message) =>
        new(false, null, null, null, null, null, code, message);

    [LoggerMessage(EventId = 9302, Level = LogLevel.Information, Message = "Quotation approval decision idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed class GetPendingQuotationApprovalsQueryHandler(
    IQuotationRepository quotations,
    ITenantContext tenant)
    : IRequestHandler<GetPendingQuotationApprovalsQuery, IReadOnlyList<PendingQuotationApprovalDto>>
{
    public async Task<IReadOnlyList<PendingQuotationApprovalDto>> Handle(GetPendingQuotationApprovalsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = QuotationApprovalIdempotency.RequireTenantId(tenant);

        var pending = await quotations.GetPendingApprovalsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return pending.Select(Map).ToList();
    }

    private static PendingQuotationApprovalDto Map(Quotation quotation)
    {
        var next = quotation.Approvals
            .Where(a => a.Status == QuotationApprovalStatus.Pending)
            .OrderBy(a => a.TierLevel)
            .FirstOrDefault();

        return new PendingQuotationApprovalDto(
            QuotationId: quotation.Id,
            QuoteNumber: quotation.QuoteNumber,
            TotalSar: quotation.TotalSar,
            SubmittedAtUtc: quotation.SubmittedAtUtc,
            NextTierLevel: next?.TierLevel,
            NextRequiredRoleCode: next?.RequiredRoleCode,
            NextAssignedUserId: next?.AssignedUserId,
            PendingTierCount: quotation.Approvals.Count(a => a.Status == QuotationApprovalStatus.Pending));
    }
}
