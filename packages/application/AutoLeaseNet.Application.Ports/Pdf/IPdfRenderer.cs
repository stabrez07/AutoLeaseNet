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

/// <summary>
/// Quotation PDF document record for rendering quotes.
/// </summary>
public sealed record QuotePdfDocument(
    string Title,
    PdfLocale Locale,
    string CompanyName,
    string QuoteNumber,
    DateOnly QuoteDate,
    DateOnly ValidUntilDate,
    string CustomerName,
    string CustomerIdNumber,
    decimal SubTotalSar,
    decimal DiscountPercent,
    decimal VatSar,
    decimal TotalSar,
    List<QuoteLineItem> LineItems,
    string TermsAndConditions) : PdfDocument(Title, Locale);

public sealed record QuoteLineItem(
    string Description,
    int Quantity,
    decimal UnitPriceSar,
    decimal TotalSar);
