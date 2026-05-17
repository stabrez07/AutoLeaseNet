namespace AutoLeaseNet.Application.Ports.Messaging;

/// <summary>
/// Port for transactional email. Per doc 04 §3.1.
/// Implementations: Adapters.Email.AzureCommunication, Adapters.Email.InMemory.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    string? From = null,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyDictionary<string, string>? Tags = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailSendResult(bool Success, string? ProviderMessageId, string? FailureDetail = null);
