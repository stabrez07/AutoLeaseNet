using AutoLeaseNet.Application.Ports.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLeaseNet.Adapters.Pdf.QuestPdf;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuestPdfRenderer(this IServiceCollection services)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        return services;
    }
}
