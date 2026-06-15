using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class AddQuotationLineCommandHandler(
    IQuotationRepository quotations,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<AddQuotationLineCommandHandler> logger)
    : IRequestHandler<AddQuotationLineCommand, QuotationCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<QuotationCommandResult> Handle(AddQuotationLineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = RequireTenant();

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:add-line:{request.IdempotencyKey}");
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
            quotation.AddLine(new AddQuotationLineInput
            {
                ItemType = request.ItemType,
                Description = request.Description,
                VehicleSpecRef = request.VehicleSpecRef,
                Quantity = request.Quantity,
                UnitPriceSar = request.UnitPriceSar,
                DiscountPercent = request.DiscountPercent,
                NowUtc = clock.UtcNow,
            });
        }
        catch (InvalidOperationException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_state", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_line", ex.Message);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = QuotationCommandResult.Ok(quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.SubTotalSar, quotation.TotalSar);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        return result;
    }

    private Guid RequireTenant()
    {
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new InvalidOperationException("AddQuotationLineCommand requires an authenticated tenant context.");
        return id;
    }

    [LoggerMessage(EventId = 8011, Level = LogLevel.Information,
        Message = "AddQuotationLine idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
