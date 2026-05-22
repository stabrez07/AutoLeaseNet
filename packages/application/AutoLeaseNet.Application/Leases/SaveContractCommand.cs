using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Use case: post a draft contract to Tajeer and persist a local <c>Lease</c> row in
/// <see cref="AutoLeaseNet.Domain.Leases.LeaseStatus.PendingIssuance"/>. Driven by the BFF
/// <c>POST /api/v1/dev/save-contract</c> endpoint.
///
/// <para>
/// The Tajeer-shaped <see cref="SaveContractRequest"/> flows through unchanged for Phase 1
/// (Pattern B per Spec 04 §3.2 — the application layer talks to Tajeer's DTOs directly).
/// Week 2+ may introduce a domain-shaped command when the form layer evolves.
/// </para>
/// </summary>
/// <param name="IdempotencyKey">Client-supplied key (BFF requires <c>Idempotency-Key</c> header).</param>
/// <param name="CustomerId">Optional fleet/customer association for B2B leases.</param>
/// <param name="Request">Tajeer V9.7 save-contract payload.</param>
public sealed record SaveContractCommand(
    string IdempotencyKey,
    Guid? CustomerId,
    SaveContractRequest Request) : IRequest<SaveContractCommandResult>;

/// <summary>
/// Result of <see cref="SaveContractCommand"/>. <c>Success</c> implies a <c>Lease</c> row
/// was written and Tajeer returned a usable contract number + issuance URL. Vendor business
/// errors and infra failures surface as <c>Success = false</c> with a stable
/// <c>ErrorCode</c> (mirrors <c>IntegrationResult.ErrorCode</c>).
/// </summary>
public sealed record SaveContractCommandResult(
    bool Success,
    Guid? LeaseId,
    long? TajeerContractNumber,
    string? IssuanceUrl,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTransient);
