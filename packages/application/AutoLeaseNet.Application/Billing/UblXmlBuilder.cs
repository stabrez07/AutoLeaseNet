using System.Xml.Linq;
using AutoLeaseNet.Domain.Billing;

namespace AutoLeaseNet.Application.Billing;

/// <summary>
/// UBL 2.1 XML builder for invoice clearance per Spec 03 §8.2.
/// Converts Invoice domain aggregate to standard-compliant XML (no external lib).
/// Phase 1: mandatory fields only (single-line invoice, basic customer).
/// Phase 2: multi-line items, attachments, extended payee info.
/// </summary>
public sealed class UblXmlBuilder
{
    private const string UblNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string CanonicalXmlNamespace = "http://www.w3.org/2001/10/xml-exc-c14n#";
    private const string DsNamespace = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>
    /// Build canonical UBL 2.1 XML from invoice. Ready for ECDSA signing.
    /// Returns canonical (exc-c14n) form per ZATCA spec.
    /// </summary>
    public static string BuildUblXml(Invoice invoice, string invoiceHash)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceHash);

        var root = new XElement(
            XName.Get("Invoice", UblNamespace),
            new XAttribute("xmlns", UblNamespace),
            new XAttribute("xmlns:cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XAttribute("xmlns:cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
            new XAttribute("xmlns:ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"),

            // Header elements (mandatory)
            new XElement(XName.Get("cbc:UBLVersionID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), "2.1"),
            new XElement(XName.Get("cbc:CustomizationID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:international:cor:3.0.0"),
            new XElement(XName.Get("cbc:ProfileID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0"),
            new XElement(XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), invoice.InvoiceNumber),
            new XElement(XName.Get("cbc:IssueDate", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), invoice.IssueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(XName.Get("cbc:DueDate", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), invoice.DueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(XName.Get("cbc:InvoiceTypeCode", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"), "380"), // 380 = Commercial invoice

            // Supplier (issuer) - Phase 1: minimal
            BuildSupplierParty(),

            // Customer (bill-to) - Phase 1: customer ID only
            BuildCustomerParty(invoice.CustomerId),

            // Delivery (Phase 1: optional, minimal)
            BuildDelivery(invoice.IssueDateUtc),

            // Payment terms (Phase 1: due date reference)
            BuildPaymentTerms(invoice.DueDateUtc),

            // Totals
            BuildLegalMonetaryTotal(invoice.BaseAmountSar, invoice.VatSar, invoice.TotalSar),

            // Line items (Phase 1: single line for base rent)
            BuildInvoiceLine(invoice.InvoiceNumber, invoice.BaseAmountSar, invoice.VatSar)
        );

        // Return as canonical XML (exc-c14n per ZATCA spec)
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildSupplierParty()
    {
        return new XElement(
            XName.Get("cac:AccountingSupplierParty", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cac:Party", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                new XElement(
                    XName.Get("cac:PartyIdentification", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                    new XElement(
                        XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                        new XAttribute("schemeID", "VAT"),
                        "310000000000000" // Phase 1: placeholder VAT ID (Phase 2: real tenant VAT)
                    )
                ),
                new XElement(
                    XName.Get("cac:PartyLegalEntity", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                    new XElement(
                        XName.Get("cbc:RegistrationName", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                        "AutoLeaseNet KSA" // Phase 1: hardcoded (Phase 2: tenant-configurable)
                    )
                )
            )
        );
    }

    private static XElement BuildCustomerParty(Guid customerId)
    {
        return new XElement(
            XName.Get("cac:AccountingCustomerParty", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cac:Party", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                new XElement(
                    XName.Get("cac:PartyIdentification", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                    new XElement(
                        XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                        new XAttribute("schemeID", "0"),
                        customerId.ToString() // Phase 1: GUID; Phase 2: national ID or passport
                    )
                )
            )
        );
    }

    private static XElement BuildDelivery(DateOnly issueDate)
    {
        return new XElement(
            XName.Get("cac:Delivery", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cbc:ActualDeliveryDate", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                issueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }

    private static XElement BuildPaymentTerms(DateOnly dueDate)
    {
        return new XElement(
            XName.Get("cac:PaymentTerms", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cbc:DueDate", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                dueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }

    private static XElement BuildLegalMonetaryTotal(decimal baseAmount, decimal vat, decimal total)
    {
        return new XElement(
            XName.Get("cac:LegalMonetaryTotal", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cbc:LineExtensionAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                baseAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            ),
            new XElement(
                XName.Get("cbc:TaxExclusiveAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                baseAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            ),
            new XElement(
                XName.Get("cbc:TaxInclusiveAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            ),
            new XElement(
                XName.Get("cbc:AlreadyClaimedTaxTotal", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                "0.00"
            ),
            new XElement(
                XName.Get("cbc:PrepaidAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                "0.00"
            ),
            new XElement(
                XName.Get("cbc:PayableAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }

    private static XElement BuildInvoiceLine(string invoiceNumber, decimal baseAmount, decimal vat)
    {
        const decimal vatRate = 0.15m;

        return new XElement(
            XName.Get("cac:InvoiceLine", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XElement(
                XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                "1"
            ),
            new XElement(
                XName.Get("cbc:InvoicedQuantity", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("unitCode", "MON"),
                "1"
            ),
            new XElement(
                XName.Get("cbc:LineExtensionAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                new XAttribute("currencyID", "SAR"),
                baseAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            ),
            new XElement(
                XName.Get("cac:Item", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                new XElement(
                    XName.Get("cbc:Description", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                    "Monthly Vehicle Lease Rental"
                ),
                new XElement(
                    XName.Get("cbc:Name", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                    $"Invoice {invoiceNumber}"
                )
            ),
            new XElement(
                XName.Get("cac:Price", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                new XElement(
                    XName.Get("cbc:PriceAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                    new XAttribute("currencyID", "SAR"),
                    baseAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                )
            ),
            new XElement(
                XName.Get("cac:TaxTotal", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                new XElement(
                    XName.Get("cbc:TaxAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                    new XAttribute("currencyID", "SAR"),
                    vat.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                ),
                new XElement(
                    XName.Get("cac:TaxSubtotal", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                    new XElement(
                        XName.Get("cbc:TaxableAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                        new XAttribute("currencyID", "SAR"),
                        baseAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    ),
                    new XElement(
                        XName.Get("cbc:TaxAmount", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                        new XAttribute("currencyID", "SAR"),
                        vat.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    ),
                    new XElement(
                        XName.Get("cac:TaxCategory", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                        new XElement(
                            XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                            "S"
                        ),
                        new XElement(
                            XName.Get("cbc:Percent", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                            (vatRate * 100m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        ),
                        new XElement(
                            XName.Get("cac:TaxScheme", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
                            new XElement(
                                XName.Get("cbc:ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
                                "VAT"
                            )
                        )
                    )
                )
            )
        );
    }
}
