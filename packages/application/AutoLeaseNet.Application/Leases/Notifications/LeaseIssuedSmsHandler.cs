using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Application.Ports.Messaging;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Leases;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Leases.Notifications;

/// <summary>
/// On <see cref="LeaseIssuedDomainEvent"/> (wrapped as <see cref="DomainEventNotification{TEvent}"/>):
/// look up the renter Customer, render the SMS in their preferred language, dispatch via
/// <see cref="ISmsSender"/>. SMS failures are logged but never re-thrown — a customer-facing
/// notification is best-effort and must not roll back the issuance transaction.
/// </summary>
public sealed partial class LeaseIssuedSmsHandler(
    ICustomerRepository customers,
    ISmsSender sms,
    ILogger<LeaseIssuedSmsHandler> logger) : INotificationHandler<DomainEventNotification<LeaseIssuedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<LeaseIssuedDomainEvent> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var evt = notification.Event;

        if (evt.CustomerId is not { } customerId)
        {
            LogSkippedNoCustomer(evt.LeaseId);
            return;
        }

        var customer = await customers.GetByIdAsync(evt.TenantId, customerId, cancellationToken)
            .ConfigureAwait(false);
        if (customer is null)
        {
            LogSkippedCustomerMissing(evt.LeaseId, customerId);
            return;
        }
        if (string.IsNullOrWhiteSpace(customer.Mobile))
        {
            LogSkippedNoMobile(evt.LeaseId, customerId);
            return;
        }

        var (templateKey, body) = LeaseIssuedSmsTemplates.Render(
            customer.PreferredLanguage,
            evt.TajeerContractNumber,
            evt.IssuanceUrl);

        var message = new SmsMessage(
            ToE164: customer.Mobile,
            Body: body,
            Tags: new Dictionary<string, string>
            {
                ["template"] = templateKey,
                ["leaseId"] = evt.LeaseId.ToString(),
                ["tenantId"] = evt.TenantId.ToString(),
                ["tajeerContractNumber"] = evt.TajeerContractNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });

        try
        {
            var result = await sms.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                LogSent(evt.LeaseId, templateKey, result.ProviderMessageId);
            }
            else
            {
                LogProviderFailure(evt.LeaseId, templateKey, result.FailureReason?.ToString() ?? "Other", result.FailureDetail);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDispatchException(ex, evt.LeaseId, templateKey);
        }
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "LeaseIssued SMS skipped for Lease {LeaseId} — no Customer associated.")]
    partial void LogSkippedNoCustomer(Guid leaseId);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning,
        Message = "LeaseIssued SMS skipped — Customer {CustomerId} not found for Lease {LeaseId}.")]
    partial void LogSkippedCustomerMissing(Guid leaseId, Guid customerId);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information,
        Message = "LeaseIssued SMS skipped for Lease {LeaseId} — Customer {CustomerId} has no mobile on file.")]
    partial void LogSkippedNoMobile(Guid leaseId, Guid customerId);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Information,
        Message = "LeaseIssued SMS sent for Lease {LeaseId} using template {TemplateKey}; providerMessageId={ProviderMessageId}.")]
    partial void LogSent(Guid leaseId, string templateKey, string? providerMessageId);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Warning,
        Message = "LeaseIssued SMS dispatch returned non-success for Lease {LeaseId} (template {TemplateKey}; reason {Reason}; detail {Detail}).")]
    partial void LogProviderFailure(Guid leaseId, string templateKey, string reason, string? detail);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Error,
        Message = "LeaseIssued SMS dispatch threw for Lease {LeaseId} (template {TemplateKey}); swallowed so issuance flow continues.")]
    partial void LogDispatchException(Exception ex, Guid leaseId, string templateKey);
}
