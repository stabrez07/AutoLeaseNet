using System.Globalization;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales;

public sealed partial class CreateRfqCommandHandler(
    IRfqRepository rfqs,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateRfqCommandHandler> logger)
    : IRequestHandler<CreateRfqCommand, RfqCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<RfqCommandResult> Handle(CreateRfqCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("rfq.idempotency_required", "CreateRfq requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:rfq-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<RfqCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        if (!Enum.TryParse<RfqSource>(request.Source, ignoreCase: true, out var source))
            return Fail("rfq.invalid_source", $"Unknown RFQ source '{request.Source}'. Valid: {string.Join(", ", Enum.GetNames<RfqSource>())}.");

        var seq = await rfqs.GetNextSequenceAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var rfqNumber = string.Create(CultureInfo.InvariantCulture, $"RFQ-{clock.UtcNow.Year}-{seq:D6}");

        var ownerUserId = tenant.UserId ?? Guid.Empty;

        Rfq rfq;
        try
        {
            rfq = Rfq.Create(new RfqCreateInput
            {
                TenantId = tenantId,
                RfqNumber = rfqNumber,
                CustomerId = request.CustomerId,
                Source = source,
                VehicleQty = request.VehicleQty,
                TenureMonths = request.TenureMonths,
                VehicleCategories = request.VehicleCategories,
                Services = request.Services,
                AnnualMileageCapKm = request.AnnualMileageCapKm,
                ExpectedCloseDate = request.ExpectedCloseDate,
                Notes = request.Notes,
                OwnerUserId = ownerUserId,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("rfq.invalid_input", ex.Message);
        }

        rfqs.Add(rfq);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new RfqCommandResult(true, rfq.Id, null, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(rfq.Id, rfqNumber, tenantId);
        return result;
    }

    private static RfqCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9601, Level = LogLevel.Information,
        Message = "RFQ {RfqId} ({RfqNumber}) created for tenant {TenantId}")]
    partial void LogCreated(Guid rfqId, string rfqNumber, Guid tenantId);

    [LoggerMessage(EventId = 9602, Level = LogLevel.Debug,
        Message = "CreateRfq idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class UpdateRfqStageCommandHandler(
    IRfqRepository rfqs,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateRfqStageCommandHandler> logger)
    : IRequestHandler<UpdateRfqStageCommand, RfqCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<RfqCommandResult> Handle(UpdateRfqStageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("rfq.idempotency_required", "UpdateRfqStage requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:rfq-stage:{request.RfqId:N}:{request.ToStage}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<RfqCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var rfq = await rfqs.GetByIdAsync(tenantId, request.RfqId, cancellationToken).ConfigureAwait(false);
        if (rfq is null)
            return Fail("rfq.not_found", $"RFQ {request.RfqId} not found.");

        if (!Enum.TryParse<RfqStage>(request.ToStage, ignoreCase: true, out var toStage))
            return Fail("rfq.invalid_stage", $"Unknown RFQ stage '{request.ToStage}'. Valid: {string.Join(", ", Enum.GetNames<RfqStage>())}.");

        var userId = tenant.UserId ?? Guid.Empty;

        try
        {
            rfq.TransitionStage(toStage, userId, request.Comment, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("rfq.invalid_transition", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new RfqCommandResult(true, rfq.Id, null, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogStageUpdated(rfq.Id, request.ToStage, tenantId);
        return result;
    }

    private static RfqCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9603, Level = LogLevel.Information,
        Message = "RFQ {RfqId} stage transitioned to {ToStage} for tenant {TenantId}")]
    partial void LogStageUpdated(Guid rfqId, string toStage, Guid tenantId);

    [LoggerMessage(EventId = 9604, Level = LogLevel.Debug,
        Message = "UpdateRfqStage idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class UpdateRfqDetailsCommandHandler(
    IRfqRepository rfqs,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateRfqDetailsCommandHandler> logger)
    : IRequestHandler<UpdateRfqDetailsCommand, RfqCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<RfqCommandResult> Handle(UpdateRfqDetailsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("rfq.idempotency_required", "UpdateRfqDetails requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:rfq-details:{request.RfqId:N}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<RfqCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var rfq = await rfqs.GetByIdAsync(tenantId, request.RfqId, cancellationToken).ConfigureAwait(false);
        if (rfq is null)
            return Fail("rfq.not_found", $"RFQ {request.RfqId} not found.");

        try
        {
            rfq.UpdateDetails(new RfqUpdateInput
            {
                VehicleQty = request.VehicleQty,
                TenureMonths = request.TenureMonths,
                VehicleCategories = request.VehicleCategories,
                Services = request.Services,
                AnnualMileageCapKm = request.AnnualMileageCapKm,
                ExpectedCloseDate = request.ExpectedCloseDate,
                Notes = request.Notes,
                Probability = request.Probability,
            }, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("rfq.invalid_update", ex.Message);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new RfqCommandResult(true, rfq.Id, null, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogDetailsUpdated(rfq.Id, tenantId);
        return result;
    }

    private static RfqCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9605, Level = LogLevel.Information,
        Message = "RFQ {RfqId} details updated for tenant {TenantId}")]
    partial void LogDetailsUpdated(Guid rfqId, Guid tenantId);

    [LoggerMessage(EventId = 9606, Level = LogLevel.Debug,
        Message = "UpdateRfqDetails idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class ConvertRfqToQuotationCommandHandler(
    IRfqRepository rfqs,
    IQuotationRepository quotations,
    ICustomerRepository customers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<ConvertRfqToQuotationCommandHandler> logger)
    : IRequestHandler<ConvertRfqToQuotationCommand, RfqCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<RfqCommandResult> Handle(ConvertRfqToQuotationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("rfq.idempotency_required", "ConvertRfqToQuotation requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:rfq-convert:{request.RfqId:N}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<RfqCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var rfq = await rfqs.GetByIdAsync(tenantId, request.RfqId, cancellationToken).ConfigureAwait(false);
        if (rfq is null)
            return Fail("rfq.not_found", $"RFQ {request.RfqId} not found.");

        if (rfq.Stage is not (RfqStage.Qualified or RfqStage.Proposal or RfqStage.Negotiation))
            return Fail("rfq.invalid_stage_for_conversion",
                $"RFQ {request.RfqId} is in stage {rfq.Stage}; conversion requires Qualified, Proposal, or Negotiation.");

        var customer = await customers.GetByIdAsync(tenantId, rfq.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer is null)
            return Fail("rfq.customer_not_found", $"Customer {rfq.CustomerId} not found.");

        if (customer.Status == CustomerStatus.Closed)
            return Fail("rfq.customer_closed", $"Customer {rfq.CustomerId} is Closed and cannot receive new quotations.");

        if (customer.Status == CustomerStatus.Suspended)
            return Fail("rfq.customer_suspended", $"Customer {rfq.CustomerId} is Suspended and cannot receive new quotations.");

        var userId = tenant.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var quoteNumber = string.Create(CultureInfo.InvariantCulture, $"QT-{now.Year}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant()}");

        Quotation quotation;
        try
        {
            quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = tenantId,
                QuoteNumber = quoteNumber,
                CustomerId = rfq.CustomerId,
                AccountManagerId = userId,
                QuoteDate = DateOnly.FromDateTime(now.UtcDateTime),
                ValidUntilDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(30)),
                ContractType = QuotationContractType.LongTermLease,
                EstimatedDurationMonths = rfq.TenureMonths,
                NowUtc = now,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("rfq.quotation_creation_failed", ex.Message);
        }

        try
        {
            rfq.MarkWon(quotation.Id, userId, now);
        }
        catch (InvalidOperationException ex)
        {
            return Fail("rfq.invalid_transition", ex.Message);
        }

        quotations.Add(quotation);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new RfqCommandResult(true, rfq.Id, quotation.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogConverted(rfq.Id, quotation.Id, tenantId);
        return result;
    }

    private static RfqCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9607, Level = LogLevel.Information,
        Message = "RFQ {RfqId} converted to Quotation {QuotationId} for tenant {TenantId}")]
    partial void LogConverted(Guid rfqId, Guid quotationId, Guid tenantId);

    [LoggerMessage(EventId = 9608, Level = LogLevel.Debug,
        Message = "ConvertRfqToQuotation idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
