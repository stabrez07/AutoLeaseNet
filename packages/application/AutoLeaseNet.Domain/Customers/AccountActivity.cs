using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Customers;

/// <summary>
/// Represents an activity/interaction logged against a customer account.
/// Activities form the customer timeline (CRM-style activity feed).
/// </summary>
public sealed class AccountActivity : Entity
{
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Activity type: Call, Meeting, Email, Note, StageChange, DocumentUpload, SystemEvent.
    /// </summary>
    public string ActivityType { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
    public string? Body { get; private set; }

    /// <summary>Inbound, Outbound, or Internal.</summary>
    public string? Direction { get; private set; }

    public int? DurationMinutes { get; private set; }
    public Guid PerformedByUserId { get; private set; }

    /// <summary>Optional link to a related entity: RFQ, Quote, Contract, Invoice.</summary>
    public string? LinkedEntityType { get; private set; }

    public Guid? LinkedEntityId { get; private set; }

    private AccountActivity() { }

    public static AccountActivity Create(
        Guid tenantId,
        Guid customerId,
        string activityType,
        string subject,
        string? body,
        string? direction,
        int? durationMinutes,
        Guid performedByUserId,
        string? linkedEntityType,
        Guid? linkedEntityId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(activityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (performedByUserId == Guid.Empty) throw new ArgumentException("PerformedByUserId required.", nameof(performedByUserId));

        var now = DateTimeOffset.UtcNow;
        return new AccountActivity
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ActivityType = activityType,
            Subject = subject,
            Body = body,
            Direction = direction,
            DurationMinutes = durationMinutes,
            PerformedByUserId = performedByUserId,
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
