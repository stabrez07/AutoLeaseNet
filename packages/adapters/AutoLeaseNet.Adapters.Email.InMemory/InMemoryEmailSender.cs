using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Messaging;

namespace AutoLeaseNet.Adapters.Email.InMemory;

public sealed class InMemoryEmailSender : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = new();

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        return Task.FromResult(new EmailSendResult(Success: true, ProviderMessageId: $"in-mem-{Guid.NewGuid():N}"));
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
