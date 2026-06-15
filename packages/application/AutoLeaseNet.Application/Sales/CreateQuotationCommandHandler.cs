using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Sales;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class CreateQuotationCommandHandler(
    IQuotationRepository quotations,
    ICustomerRepository customers,
    IQuoteNumberGenerator quoteNumberGenerator,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateQuotationCommandHandler> logger)
    : IRequestHandler<CreateQuotationCommand, QuotationCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<QuotationCommandResult> Handle(CreateQuotationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = RequireTenant();

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:create-quotation:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var customer = await customers.GetByIdAsync(tenantId, request.CustomerId, ct).ConfigureAwait(false);
        if (customer is null)
            return QuotationCommandResult.Fail("quotation.customer_not_found", $"Customer {request.CustomerId} not found.");

        var nowUtc = clock.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        if (request.ValidUntilDate < today)
            return QuotationCommandResult.Fail("quotation.invalid_valid_until", "ValidUntilDate cannot be in the past.");

        var accountManagerId = tenant.UserId ?? Guid.Empty;
        var quoteNumber = await quoteNumberGenerator.GenerateAsync(tenantId, ct).ConfigureAwait(false);

        Quotation quotation;
        try
        {
            quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = tenantId,
                QuoteNumber = quoteNumber,
                CustomerId = request.CustomerId,
                AccountManagerId = accountManagerId,
                QuoteDate = today,
                ValidUntilDate = request.ValidUntilDate,
                ContractType = request.ContractType,
                EstimatedDurationMonths = request.EstimatedDurationMonths,
                DiscountPercent = request.DiscountPercent,
                TermsAndConditionsMd = request.TermsAndConditionsMd,
                NowUtc = nowUtc,
            });
        }
        catch (ArgumentException ex)
        {
            return QuotationCommandResult.Fail("quotation.invalid_input", ex.Message);
        }

        foreach (var line in request.Lines)
        {
            try
            {
                quotation.AddLine(new AddQuotationLineInput
                {
                    ItemType = line.ItemType,
                    Description = line.Description,
                    VehicleSpecRef = line.VehicleSpecRef,
                    Quantity = line.Quantity,
                    UnitPriceSar = line.UnitPriceSar,
                    DiscountPercent = line.DiscountPercent,
                    NowUtc = nowUtc,
                });
            }
            catch (ArgumentException ex)
            {
                return QuotationCommandResult.Fail("quotation.invalid_line", ex.Message);
            }
        }

        quotations.Add(quotation);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = QuotationCommandResult.Ok(quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.SubTotalSar, quotation.TotalSar);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogCreated(quotation.Id, quoteNumber);
        return result;
    }

    private Guid RequireTenant()
    {
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new InvalidOperationException("CreateQuotationCommand requires an authenticated tenant context.");
        return id;
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information,
        Message = "CreateQuotation idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information,
        Message = "Quotation {QuotationId} created as Draft with number {QuoteNumber}")]
    partial void LogCreated(Guid quotationId, string quoteNumber);
}
