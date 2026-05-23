using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Use case: post a draft contract to Tajeer and persist a local <c>Lease</c> row in
/// <see cref="AutoLeaseNet.Domain.Leases.LeaseStatus.PendingIssuance"/>. Driven by the BFF
/// <c>POST /api/v1/dev/save-contract</c> endpoint.
///
/// <para>
/// Day-D reshape — the command now takes domain references (CustomerId / VehicleId /
/// PrimaryDriverId / RentPolicyId / WorkingBranchId / etc.) plus the contract terms.
/// The handler resolves those aggregates from local repositories, validates them, and
/// BUILDS the Tajeer V9.7 DTO from the looked-up data. Callers no longer need to know
/// Tajeer's wire shape.
/// </para>
/// </summary>
public sealed record SaveContractCommand : IRequest<SaveContractCommandResult>
{
    public required string IdempotencyKey { get; init; }

    /// <summary>The renter (B2B fleet account OR the B2C individual taking the car).</summary>
    public required Guid CustomerId { get; init; }

    /// <summary>The vehicle being leased — must be Available or Reserved.</summary>
    public required Guid VehicleId { get; init; }

    /// <summary>Primary driver — usually the same person as the renter for B2C, a fleet driver for B2B.</summary>
    public required Guid PrimaryDriverId { get; init; }

    /// <summary>Optional second driver per Tajeer V9.7 §6.1.</summary>
    public Guid? ExtraDriverId { get; init; }

    /// <summary>Optional TAMM-authorised driver if renter delegated.</summary>
    public Guid? AuthorizedDriverId { get; init; }

    public required Guid RentPolicyId { get; init; }
    public Guid? ExtendedCoverageId { get; init; }

    /// <summary>Branch where the contract is being issued (operator's branch).</summary>
    public required Guid WorkingBranchId { get; init; }
    /// <summary>Branch where the renter will pick the vehicle up.</summary>
    public required Guid ReceiveBranchId { get; init; }
    /// <summary>Branch where the renter will return the vehicle.</summary>
    public required Guid ReturnBranchId { get; init; }

    public required DateTimeOffset ContractStartUtc { get; init; }
    public required DateTimeOffset ContractEndUtc { get; init; }

    /// <summary>1=Daily, 2=Hourly, 3=Daily+Driver, 4=Hourly+Driver (Spec 03 §7.5).</summary>
    public required int ContractTypeCode { get; init; }

    public int AllowedKmPerHour { get; init; }
    public int AllowedKmPerDay { get; init; }
    public bool UnlimitedKm { get; init; }
    public int AllowedLateHours { get; init; }

    /// <summary>Rent amount the sales rep agreed with the renter (may override the policy's base rate).</summary>
    public required decimal RentAmount { get; init; }
    public decimal PaidAmount { get; init; }
    /// <summary>Tajeer payment method code from the lookup catalogue.</summary>
    public required int PaymentMethodCode { get; init; }
    public int? DiscountType { get; init; }
    public decimal? DiscountValue { get; init; }
}

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
