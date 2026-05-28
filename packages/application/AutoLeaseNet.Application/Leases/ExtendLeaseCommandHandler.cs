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
/// Handler for <see cref="ExtendLeaseCommand"/>. Pre-checks state + invariants
/// locally so the request never reaches Tajeer with a doomed payload, then runs
/// the Tajeer ExtendContract vendor commit, then mirrors the new end date locally.
/// Idempotency-cached.
/// </summary>
public sealed partial class ExtendLeaseCommandHandler(
    ILeaseRepository leases,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ITajeerContractClient tajeer,
    ILogger<ExtendLeaseCommandHandler> logger)
    : IRequestHandler<ExtendLeaseCommand, ExtendLeaseCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<ExtendLeaseCommandResult> Handle(ExtendLeaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("ExtendLeaseCommand requires an authenticated tenant context.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:extend:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<ExtendLeaseCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var lease = await leases.GetByIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (lease is null) return Fail("lease.not_found", $"Lease {request.LeaseId} not found.");
        if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Extended)
            return Fail("lease.invalid_state_for_extend",
                $"Lease {request.LeaseId} status is {lease.Status}; must be Active or Extended.");
        if (lease.ExtensionCount >= Lease.MaxExtensions)
            return Fail("lease.extensions_exhausted",
                $"Lease {request.LeaseId} has reached MaxExtensions ({Lease.MaxExtensions}).");
        if (request.NewContractEndUtc <= lease.ContractEndUtc)
            return Fail("lease.invalid_new_end_date",
                $"NewContractEndUtc {request.NewContractEndUtc:O} must be strictly after current ContractEndUtc {lease.ContractEndUtc:O}.");
        if (lease.TajeerContractNumber is not { } contractNumber)
            return Fail("tajeer.contract_number_missing",
                $"Lease {request.LeaseId} has no TajeerContractNumber; cannot extend at vendor.");

        var tajeerEnd = request.NewContractEndUtc.UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var tajeerRequest = new ExtendContractRequest
        {
            ContractNumber = contractNumber,
            NewContractEndDate = tajeerEnd,
            ExtensionReasonCode = request.ExtensionReasonCode,
            AdditionalChargesAmount = request.AdditionalCharges,
            PaymentMethodCode = request.PaymentMethodCode,
        };
        var tajeerResult = await tajeer.ExtendAsync(tajeerRequest, ct).ConfigureAwait(false);
        if (!tajeerResult.IsSuccess)
        {
            LogTajeerFailure(contractNumber, tajeerResult.ErrorCode ?? "unknown", tajeerResult.IsTransient);
            return Fail(
                code: tajeerResult.IsTransient ? "tajeer.extend.transient" : "tajeer.extend.failure",
                message: $"Tajeer ExtendContract failed for contract {contractNumber}: {tajeerResult.ErrorMessage}");
        }
        var vendor = tajeerResult.Value!;

        var nowUtc = clock.UtcNow;
        try
        {
            lease.IncrementExtension(request.NewContractEndUtc, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("lease.extend_rejected", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = new ExtendLeaseCommandResult(
            Success: true,
            LeaseId: lease.Id,
            LeaseStatus: lease.Status.ToString(),
            NewContractEndUtc: lease.ContractEndUtc,
            ExtensionCount: lease.ExtensionCount,
            Charges: new ExtensionChargeBreakdown(vendor.TotalDue, vendor.VatAmount, vendor.GrandTotal),
            ErrorCode: null,
            ErrorMessage: null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogExtended(lease.Id, lease.ExtensionCount, contractNumber);
        return result;
    }

    private static ExtendLeaseCommandResult Fail(string code, string message) =>
        new(false, null, null, null, null, null, code, message);

    [LoggerMessage(EventId = 5201, Level = LogLevel.Information, Message = "Extend idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5202, Level = LogLevel.Information, Message = "Lease {LeaseId} extended (count={ExtensionCount}); Tajeer contract {ContractNumber}")]
    partial void LogExtended(Guid leaseId, int extensionCount, long contractNumber);

    [LoggerMessage(EventId = 5203, Level = LogLevel.Warning, Message = "Tajeer ExtendContract failed for contract {ContractNumber}: {ErrorCode} (transient={Transient})")]
    partial void LogTajeerFailure(long contractNumber, string errorCode, bool transient);
}
