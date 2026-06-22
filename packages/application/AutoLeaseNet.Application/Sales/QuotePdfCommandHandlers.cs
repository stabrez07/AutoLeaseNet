using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Messaging;
using AutoLeaseNet.Application.Ports.Pdf;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

internal static class QuotePdfIdempotency
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static IdempotencyKey Key(Guid tenantId, string op, string clientKey) =>
        new($"tenant:{tenantId:N}:quote-pdf-{op}:{clientKey}");

    public static Guid RequireTenantId(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("QuotePdf command requires an authenticated tenant context.");
        return tenant.TenantId;
    }
}

public sealed partial class GenerateQuotePdfCommandHandler(
    IQuotationRepository quotations,
    ICustomerRepository customers,
    IPdfRenderer pdfRenderer,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    ILogger<GenerateQuotePdfCommandHandler> logger)
    : IRequestHandler<GenerateQuotePdfCommand, QuotePdfCommandResult>
{
    public async Task<QuotePdfCommandResult> Handle(GenerateQuotePdfCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = QuotePdfIdempotency.RequireTenantId(tenant);

        var idemKey = QuotePdfIdempotency.Key(tenantId, "generate", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<QuotePdfCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, ct).ConfigureAwait(false);
        if (quotation is null)
            return Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        var customer = await customers.GetByIdAsync(tenantId, quotation.CustomerId, ct).ConfigureAwait(false);
        if (customer is null)
            return Fail("customer.not_found", "Customer not found.");

        try
        {
            var doc = new QuotePdfDocument(
                Title: $"Quote-{quotation.QuoteNumber}",
                Locale: PdfLocale.BilingualArEn,
                CompanyName: "AutoLeaseNet",
                QuoteNumber: quotation.QuoteNumber,
                QuoteDate: quotation.QuoteDate,
                ValidUntilDate: quotation.ValidUntilDate,
                CustomerName: customer.DisplayName ?? (customer.PersonNameEn ?? customer.PersonNameAr ?? "Customer"),
                CustomerIdNumber: customer.PersonIdNumber ?? "N/A",
                SubTotalSar: quotation.SubTotalSar,
                DiscountPercent: quotation.DiscountPercent,
                VatSar: quotation.VatSar,
                TotalSar: quotation.TotalSar,
                LineItems: quotation.Lines.Select(l => new QuoteLineItem(
                    Description: l.Description,
                    Quantity: l.Quantity,
                    UnitPriceSar: l.UnitPriceSar,
                    TotalSar: l.LineTotalSar)).ToList(),
                TermsAndConditions: quotation.TermsAndConditionsMd ?? "Standard Terms Apply");

            var pdf = await pdfRenderer.RenderAsync(doc, ct).ConfigureAwait(false);

            var result = Ok();
            await idempotency.SetAsync(idemKey, result, QuotePdfIdempotency.Ttl, ct).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Information))
                LogGenerated(quotation.Id, pdf.Length);

            return result;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                LogGenerationError(quotation.Id, ex.Message);
            return Fail("pdf.generation_failed", ex.Message);
        }
    }

    private static QuotePdfCommandResult Ok() => new(Success: true);
    private static QuotePdfCommandResult Fail(string code, string message) => new(Success: false, code, message);

    [LoggerMessage(EventId = 9201, Level = LogLevel.Information, Message = "GenerateQuotePdf idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Information, Message = "Quotation {QuotationId} PDF generated ({BytesLength} bytes)")]
    partial void LogGenerated(Guid quotationId, int bytesLength);

    [LoggerMessage(EventId = 9203, Level = LogLevel.Error, Message = "Quotation {QuotationId} PDF generation failed: {ErrorDetail}")]
    partial void LogGenerationError(Guid quotationId, string errorDetail);
}

public sealed partial class SendQuotePdfCommandHandler(
    IQuotationRepository quotations,
    IEmailSender emailSender,
    IPdfRenderer pdfRenderer,
    ICustomerRepository customers,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IUnitOfWork uow,
    IClock clock,
    ILogger<SendQuotePdfCommandHandler> logger)
    : IRequestHandler<SendQuotePdfCommand, QuotePdfCommandResult>
{
    public async Task<QuotePdfCommandResult> Handle(SendQuotePdfCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = QuotePdfIdempotency.RequireTenantId(tenant);

        var idemKey = QuotePdfIdempotency.Key(tenantId, "send", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<QuotePdfCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var quotation = await quotations.GetByIdAsync(tenantId, request.QuotationId, ct).ConfigureAwait(false);
        if (quotation is null)
            return Fail("quotation.not_found", $"Quotation {request.QuotationId} not found.");

        if (quotation.Status != QuotationStatus.Approved
            && quotation.Status != QuotationStatus.PendingApproval
            && quotation.Status != QuotationStatus.SentToCustomer)
            return Fail("quotation.invalid_status", $"Cannot send PDF for status {quotation.Status}.");

        try
        {
            var customer = await customers.GetByIdAsync(tenantId, quotation.CustomerId, ct).ConfigureAwait(false);
            if (customer is null)
                return Fail("customer.not_found", "Customer not found.");

            var doc = new QuotePdfDocument(
                Title: $"Quote-{quotation.QuoteNumber}",
                Locale: PdfLocale.BilingualArEn,
                CompanyName: "AutoLeaseNet",
                QuoteNumber: quotation.QuoteNumber,
                QuoteDate: quotation.QuoteDate,
                ValidUntilDate: quotation.ValidUntilDate,
                CustomerName: customer.DisplayName ?? (customer.PersonNameEn ?? customer.PersonNameAr ?? "Customer"),
                CustomerIdNumber: customer.PersonIdNumber ?? "N/A",
                SubTotalSar: quotation.SubTotalSar,
                DiscountPercent: quotation.DiscountPercent,
                VatSar: quotation.VatSar,
                TotalSar: quotation.TotalSar,
                LineItems: quotation.Lines.Select(l => new QuoteLineItem(
                    Description: l.Description,
                    Quantity: l.Quantity,
                    UnitPriceSar: l.UnitPriceSar,
                    TotalSar: l.LineTotalSar)).ToList(),
                TermsAndConditions: quotation.TermsAndConditionsMd ?? "Standard Terms Apply");

            var pdf = await pdfRenderer.RenderAsync(doc, ct).ConfigureAwait(false);

            var message = new EmailMessage(
                To: request.RecipientEmail,
                Subject: $"Your Vehicle Lease Quote #{quotation.QuoteNumber}",
                HtmlBody: $"<p>Dear {customer.DisplayName ?? (customer.PersonNameEn ?? customer.PersonNameAr ?? "Customer")},</p><p>Attached is your quote for vehicle lease.</p>",
                Attachments: new[] { new EmailAttachment($"Quote-{quotation.QuoteNumber}.pdf", "application/pdf", pdf) });

            var emailResult = await emailSender.SendAsync(message, ct).ConfigureAwait(false);

            if (!emailResult.Success)
                return Fail("email.send_failed", emailResult.FailureDetail ?? "Unknown email error.");

            if (quotation.Status == QuotationStatus.Approved)
            {
                quotation.MarkSentToCustomer(null, clock.UtcNow);
                await uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            var result = Ok();
            await idempotency.SetAsync(idemKey, result, QuotePdfIdempotency.Ttl, ct).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Information))
                LogSent(quotation.Id, request.RecipientEmail);

            return result;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                LogSendError(quotation.Id, ex.Message);
            return Fail("pdf.send_failed", ex.Message);
        }
    }

    private static QuotePdfCommandResult Ok() => new(Success: true);
    private static QuotePdfCommandResult Fail(string code, string message) => new(Success: false, code, message);

    [LoggerMessage(EventId = 9211, Level = LogLevel.Information, Message = "SendQuotePdf idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 9212, Level = LogLevel.Information, Message = "Quotation {QuotationId} PDF sent to {RecipientEmail}")]
    partial void LogSent(Guid quotationId, string recipientEmail);

    [LoggerMessage(EventId = 9213, Level = LogLevel.Error, Message = "Quotation {QuotationId} PDF send failed: {ErrorDetail}")]
    partial void LogSendError(Guid quotationId, string errorDetail);
}
