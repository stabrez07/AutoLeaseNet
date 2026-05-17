using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Messaging;

namespace AutoLeaseNet.Adapters.Sms.InMemory;

/// <summary>In-memory ISmsSender for tests and offline dev. Captures sent messages.</summary>
public sealed class InMemorySmsSender : ISmsSender
{
    public ConcurrentBag<SmsMessage> Sent { get; } = new();
    public Func<SmsMessage, SmsSendResult>? RespondWith { get; set; }

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        var result = RespondWith?.Invoke(message)
                     ?? new SmsSendResult(Success: true, ProviderMessageId: $"in-mem-{Guid.NewGuid():N}");
        return Task.FromResult(result);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemorySms(this IServiceCollection services)
    {
        services.AddSingleton<InMemorySmsSender>();
        services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<InMemorySmsSender>());
        return services;
    }
}
