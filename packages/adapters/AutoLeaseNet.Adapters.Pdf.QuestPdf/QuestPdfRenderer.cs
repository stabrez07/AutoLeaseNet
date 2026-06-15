using AutoLeaseNet.Application.Ports.Pdf;
using QuestPDF.Fluent;

namespace AutoLeaseNet.Adapters.Pdf.QuestPdf;

/// <summary>
/// QuestPDF implementation of IPdfRenderer (minimal Phase-1 version).
/// Full layout per design.md lands in Phase 2.
/// </summary>
internal sealed class QuestPdfRenderer : IPdfRenderer
{
    public Task<byte[]> RenderAsync(PdfDocument document, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        
        var pdf = Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(594, 841); // A4 pixels
                    page.Margin(20);
                    
                    page.Content().Text($"{document.Title} - PDF Generated").FontSize(16);
                });
            })
            .GeneratePdf();

        return Task.FromResult(pdf);
    }
}
