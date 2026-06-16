using MediatR;

namespace AutoLeaseNet.Application.Customers;

public sealed record CreateCustomerB2BCommand(
    string LegalName, string? LegalNameAr,
    string CommercialRegistration, string? VatNumber,
    string? Email, string? Mobile, string? NationalAddress, string? BillingAddress,
    decimal? CreditLimit, string? CreditCurrency,
    string IdempotencyKey) : IRequest<CustomerCommandResult>;

public sealed record CreateCustomerB2CCommand(
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber,
    string? DateOfBirth,
    string? NationalityCode, string? Email, string? Mobile, string? NationalAddress,
    string IdempotencyKey) : IRequest<CustomerCommandResult>;

public sealed record UpdateCustomerStatusCommand(
    Guid CustomerId, string Action,
    string IdempotencyKey) : IRequest<CustomerCommandResult>;

public sealed record CustomerCommandResult(
    bool Success, Guid? CustomerId, string? Status,
    string? ErrorCode, string? ErrorMessage);
