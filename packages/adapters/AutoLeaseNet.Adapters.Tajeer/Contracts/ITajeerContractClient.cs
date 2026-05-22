using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts;

/// <summary>
/// Pattern B sub-client (Spec 04 §3) for Tajeer contract lifecycle endpoints. Phase 1 /
/// Week 1 ships <see cref="SaveAsync"/> only; remaining methods (Get, Extend, Suspend,
/// Close, Cancel, UpdatePaidAmount, CalculatePayment) land in later workstreams.
///
/// Implementations:
/// - <c>TajeerContractClient</c> in this package (real HTTP).
/// - <c>InMemoryTajeerContractClient</c> in <c>AutoLeaseNet.Adapters.Tajeer.InMemory</c>.
/// Mode is chosen via <c>Tajeer:Mode</c> (<c>Real</c> | <c>InMemory</c>) — see
/// <see cref="ServiceCollectionExtensions.AddTajeer"/>.
/// </summary>
public interface ITajeerContractClient
{
    /// <summary>
    /// POST a new draft contract to Tajeer. Returns the assigned <c>contractNumber</c> +
    /// <c>issuanceURL</c> the renter follows to complete issuance.
    /// </summary>
    /// <remarks>
    /// Failure semantics:
    /// <list type="bullet">
    ///   <item>2xx with valid body → <c>Success</c></item>
    ///   <item>2xx / 4xx with <c>errorKey</c> in body → <c>Failure(isTransient=false, errorCode="tajeer.vendor.{errorKey}")</c> — business rule violation</item>
    ///   <item>5xx / 408 / 429 → handled by the named-client Polly pipeline; if exhausted → <c>Failure(isTransient=true)</c></item>
    ///   <item>Network / timeout / JSON parse failures → mapped to dedicated error codes (see implementation)</item>
    /// </list>
    /// </remarks>
    Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default);
}
