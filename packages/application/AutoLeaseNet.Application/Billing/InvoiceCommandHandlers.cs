using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Billing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Billing;

internal sealed partial class CreateInvoiceFromLeaseCommandHandler(
    ILeaseRepository leases,
    IInvoiceRepository invoices,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateInvoiceFromLeaseCommandHandler> logger)
    : IRequestHandler<CreateInvoiceFromLeaseCommand, CreateInvoiceCommandResult>
{
    public async Task<CreateInvoiceCommandResult> Handle(CreateInvoiceFromLeaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:invoice-from-lease:{request.LeaseId:N}");
        var cached = await idempotency.GetAsync<CreateInvoiceCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        // Fetch lease
        var lease = await leases.GetByIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (lease is null)
            return Fail("lease.not_found", $"Lease {request.LeaseId} not found.");

        // Check if invoice already exists for this lease
        var existing = await invoices.GetByLeaseIdAsync(tenantId, request.LeaseId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            LogAlreadyExists(request.LeaseId, existing.InvoiceNumber);
            var result = Ok(existing.Id, existing.InvoiceNumber);
            await idempotency.SetAsync(idemKey, result, TimeSpan.FromHours(24), ct).ConfigureAwait(false);
            return result;
        }

        try
        {
            // Generate invoice number (tenant-scoped)
            var invoiceNumber = await invoices.GetNextInvoiceNumberAsync(tenantId, ct).ConfigureAwait(false);

            // Phase 1: single monthly rental line (base rent from lease)
            var baseAmountSar = lease.RentAmount;
            if (baseAmountSar <= 0)
            {
                LogInsufficientData(request.LeaseId, "RentAmount not configured");
                return Fail("lease.insufficient_data", "Lease monthly rent not configured.");
            }

            var invoiceDate = clock.UtcNow.Date;
            var invoice = Invoice.CreateFromLease(
                tenantId,
                request.LeaseId,
                lease.CustomerId ?? Guid.Empty,
                invoiceNumber,
                baseAmountSar,
                DateOnly.FromDateTime(invoiceDate));

            // Persist
            var created = await invoices.CreateAsync(invoice, ct).ConfigureAwait(false);

            // Phase 2: emit domain event for ZATCA submission saga trigger
            // (today: Event will be auto-published via Outbox pattern when invoice saved)

            var result = Ok(created.Id, invoiceNumber);
            await idempotency.SetAsync(idemKey, result, TimeSpan.FromHours(24), ct).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Information))
                LogCreated(request.LeaseId, invoiceNumber, created.TotalSar);

            return result;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                LogError(request.LeaseId, ex.Message);
            return Fail("invoice.creation_failed", ex.Message);
        }
    }

    private static CreateInvoiceCommandResult Ok(Guid invoiceId, string invoiceNumber)
        => new(Success: true, InvoiceId: invoiceId, InvoiceNumber: invoiceNumber);

    private static CreateInvoiceCommandResult Fail(string code, string message)
        => new(Success: false, ErrorCode: code, ErrorMessage: message);

    [LoggerMessage(EventId = 8101, Level = LogLevel.Debug, Message = "CreateInvoiceFromLease idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8102, Level = LogLevel.Information, Message = "Invoice already exists for lease {LeaseId}: {InvoiceNumber}")]
    partial void LogAlreadyExists(Guid leaseId, string invoiceNumber);

    [LoggerMessage(EventId = 8103, Level = LogLevel.Warning, Message = "Lease {LeaseId} missing required data: {Detail}")]
    partial void LogInsufficientData(Guid leaseId, string detail);

    [LoggerMessage(EventId = 8104, Level = LogLevel.Information, Message = "Invoice created for lease {LeaseId}: {InvoiceNumber} (total {TotalSar:F2} SAR)")]
    partial void LogCreated(Guid leaseId, string invoiceNumber, decimal totalSar);

    [LoggerMessage(EventId = 8105, Level = LogLevel.Error, Message = "Invoice creation failed for lease {LeaseId}: {ErrorMessage}")]
    partial void LogError(Guid leaseId, string errorMessage);
}
