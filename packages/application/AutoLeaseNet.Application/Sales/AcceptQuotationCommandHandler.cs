using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

/// <summary>
/// Handles <see cref="AcceptQuotationCommand"/>. Marks the quotation Accepted and raises
/// <see cref="Domain.Sales.QuotationAcceptedDomainEvent"/>. Idempotent: replays the cached
/// result on a duplicate Idempotency-Key (24h TTL).
/// The downstream Lease Issuance Saga subscribes to the domain event (Day 29).
/// </summary>
public sealed partial class AcceptQuotationCommandHandler(
    IQuotationRepository quotations,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<AcceptQuotationCommandHandler> logger)
    : IRequestHandler<AcceptQuotationCommand, AcceptQuotationResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<AcceptQuotationResult> Handle(AcceptQuotationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("accept.idempotency_required", "AcceptQuotation requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:quotation-accept:{request.QuotationId:N}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<AcceptQuotationResult>(idemKey, cancellationToken).ConfigureAwait(false);
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
            quotation.Accept(request.CustomerSignature, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("quotation.invalid_transition", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new AcceptQuotationResult(
            Success: true,
            QuotationId: quotation.Id,
            QuoteNumber: quotation.QuoteNumber,
            Status: quotation.Status.ToString(),
            AcceptedAtUtc: quotation.AcceptedAtUtc,
            ErrorCode: null,
            ErrorMessage: null);

        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogAccepted(quotation.Id, quotation.QuoteNumber, tenantId);
        return result;
    }

    private static AcceptQuotationResult Fail(string code, string message) =>
        new(false, null, null, null, null, code, message);

    [LoggerMessage(EventId = 9401, Level = LogLevel.Information,
        Message = "Quotation {QuotationId} ({QuoteNumber}) accepted by tenant {TenantId}")]
    partial void LogAccepted(Guid quotationId, string quoteNumber, Guid tenantId);

    [LoggerMessage(EventId = 9402, Level = LogLevel.Debug,
        Message = "AcceptQuotation idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
