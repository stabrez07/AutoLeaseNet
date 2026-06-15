using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts;

/// <summary>
/// Pattern B sub-client (Spec 04 §3) for Tajeer contract lifecycle endpoints. Phase 1
/// ships <see cref="SaveAsync"/>, <see cref="CalculatePaymentAsync"/>,
/// <see cref="CloseAsync"/>, <see cref="ExtendAsync"/>, and <see cref="SuspendAsync"/>
/// — the surface needed by the Day-19 check-in saga (Spec 02 §6.4) plus Day-20
/// extend/suspend endpoints. Remaining methods (Get, Cancel, UpdatePaidAmount) land
/// in later workstreams.
///
/// Implementations:
/// - <c>TajeerContractClient</c> in this package (real HTTP).
/// - <c>InMemoryTajeerContractClient</c> in <c>AutoLeaseNet.Adapters.Tajeer.InMemory</c>.
/// Mode is chosen via <c>Tajeer:Mode</c> (<c>Real</c> | <c>InMemory</c>) — see
/// <see cref="ServiceCollectionExtensions.AddTajeer"/>.
///
/// <para>
/// <b>Failure semantics</b> are uniform across all methods:
/// <list type="bullet">
///   <item>2xx with valid body → <c>Success</c></item>
///   <item>2xx / 4xx with <c>errorKey</c> in body → <c>Failure(isTransient=false, errorCode="tajeer.vendor.{errorKey}")</c> — business rule violation</item>
///   <item>5xx / 408 / 429 → handled by the named-client Polly pipeline; if exhausted → <c>Failure(isTransient=true)</c></item>
///   <item>Network / timeout / JSON parse failures → mapped to dedicated error codes (see implementation)</item>
/// </list>
/// </para>
/// </summary>
public interface ITajeerContractClient
{
    /// <summary>
    /// POST a new draft contract to Tajeer. Returns the assigned <c>contractNumber</c> +
    /// <c>issuanceURL</c> the renter follows to complete issuance.
    /// </summary>
    Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Non-destructive preview of the money breakdown for a check-in close (Spec 02 §6.4
    /// box 4). Tajeer recomputes server-side from the contract's current state + the
    /// supplied return readings; the response feeds the ops preview and the eventual
    /// <see cref="CloseAsync"/> call.
    /// </summary>
    Task<IntegrationResult<CalculatePaymentResponse>> CalculatePaymentAsync(
        CalculatePaymentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Destructive vendor commit — flips the Tajeer contract to <c>contractStatusCode = 2</c>
    /// (Closed). Spec 02 §6.4 box 7. Tajeer is responsible for idempotency at its end;
    /// callers should also wrap this with their own idempotency cache.
    /// </summary>
    Task<IntegrationResult<CloseContractResponse>> CloseAsync(
        CloseContractRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Spec 02 §6.7 — push the contract end date forward. Vendor transitions
    /// <c>contractStatusCode</c> to 4 (Extended). Caller enforces the
    /// <c>Lease.MaxExtensions</c> cap locally before invoking this.
    /// </summary>
    Task<IntegrationResult<ExtendContractResponse>> ExtendAsync(
        ExtendContractRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Spec 02 §6.10 — pause the contract pending review (e.g. NON_TRAFFIC_DAMAGE).
    /// Vendor transitions <c>contractStatusCode</c> to 3 (Suspended). Tajeer does
    /// not support the reverse transition; only SUSPENDED → CLOSED is allowed.
    /// </summary>
    Task<IntegrationResult<SuspendContractResponse>> SuspendAsync(
        SuspendContractRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Spec 03 §6.8 — cancel a saved contract before issuance. Used as the compensation
    /// path when replacement orchestration fails after a new Tajeer save has succeeded.
    /// </summary>
    Task<IntegrationResult<Unit>> CancelAsync(
        CancelContractRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Spec 03 §6.3 — read-only lookup of a contract's current state on Tajeer. Used by
    /// the reconciliation drift detector to verify our local mirror matches the vendor's
    /// view (CLAUDE.md §5 — Tajeer is system of record). Vendor 404 surfaces as
    /// <c>tajeer.vendor.contract.not_found</c> rather than a transient failure.
    /// </summary>
    Task<IntegrationResult<GetContractResponse>> GetAsync(
        long contractNumber,
        CancellationToken ct = default);
}
