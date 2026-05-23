using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Leases;

/// <summary>
/// Lease aggregate root. Carries every field BI / executive reporting / downstream
/// decision systems will eventually need — payment breakdown, KM + fuel snapshots at
/// issuance and return, every state-transition timestamp, suspension/closure reasons,
/// extension count, contract terms, and references to Customer / Vehicle / Driver /
/// Branch / RentPolicy / ExtendedCoverage aggregates.
///
/// <para>
/// Tajeer is system of record for the lease lifecycle (CLAUDE.md §5). The webhook
/// receiver + saga are the only legitimate writers for state transitions — direct
/// mutation from elsewhere is a bug. Every transition method validates the current
/// status and is idempotent against same-state re-entry (defends against webhook
/// replays).
/// </para>
///
/// <para>
/// Customer / Vehicle / Driver / Branch reference IDs are nullable today because
/// Day-5 callers construct the lease from a Tajeer DTO; the Day-D reshape will
/// populate them via domain lookups before persistence.
/// </para>
/// </summary>
public sealed class Lease : Entity
{
    // ─── References ─────────────────────────────────────────────────────────
    /// <summary>Optional — populated for B2B leases tied to a Customer (Fleet account).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Local Vehicle aggregate the lease is for. Nullable until Day D wires lookups.</summary>
    public Guid? VehicleId { get; private set; }

    /// <summary>Primary driver. Nullable until Day D wires lookups.</summary>
    public Guid? PrimaryDriverId { get; private set; }

    /// <summary>Optional extra (companion) driver per Tajeer V9.7.</summary>
    public Guid? ExtraDriverId { get; private set; }

    /// <summary>Optional authorized driver (TAMM delegation case).</summary>
    public Guid? AuthorizedDriverId { get; private set; }

    /// <summary>Local RentPolicy aggregate. Nullable until Day D wires lookups.</summary>
    public Guid? RentPolicyId { get; private set; }

    /// <summary>Optional Extended (insurance) Coverage selected at saving.</summary>
    public Guid? ExtendedCoverageId { get; private set; }

    /// <summary>Local Branch aggregate where the contract was issued.</summary>
    public Guid? WorkingBranchId { get; private set; }

    /// <summary>Branch where the vehicle is to be picked up.</summary>
    public Guid? ReceiveBranchId { get; private set; }

    /// <summary>Branch where the vehicle is to be returned.</summary>
    public Guid? ReturnBranchId { get; private set; }

    // ─── Tajeer system-of-record refs ───────────────────────────────────────
    /// <summary>Tajeer's contract identifier — null until SaveContract has succeeded.</summary>
    public long? TajeerContractNumber { get; private set; }

    /// <summary>Tajeer-issued token used by the renter to complete issuance.</summary>
    public string? TajeerIssuanceToken { get; private set; }

    /// <summary>The URL Tajeer returns for the renter to complete (issue) the contract.</summary>
    public string? IssuanceUrl { get; private set; }

    /// <summary>Tajeer branch id mirrored from the SaveContract request (operator's branch).</summary>
    public int? TajeerWorkingBranchId { get; private set; }

    public int? TajeerReceiveBranchId { get; private set; }
    public int? TajeerReturnBranchId { get; private set; }
    public int? TajeerRentPolicyId { get; private set; }
    public int? TajeerExtendedCoverageId { get; private set; }
    public long? TajeerOperatorId { get; private set; }

    // ─── Contract terms ─────────────────────────────────────────────────────
    /// <summary>1=Daily, 2=Hourly, 3=Daily+Driver, 4=Hourly+Driver (Spec 03 §7.5).</summary>
    public int ContractTypeCode { get; private set; }

    public DateTimeOffset ContractStartUtc { get; private set; }
    public DateTimeOffset ContractEndUtc { get; private set; }
    public DateTimeOffset? ActualReturnUtc { get; private set; }

    public int AllowedKmPerHour { get; private set; }
    public int AllowedKmPerDay { get; private set; }
    public bool UnlimitedKm { get; private set; }
    public int AllowedLateHours { get; private set; }

    // ─── Payment snapshot (mirrored from Tajeer mainPaymentDetails) ─────────
    public decimal RentAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int PaymentMethodCode { get; private set; }
    /// <summary>0=none, 1=before-VAT, 2=after-VAT (Tajeer payment lookup).</summary>
    public int? DiscountType { get; private set; }
    public decimal? DiscountValue { get; private set; }

    // ─── At-issuance snapshot ───────────────────────────────────────────────
    public int? StartKm { get; private set; }
    public int? StartFuelLevelCode { get; private set; }
    public string? IssuanceConditionNotes { get; private set; }

    // ─── At-return snapshot ─────────────────────────────────────────────────
    public int? EndKm { get; private set; }
    public int? ReturnFuelLevelCode { get; private set; }
    public string? ReturnConditionNotes { get; private set; }
    public string? DamagesObserved { get; private set; }

    // ─── Lifecycle ──────────────────────────────────────────────────────────
    public LeaseStatus Status { get; private set; }
    public DateTimeOffset? SavedAtUtc { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public DateTimeOffset? SuspendedAtUtc { get; private set; }
    public DateTimeOffset? ResumedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset? ExpiredAtUtc { get; private set; }

    public int ExtensionCount { get; private set; }

    /// <summary>Suspension reason code per Spec 03 §7.4 (1=NonTrafficAccident, 2=FinancialClaims).</summary>
    public int? SuspensionReasonCode { get; private set; }

    /// <summary>Closure main reason per Spec 03 §7.3.</summary>
    public int? ClosureMainReasonCode { get; private set; }
    public int? ClosureSubReasonCode { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>Vendor errorKey when status = SaveFailed.</summary>
    public string? SaveFailureReason { get; private set; }

    /// <summary>True when the renter has invoked Right To Be Forgotten — PII columns must be redacted on next save.</summary>
    public bool PiiOptedOut { get; private set; }

    // ─── Constructors ───────────────────────────────────────────────────────
    private Lease() { }

    /// <summary>
    /// Factory for the Saved-but-not-yet-Issued state — used when the Tajeer SaveContract
    /// call returns success. Carries the full BI-relevant snapshot from the request +
    /// response so we never have to backfill columns from logs later.
    /// </summary>
    public static Lease CreatePending(CreatePendingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TajeerContractNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.IssuanceUrl);

        return new Lease
        {
            TenantId = input.TenantId,
            CustomerId = input.CustomerId,
            VehicleId = input.VehicleId,
            PrimaryDriverId = input.PrimaryDriverId,
            ExtraDriverId = input.ExtraDriverId,
            AuthorizedDriverId = input.AuthorizedDriverId,
            RentPolicyId = input.RentPolicyId,
            ExtendedCoverageId = input.ExtendedCoverageId,
            WorkingBranchId = input.WorkingBranchId,
            ReceiveBranchId = input.ReceiveBranchId,
            ReturnBranchId = input.ReturnBranchId,

            TajeerContractNumber = input.TajeerContractNumber,
            TajeerIssuanceToken = input.TajeerIssuanceToken,
            IssuanceUrl = input.IssuanceUrl,
            TajeerWorkingBranchId = input.TajeerWorkingBranchId,
            TajeerReceiveBranchId = input.TajeerReceiveBranchId,
            TajeerReturnBranchId = input.TajeerReturnBranchId,
            TajeerRentPolicyId = input.TajeerRentPolicyId,
            TajeerExtendedCoverageId = input.TajeerExtendedCoverageId,
            TajeerOperatorId = input.TajeerOperatorId,

            ContractTypeCode = input.ContractTypeCode,
            ContractStartUtc = input.ContractStartUtc,
            ContractEndUtc = input.ContractEndUtc,
            AllowedKmPerHour = input.AllowedKmPerHour,
            AllowedKmPerDay = input.AllowedKmPerDay,
            UnlimitedKm = input.UnlimitedKm,
            AllowedLateHours = input.AllowedLateHours,

            RentAmount = input.RentAmount,
            PaidAmount = input.PaidAmount,
            RemainingAmount = input.RemainingAmount,
            VatAmount = input.VatAmount,
            TotalAmount = input.TotalAmount,
            PaymentMethodCode = input.PaymentMethodCode,
            DiscountType = input.DiscountType,
            DiscountValue = input.DiscountValue,

            Status = LeaseStatus.PendingIssuance,
            SavedAtUtc = input.NowUtc,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    // ─── State transitions ──────────────────────────────────────────────────
    /// <summary>PendingIssuance → Active when Tajeer's LeaseIssued webhook arrives.</summary>
    public void MarkIssued(int? startKm, int? startFuelLevelCode, string? conditionNotes, DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.Active) return; // idempotent replay
        if (Status != LeaseStatus.PendingIssuance)
            throw new InvalidOperationException($"Cannot mark Lease {Id} Issued from status {Status}.");

        Status = LeaseStatus.Active;
        IssuedAtUtc = nowUtc;
        StartKm = startKm;
        StartFuelLevelCode = startFuelLevelCode;
        IssuanceConditionNotes = conditionNotes;
        UpdatedAtUtc = nowUtc;

        RaiseDomainEvent(new LeaseIssuedDomainEvent(
            LeaseId: Id,
            TenantId: TenantId,
            CustomerId: CustomerId,
            TajeerContractNumber: TajeerContractNumber ?? 0,
            IssuanceUrl: IssuanceUrl ?? string.Empty,
            IssuedAtUtc: nowUtc));
    }

    public void MarkSuspended(int suspensionReasonCode, DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.Suspended) return;
        if (Status != LeaseStatus.Active && Status != LeaseStatus.Extended)
            throw new InvalidOperationException($"Cannot suspend Lease {Id} from status {Status}.");

        Status = LeaseStatus.Suspended;
        SuspensionReasonCode = suspensionReasonCode;
        SuspendedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkResumed(DateTimeOffset nowUtc)
    {
        if (Status != LeaseStatus.Suspended)
            throw new InvalidOperationException($"Cannot resume Lease {Id} from status {Status}.");
        Status = ExtensionCount > 0 ? LeaseStatus.Extended : LeaseStatus.Active;
        SuspensionReasonCode = null;
        ResumedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkClosed(
        int closureMainReasonCode,
        int? closureSubReasonCode,
        int? endKm,
        int? returnFuelLevelCode,
        string? returnConditionNotes,
        string? damagesObserved,
        DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.Closed) return;
        if (Status != LeaseStatus.Active && Status != LeaseStatus.Extended && Status != LeaseStatus.Suspended)
            throw new InvalidOperationException($"Cannot close Lease {Id} from status {Status}.");

        Status = LeaseStatus.Closed;
        ClosureMainReasonCode = closureMainReasonCode;
        ClosureSubReasonCode = closureSubReasonCode;
        EndKm = endKm;
        ReturnFuelLevelCode = returnFuelLevelCode;
        ReturnConditionNotes = returnConditionNotes;
        DamagesObserved = damagesObserved;
        ActualReturnUtc = nowUtc;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(string cancellationReason, DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.Cancelled) return;
        if (Status != LeaseStatus.PendingIssuance)
            throw new InvalidOperationException($"Cannot cancel Lease {Id} from status {Status} (Tajeer only allows cancel before issuance).");
        ArgumentException.ThrowIfNullOrWhiteSpace(cancellationReason);

        Status = LeaseStatus.Cancelled;
        CancellationReason = cancellationReason;
        CancelledAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkExpired(DateTimeOffset nowUtc)
    {
        if (Status == LeaseStatus.ExpiredDraft) return;
        if (Status != LeaseStatus.PendingIssuance)
            throw new InvalidOperationException($"Cannot expire Lease {Id} from status {Status}.");
        Status = LeaseStatus.ExpiredDraft;
        ExpiredAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void IncrementExtension(DateTimeOffset newEndUtc, DateTimeOffset nowUtc)
    {
        if (Status != LeaseStatus.Active && Status != LeaseStatus.Extended)
            throw new InvalidOperationException($"Cannot extend Lease {Id} from status {Status}.");
        ExtensionCount++;
        ContractEndUtc = newEndUtc;
        Status = LeaseStatus.Extended;
        UpdatedAtUtc = nowUtc;
    }

    public void RecordSaveFailure(string vendorErrorKey, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorErrorKey);
        Status = LeaseStatus.SaveFailed;
        SaveFailureReason = vendorErrorKey;
        UpdatedAtUtc = nowUtc;
    }

    public void OptOutOfPii(DateTimeOffset nowUtc)
    {
        PiiOptedOut = true;
        UpdatedAtUtc = nowUtc;
    }
}

/// <summary>
/// Full constructor input for <see cref="Lease.CreatePending"/>. Optional fields default
/// to null/sentinel values so a Day-5 caller that only knows Tajeer DTO data can populate
/// what it has and leave the rest for the Day-D reshape (Customer / Vehicle / Driver
/// references).
/// </summary>
public sealed record CreatePendingInput
{
    public required Guid TenantId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? VehicleId { get; init; }
    public Guid? PrimaryDriverId { get; init; }
    public Guid? ExtraDriverId { get; init; }
    public Guid? AuthorizedDriverId { get; init; }
    public Guid? RentPolicyId { get; init; }
    public Guid? ExtendedCoverageId { get; init; }
    public Guid? WorkingBranchId { get; init; }
    public Guid? ReceiveBranchId { get; init; }
    public Guid? ReturnBranchId { get; init; }

    public required long TajeerContractNumber { get; init; }
    public string? TajeerIssuanceToken { get; init; }
    public required string IssuanceUrl { get; init; }
    public int? TajeerWorkingBranchId { get; init; }
    public int? TajeerReceiveBranchId { get; init; }
    public int? TajeerReturnBranchId { get; init; }
    public int? TajeerRentPolicyId { get; init; }
    public int? TajeerExtendedCoverageId { get; init; }
    public long? TajeerOperatorId { get; init; }

    public required int ContractTypeCode { get; init; }
    public required DateTimeOffset ContractStartUtc { get; init; }
    public required DateTimeOffset ContractEndUtc { get; init; }
    public int AllowedKmPerHour { get; init; }
    public int AllowedKmPerDay { get; init; }
    public bool UnlimitedKm { get; init; }
    public int AllowedLateHours { get; init; }

    public required decimal RentAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public decimal VatAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public required int PaymentMethodCode { get; init; }
    public int? DiscountType { get; init; }
    public decimal? DiscountValue { get; init; }

    public required DateTimeOffset NowUtc { get; init; }
}
