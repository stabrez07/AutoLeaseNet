using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Notifications;

public sealed class Notification : Entity
{
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public string? LinkedEntityType { get; private set; }
    public Guid? LinkedEntityId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid tenantId,
        Guid userId,
        string type,
        string title,
        string? body,
        string? linkedEntityType = null,
        Guid? linkedEntityId = null)
    {
        return new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public void MarkRead(DateTimeOffset nowUtc)
    {
        IsRead = true;
        ReadAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
