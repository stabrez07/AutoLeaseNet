using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Zatca.Dtos;

namespace AutoLeaseNet.Adapters.Zatca;

/// <summary>
/// ZATCA Phase-2 e-invoicing client (Pattern B per Spec 04 §3.2). One method this PR:
/// <see cref="SubmitInvoiceAsync"/> — translates the Fatoorah gateway's HTTP response
/// into an adapter-level <see cref="SubmitInvoiceResponse"/>. The saga is responsible
/// for translating the adapter outcome into the broader ZatcaSubmission state machine
/// (PROCESSING / NETWORK_ERROR / DEAD_LETTER per Spec 02 §4.5).
///
/// <para>
/// Phase-1 (this workstream) wires the contract only — the Real implementation returns
/// <c>zatca.not_yet_implemented</c>. Week-4 lights up real UBL 2.1 + ECDSA P-256 + TLV
/// QR + actual sandbox round-trips.
/// </para>
///
/// <para>
/// Future methods (added with their workstreams, NOT here):
/// <list type="bullet">
///   <item><c>GetSubmissionStatusAsync</c> — polling fallback when a Submit times out.</item>
///   <item><c>OnboardEgsAsync</c> — CSR / CSID lifecycle.</item>
/// </list>
/// </para>
/// </summary>
public interface IZatcaClient
{
    Task<IntegrationResult<SubmitInvoiceResponse>> SubmitInvoiceAsync(
        SubmitInvoiceRequest request,
        CancellationToken ct = default);
}
