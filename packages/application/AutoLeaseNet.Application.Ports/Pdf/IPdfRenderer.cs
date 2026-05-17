namespace AutoLeaseNet.Application.Ports.Pdf;

/// <summary>
/// Port for PDF generation. Per doc 04 §3.1.
/// Used for: customer quotes, ZATCA-compliant invoices with QR code.
/// Implementations: Adapters.Pdf.QuestPdf.
/// </summary>
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(PdfDocument document, CancellationToken ct);
}

public abstract record PdfDocument(string Title, PdfLocale Locale);

public enum PdfLocale
{
    ArabicRtl,
    EnglishLtr,
    BilingualArEn
}
