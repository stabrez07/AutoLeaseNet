using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Handler for <see cref="SaveContractCommand"/>. Glues Tajeer → DB → idempotency together:
/// <list type="number">
///   <item>Idempotency replay — same key + tenant returns the cached result.</item>
///   <item>Call <see cref="ITajeerContractClient.SaveAsync"/>.</item>
///   <item>On Success → persist <see cref="Lease"/> in <c>PendingIssuance</c> + cache result.</item>
///   <item>On Failure → surface the integration error (no row written, no cache entry).</item>
/// </list>
/// </summary>
public sealed partial class SaveContractCommandHandler(
    ITajeerContractClient tajeer,
    ILeaseRepository leases,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<SaveContractCommandHandler> logger)
    : IRequestHandler<SaveContractCommand, SaveContractCommandResult>
{
    /// <summary>Idempotency-store TTL — 24h per Spec 03 §10 / CLAUDE.md §8.</summary>
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<SaveContractCommandResult> Handle(
        SaveContractCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = request;
        var ct = cancellationToken;

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "SaveContractCommand requires an authenticated tenant context (TenancyMiddleware should have rejected this request).");
        }

        // 1. Idempotency replay — keyed on tenant + client-supplied key so cross-tenant
        //    collisions are impossible by construction.
        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:save-contract:{command.IdempotencyKey}");
        var cached = await idempotency.GetAsync<SaveContractCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogIdempotencyReplay(idemKey.Value);
            return cached;
        }

        // 2. Call Tajeer.
        var integration = await tajeer.SaveAsync(command.Request, ct).ConfigureAwait(false);
        if (!integration.IsSuccess || integration.Value is null)
        {
            LogTajeerFailure(integration.ErrorCode ?? "unknown", integration.IsTransient);
            return new SaveContractCommandResult(
                Success: false,
                LeaseId: null,
                TajeerContractNumber: null,
                IssuanceUrl: null,
                ErrorCode: integration.ErrorCode,
                ErrorMessage: integration.ErrorMessage,
                IsTransient: integration.IsTransient);
        }

        // 3. Persist Lease in PendingIssuance.
        var nowUtc = clock.UtcNow;
        var lease = Lease.CreatePending(
            tenantId: tenantId,
            customerId: command.CustomerId,
            tajeerContractNumber: integration.Value.ContractNumber,
            issuanceUrl: integration.Value.IssuanceUrl,
            nowUtc: nowUtc);
        leases.Add(lease);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = new SaveContractCommandResult(
            Success: true,
            LeaseId: lease.Id,
            TajeerContractNumber: integration.Value.ContractNumber,
            IssuanceUrl: integration.Value.IssuanceUrl,
            ErrorCode: null,
            ErrorMessage: null,
            IsTransient: false);

        // 4. Cache the success so the next retry with the same key replays atomically.
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);

        LogLeaseSaved(lease.Id, integration.Value.ContractNumber);
        return result;
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "SaveContract idempotency replay for key {IdempotencyKey}")]
    partial void LogIdempotencyReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning,
        Message = "Tajeer SaveContract returned failure {ErrorCode} (transient={IsTransient})")]
    partial void LogTajeerFailure(string errorCode, bool isTransient);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information,
        Message = "Lease {LeaseId} saved in PendingIssuance with Tajeer contract {TajeerContractNumber}")]
    partial void LogLeaseSaved(Guid leaseId, long tajeerContractNumber);
}
