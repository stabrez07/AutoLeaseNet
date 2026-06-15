using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Domain.Sales;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Sales.Notifications;

/// <summary>
/// Phase-1 push-notification stub for <see cref="QuotationSubmittedForApprovalDomainEvent"/>.
/// Logs the event so the approver-workflow outbox is visible in telemetry. The approver UI
/// polls the <c>GET /api/v1/approvals/pending</c> inbox endpoint; this handler is the
/// future push path (SMS / email) that Phase-2 lights up via Unifonic + SES.
/// </summary>
public sealed partial class QuotationSubmittedForApprovalNotificationHandler(
    ILogger<QuotationSubmittedForApprovalNotificationHandler> logger)
    : INotificationHandler<DomainEventNotification<QuotationSubmittedForApprovalDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<QuotationSubmittedForApprovalDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var evt = notification.Event;
        LogSubmitted(evt.QuotationId, evt.TenantId, evt.TotalSar, evt.FirstTierLevel);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 8060, Level = LogLevel.Information,
        Message = "QuotationSubmittedForApproval: QuotationId={QuotationId} TenantId={TenantId} TotalSar={TotalSar} FirstTierLevel={FirstTierLevel} — approver inbox updated; push notifications are Phase-2.")]
    partial void LogSubmitted(Guid quotationId, Guid tenantId, decimal totalSar, byte firstTierLevel);
}
