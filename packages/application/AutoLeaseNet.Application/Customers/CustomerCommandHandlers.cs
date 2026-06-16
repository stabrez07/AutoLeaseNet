using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Customers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Customers;

public sealed partial class CreateCustomerB2BCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateCustomerB2BCommandHandler> logger)
    : IRequestHandler<CreateCustomerB2BCommand, CustomerCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<CustomerCommandResult> Handle(CreateCustomerB2BCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("customer.idempotency_required", "CreateCustomerB2B requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:customer-b2b-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<CustomerCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        Customer customer;
        try
        {
            customer = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = tenantId,
                LegalName = request.LegalName,
                LegalNameAr = request.LegalNameAr,
                CommercialRegistration = request.CommercialRegistration,
                VatNumber = request.VatNumber,
                Email = request.Email,
                Mobile = request.Mobile,
                NationalAddress = request.NationalAddress,
                BillingAddress = request.BillingAddress,
                CreditLimit = request.CreditLimit,
                CreditCurrency = request.CreditCurrency,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("customer.invalid_input", ex.Message);
        }

        customers.Add(customer);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new CustomerCommandResult(true, customer.Id, customer.Status.ToString(), null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(customer.Id, tenantId);
        return result;
    }

    private static CustomerCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9501, Level = LogLevel.Information,
        Message = "B2B Customer {CustomerId} created for tenant {TenantId}")]
    partial void LogCreated(Guid customerId, Guid tenantId);

    [LoggerMessage(EventId = 9502, Level = LogLevel.Debug,
        Message = "CreateCustomerB2B idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class CreateCustomerB2CCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateCustomerB2CCommandHandler> logger)
    : IRequestHandler<CreateCustomerB2CCommand, CustomerCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<CustomerCommandResult> Handle(CreateCustomerB2CCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("customer.idempotency_required", "CreateCustomerB2C requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:customer-b2c-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<CustomerCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (!DateOnly.TryParseExact(request.DateOfBirth, "yyyy-MM-dd", out var parsed))
                return Fail("customer.invalid_dob", "DateOfBirth must be in YYYY-MM-DD format.");
            dob = parsed;
        }

        Customer customer;
        try
        {
            customer = Customer.CreateB2C(new B2CCreateInput
            {
                TenantId = tenantId,
                PersonNameEn = request.PersonNameEn,
                PersonNameAr = request.PersonNameAr,
                IdTypeCode = request.IdTypeCode,
                PersonIdNumber = request.PersonIdNumber,
                DateOfBirth = dob,
                NationalityCode = request.NationalityCode,
                Email = request.Email,
                Mobile = request.Mobile,
                NationalAddress = request.NationalAddress,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("customer.invalid_input", ex.Message);
        }

        customers.Add(customer);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new CustomerCommandResult(true, customer.Id, customer.Status.ToString(), null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(customer.Id, tenantId);
        return result;
    }

    private static CustomerCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9503, Level = LogLevel.Information,
        Message = "B2C Customer {CustomerId} created for tenant {TenantId}")]
    partial void LogCreated(Guid customerId, Guid tenantId);

    [LoggerMessage(EventId = 9504, Level = LogLevel.Debug,
        Message = "CreateCustomerB2C idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class UpdateCustomerStatusCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateCustomerStatusCommandHandler> logger)
    : IRequestHandler<UpdateCustomerStatusCommand, CustomerCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<CustomerCommandResult> Handle(UpdateCustomerStatusCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("customer.idempotency_required", "UpdateCustomerStatus requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:customer-status:{request.CustomerId:N}:{request.Action}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<CustomerCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var customer = await customers.GetByIdAsync(tenantId, request.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer is null)
            return Fail("customer.not_found", $"Customer {request.CustomerId} not found.");

        try
        {
            switch (request.Action.ToLowerInvariant())
            {
                case "suspend": customer.Suspend(clock.UtcNow); break;
                case "reactivate": customer.Reactivate(clock.UtcNow); break;
                case "close": customer.Close(clock.UtcNow); break;
                default: return Fail("customer.invalid_action", $"Unknown action '{request.Action}'. Valid: suspend, reactivate, close.");
            }
        }
        catch (InvalidOperationException ex)
        {
            return Fail("customer.invalid_transition", ex.Message);
        }

        await customers.UpdateAsync(customer, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new CustomerCommandResult(true, customer.Id, customer.Status.ToString(), null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogUpdated(customer.Id, request.Action, tenantId);
        return result;
    }

    private static CustomerCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 9505, Level = LogLevel.Information,
        Message = "Customer {CustomerId} status action '{Action}' applied for tenant {TenantId}")]
    partial void LogUpdated(Guid customerId, string action, Guid tenantId);

    [LoggerMessage(EventId = 9506, Level = LogLevel.Debug,
        Message = "UpdateCustomerStatus idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
