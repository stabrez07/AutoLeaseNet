using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Handler for <see cref="SuspendLeaseCommand"/>. Same Tajeer-first ordering as
/// ExtendLeaseCommandHandler — pre-check locally, vendor commit, then mirror.
/// </summary>
public sealed partial class SuspendLeaseCommandHandler(
    ILeaseRepository leases,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ITajeerContractClient tajeer,
    ILogger<SuspendLeaseCommandHandler> logger)
    : IRequestHandler<SuspendLeaseCommand, SuspendLeaseCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<SuspendLeaseCommandResult> Handle(SuspendLeaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("SuspendLeaseCommand requires an authenticated tenant context.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:suspend:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<SuspendLeaseCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var lease = await leases.GetByIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (lease is null) return Fail("lease.not_found", $"Lease {request.LeaseId} not found.");
        if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Extended)
            return Fail("lease.invalid_state_for_suspend",
                $"Lease {request.LeaseId} status is {lease.Status}; must be Active or Extended.");
        if (lease.TajeerContractNumber is not { } contractNumber)
            return Fail("tajeer.contract_number_missing",
                $"Lease {request.LeaseId} has no TajeerContractNumber; cannot suspend at vendor.");

        var nowUtc = clock.UtcNow;
        var tajeerTimestamp = nowUtc.UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var tajeerRequest = new SuspendContractRequest
        {
            ContractNumber = contractNumber,
            SuspensionReasonCode = request.SuspensionReasonCode,
            SuspensionNotes = request.Notes,
            SuspendedAt = tajeerTimestamp,
        };
        var tajeerResult = await tajeer.SuspendAsync(tajeerRequest, ct).ConfigureAwait(false);
        if (!tajeerResult.IsSuccess)
        {
            LogTajeerFailure(contractNumber, tajeerResult.ErrorCode ?? "unknown", tajeerResult.IsTransient);
            return Fail(
                code: tajeerResult.IsTransient ? "tajeer.suspend.transient" : "tajeer.suspend.failure",
                message: $"Tajeer SuspendContract failed for contract {contractNumber}: {tajeerResult.ErrorMessage}");
        }

        try
        {
            lease.MarkSuspended(request.SuspensionReasonCode, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("lease.suspend_rejected", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = new SuspendLeaseCommandResult(
            Success: true,
            LeaseId: lease.Id,
            LeaseStatus: lease.Status.ToString(),
            SuspensionReasonCode: lease.SuspensionReasonCode,
            SuspendedAtUtc: lease.SuspendedAtUtc,
            ErrorCode: null,
            ErrorMessage: null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogSuspended(lease.Id, request.SuspensionReasonCode, contractNumber);
        return result;
    }

    private static SuspendLeaseCommandResult Fail(string code, string message) =>
        new(false, null, null, null, null, code, message);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "Suspend idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5302, Level = LogLevel.Information, Message = "Lease {LeaseId} suspended (reason {Reason}); Tajeer contract {ContractNumber}")]
    partial void LogSuspended(Guid leaseId, int reason, long contractNumber);

    [LoggerMessage(EventId = 5303, Level = LogLevel.Warning, Message = "Tajeer SuspendContract failed for contract {ContractNumber}: {ErrorCode} (transient={Transient})")]
    partial void LogTajeerFailure(long contractNumber, string errorCode, bool transient);
}
