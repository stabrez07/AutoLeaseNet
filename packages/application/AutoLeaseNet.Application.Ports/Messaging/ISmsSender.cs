namespace AutoLeaseNet.Application.Ports.Messaging;

/// <summary>
/// Port for SMS dispatch. Per doc 04 §3.1 (Pattern A — multiple providers swappable via DI).
/// Implementations: Adapters.Sms.Unifonic, Adapters.Sms.InMemory.
/// </summary>
public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct);
}

public sealed record SmsMessage(
    string ToE164,
    string Body,
    string? SenderId = null,
    IReadOnlyDictionary<string, string>? Tags = null);

public sealed record SmsSendResult(
    bool Success,
    string? ProviderMessageId,
    SmsFailureReason? FailureReason = null,
    string? FailureDetail = null);

public enum SmsFailureReason
{
    InvalidRecipient,
    Throttled,
    InsufficientBalance,
    ProviderUnavailable,
    Other
}
