using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class MarkQuotationSentToCustomerCommandHandler(
    IQuotationRepository quotations,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<MarkQuotationSentToCustomerCommandHandler> logger)
    : IRequestHandler<MarkQuotationSentToCustomerCommand, QuotationCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<QuotationCommandResult> Handle(MarkQuotationSentToCustomerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = RequireTenant();

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:send-quotation:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, ct).ConfigureAwait(false);
        if (quotation is null)
            return QuotationCommandResult.Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        try
        {
            quotation.MarkSentToCustomer(request.PdfBlobUri, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_state", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = QuotationCommandResult.Ok(quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.SubTotalSar, quotation.TotalSar);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogSent(quotation.Id);
        return result;
    }

    private Guid RequireTenant()
    {
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new InvalidOperationException("MarkQuotationSentToCustomerCommand requires an authenticated tenant context.");
        return id;
    }

    [LoggerMessage(EventId = 8051, Level = LogLevel.Information,
        Message = "MarkQuotationSentToCustomer idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8052, Level = LogLevel.Information,
        Message = "Quotation {QuotationId} marked SentToCustomer")]
    partial void LogSent(Guid quotationId);
}
