using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AutoLeaseNet.Application.Ports.Messaging;

namespace AutoLeaseNet.Adapters.Sms.Unifonic;

public sealed class UnifonicOptions
{
    public const string SectionName = "Sms:Unifonic";
    public required string BaseUrl { get; init; }
    public required string AppSid { get; init; }
    public string? SenderId { get; init; }
}

/// <summary>Placeholder Unifonic implementation. Full client to be implemented Phase 1 Week 2.</summary>
internal sealed class UnifonicSmsSender(IOptions<UnifonicOptions> options) : ISmsSender
{
    private readonly UnifonicOptions _options = options.Value;

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct)
        => throw new NotImplementedException("Unifonic adapter implementation pending Phase 1 Week 2.");
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUnifonicSms(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        services.Configure<UnifonicOptions>(configurationSection);
        services.AddHttpClient<ISmsSender, UnifonicSmsSender>();
        return services;
    }
}
