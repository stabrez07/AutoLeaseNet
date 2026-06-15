using AutoLeaseNet.Application.Ports.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Adapters.Pdf.QuestPdf;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuestPdfRenderer(this IServiceCollection services)
    {
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        return services;
    }
}
