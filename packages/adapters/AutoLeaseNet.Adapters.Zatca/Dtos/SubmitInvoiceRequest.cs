namespace AutoLeaseNet.Adapters.Zatca.Dtos;

/// <summary>
/// Adapter-level submission payload. The application layer (saga) builds this from
/// the Invoice aggregate, the rendered UBL 2.1 XML, and the SHA-256 hashes. The
/// adapter is opaque to UBL details — it just ferries the request to ZATCA.
///
/// <para>
/// Phase-1 carries only the fields the Real client will eventually need to put on
/// the wire. Week-4 may add CSID material once we wire the per-tenant credential
/// provider; that lives outside the request DTO (it's an adapter / auth concern).
/// </para>
/// </summary>
/// <param name="Uuid">Application-generated invoice UUID — the adapter uses this for idempotency.</param>
/// <param name="InvoiceType">Tax (B2B clearance) vs Simplified (B2C reporting).</param>
/// <param name="InvoiceXml">Fully-formed UBL 2.1 XML with embedded xAdES-BES signature.</param>
/// <param name="InvoiceHash">SHA-256 of the canonicalised invoice (Base64).</param>
/// <param name="PreviousInvoiceHash">
/// PIH chain anchor — value of <c>ZatcaChainState.LastClearedInvoiceHash</c> for this tenant
/// or the ZATCA-mandated initial sentinel if this is the tenant's first cleared submission.
/// </param>
public sealed record SubmitInvoiceRequest(
    Guid Uuid,
    ZatcaInvoiceType InvoiceType,
    string InvoiceXml,
    string InvoiceHash,
    string PreviousInvoiceHash);

/// <summary>
/// ZATCA Phase-2 invoice classification. Tax invoices go through the synchronous
/// clearance API; Simplified invoices go through the asynchronous reporting API
/// (within 24h of issuance).
/// </summary>
public enum ZatcaInvoiceType
{
    /// <summary>B2B — must be cleared by ZATCA before being delivered to the buyer.</summary>
    Tax = 0,

    /// <summary>B2C — reported to ZATCA within 24h of issuance.</summary>
    Simplified = 1,
}
