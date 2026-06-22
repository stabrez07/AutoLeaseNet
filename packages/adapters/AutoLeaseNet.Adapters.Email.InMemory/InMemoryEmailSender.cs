using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Messaging;

namespace AutoLeaseNet.Adapters.Email.InMemory;

public sealed class InMemoryEmailSender : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = new();

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        try
        {
            using var smtp = new SmtpClient("localhost", 1025) { EnableSsl = false };
            var mail = new MailMessage(
                message.From ?? "noreply@autoleasenet.sa",
                message.To,
                message.Subject,
                message.HtmlBody) { IsBodyHtml = true };

            if (message.Attachments is { Count: > 0 })
            {
                foreach (var att in message.Attachments)
                    mail.Attachments.Add(new Attachment(new MemoryStream(att.Content), att.FileName, att.ContentType));
            }

            await smtp.SendMailAsync(mail, ct);
            return new EmailSendResult(Success: true, ProviderMessageId: $"mailhog-{Guid.NewGuid():N}");
        }
        catch (Exception ex)
        {
            return new EmailSendResult(Success: true, ProviderMessageId: $"fallback-{Guid.NewGuid():N}", FailureDetail: ex.Message);
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryEmail(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
        return services;
    }
}
