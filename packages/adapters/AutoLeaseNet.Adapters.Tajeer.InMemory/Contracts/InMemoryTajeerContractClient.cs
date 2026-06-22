using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

namespace AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;

/// <summary>
/// In-memory implementation of <see cref="ITajeerContractClient"/> for unit tests, offline
/// dev, and contract-conformance testing. Captures each call so tests can assert what was
/// sent, and returns either a default canned response or one produced by an injected factory.
///
/// Selected at runtime via <c>Tajeer:Mode = "InMemory"</c>; see
/// <c>AddInMemoryTajeer</c> for the wiring.
/// </summary>
public sealed class InMemoryTajeerContractClient : ITajeerContractClient
{
    private readonly Func<SaveContractRequest, IntegrationResult<SaveContractResponse>>? _saveFactoryOverride;
    private readonly Func<CalculatePaymentRequest, IntegrationResult<CalculatePaymentResponse>>? _calculateFactoryOverride;
    private readonly Func<CloseContractRequest, IntegrationResult<CloseContractResponse>>? _closeFactoryOverride;
    private readonly Func<ExtendContractRequest, IntegrationResult<ExtendContractResponse>>? _extendFactoryOverride;
    private readonly Func<SuspendContractRequest, IntegrationResult<SuspendContractResponse>>? _suspendFactoryOverride;
    private readonly Func<CancelContractRequest, IntegrationResult<Unit>>? _cancelFactoryOverride;
    private readonly Func<long, IntegrationResult<GetContractResponse>>? _getFactoryOverride;

    private readonly List<SaveContractRequest> _saveCalls = new();
    private readonly List<CalculatePaymentRequest> _calculateCalls = new();
    private readonly List<CloseContractRequest> _closeCalls = new();
    private readonly List<ExtendContractRequest> _extendCalls = new();
    private readonly List<SuspendContractRequest> _suspendCalls = new();
    private readonly List<CancelContractRequest> _cancelCalls = new();
    private readonly List<long> _getCalls = new();

    // Per-contract latest-status projection — what GetAsync derives its response from
    // when no override is supplied. Updated by every state-changing call so a
    // Save→Extend→Suspend sequence is reflected back on the next Get.
    private readonly Dictionary<long, ContractProjection> _projection = new();

    /// <summary>
    /// Construct with optional per-method failure injections. Pass <c>null</c> (or omit) for
    /// any factory you want to leave on the default success path. The parameterless form
    /// (<c>new InMemoryTajeerContractClient()</c>) keeps all factories on defaults.
    /// </summary>
    public InMemoryTajeerContractClient(
        Func<SaveContractRequest, IntegrationResult<SaveContractResponse>>? saveFactory = null,
        Func<CalculatePaymentRequest, IntegrationResult<CalculatePaymentResponse>>? calculateFactory = null,
        Func<CloseContractRequest, IntegrationResult<CloseContractResponse>>? closeFactory = null,
        Func<ExtendContractRequest, IntegrationResult<ExtendContractResponse>>? extendFactory = null,
        Func<SuspendContractRequest, IntegrationResult<SuspendContractResponse>>? suspendFactory = null,
        Func<CancelContractRequest, IntegrationResult<Unit>>? cancelFactory = null,
        Func<long, IntegrationResult<GetContractResponse>>? getFactory = null)
    {
        _saveFactoryOverride = saveFactory;
        _calculateFactoryOverride = calculateFactory;
        _closeFactoryOverride = closeFactory;
        _extendFactoryOverride = extendFactory;
        _suspendFactoryOverride = suspendFactory;
        _cancelFactoryOverride = cancelFactory;
        _getFactoryOverride = getFactory;
    }

    /// <summary>All <see cref="SaveAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<SaveContractRequest> SaveCalls => _saveCalls;

    /// <summary>All <see cref="CalculatePaymentAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<CalculatePaymentRequest> CalculateCalls => _calculateCalls;

    /// <summary>All <see cref="CloseAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<CloseContractRequest> CloseCalls => _closeCalls;

    /// <summary>All <see cref="ExtendAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<ExtendContractRequest> ExtendCalls => _extendCalls;

    /// <summary>All <see cref="SuspendAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<SuspendContractRequest> SuspendCalls => _suspendCalls;

    /// <summary>All <see cref="CancelAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<CancelContractRequest> CancelCalls => _cancelCalls;

    /// <summary>All <see cref="GetAsync"/> calls observed since construction (by contract number).</summary>
    public IReadOnlyList<long> GetCalls => _getCalls;

    public Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _saveCalls.Add(request);

        var result = _saveFactoryOverride is not null
            ? _saveFactoryOverride(request)
            : DefaultSaveResponse(request, _saveCalls.Count);

        if (result.IsSuccess && result.Value is { } saved)
        {
            _projection[saved.ContractNumber] = new ContractProjection(
                ContractStatusCode: 1, // Saved
                ExtensionCount: 0,
                SuspensionReasonCode: null,
                ClosureReasonCode: null,
                ClosureSubReasonCode: null);
        }

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<CalculatePaymentResponse>> CalculatePaymentAsync(
        CalculatePaymentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _calculateCalls.Add(request);

        var result = _calculateFactoryOverride is not null
            ? _calculateFactoryOverride(request)
            : DefaultCalculateResponse(request);

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<CloseContractResponse>> CloseAsync(
        CloseContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _closeCalls.Add(request);

        var result = _closeFactoryOverride is not null
            ? _closeFactoryOverride(request)
            : DefaultCloseResponse(request);

        if (result.IsSuccess)
        {
            _projection[request.ContractNumber] = ProjectionFor(request.ContractNumber) with
            {
                ContractStatusCode = 2,
                ClosureReasonCode = request.ClosureMainReasonCode,
                ClosureSubReasonCode = request.ClosureSubReasonCode,
            };
        }

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<ExtendContractResponse>> ExtendAsync(
        ExtendContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _extendCalls.Add(request);

        var result = _extendFactoryOverride is not null
            ? _extendFactoryOverride(request)
            : DefaultExtendResponse(request);

        if (result.IsSuccess)
        {
            var prior = ProjectionFor(request.ContractNumber);
            _projection[request.ContractNumber] = prior with
            {
                // Tajeer keeps Issued (4) after extensions — extension is local-only refinement.
                ContractStatusCode = 4,
                ExtensionCount = prior.ExtensionCount + 1,
            };
        }

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<SuspendContractResponse>> SuspendAsync(
        SuspendContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _suspendCalls.Add(request);

        var result = _suspendFactoryOverride is not null
            ? _suspendFactoryOverride(request)
            : DefaultSuspendResponse(request);

        if (result.IsSuccess)
        {
            _projection[request.ContractNumber] = ProjectionFor(request.ContractNumber) with
            {
                ContractStatusCode = 3,
                SuspensionReasonCode = request.SuspensionReasonCode,
            };
        }

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<Unit>> CancelAsync(
        CancelContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _cancelCalls.Add(request);

        var result = _cancelFactoryOverride is not null
            ? _cancelFactoryOverride(request)
            : DefaultCancelResponse(request);

        if (result.IsSuccess)
        {
            _projection[request.ContractNumber] = ProjectionFor(request.ContractNumber) with
            {
                ContractStatusCode = 5,
            };
        }

        return Task.FromResult(result);
    }

    public Task<IntegrationResult<GetContractResponse>> GetAsync(
        long contractNumber,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contractNumber);
        _getCalls.Add(contractNumber);

        if (_getFactoryOverride is not null)
        {
            return Task.FromResult(_getFactoryOverride(contractNumber));
        }

        if (!_projection.TryGetValue(contractNumber, out var p))
        {
            // No prior call observed for this contract number — mirror real vendor 404.
            return Task.FromResult(IntegrationResult<GetContractResponse>.Failure(
                errorCode: "tajeer.vendor.contract.not_found",
                errorMessage: $"InMemoryTajeerContractClient has no prior state for contractNumber {contractNumber}.",
                isTransient: false));
        }

        return Task.FromResult(IntegrationResult<GetContractResponse>.Success(new GetContractResponse
        {
            ContractNumber = contractNumber,
            ContractStatusCode = p.ContractStatusCode,
            SuspensionReasonCode = p.SuspensionReasonCode,
            ClosureReasonCode = p.ClosureReasonCode,
            ClosureSubReasonCode = p.ClosureSubReasonCode,
            ExtensionCount = p.ExtensionCount,
        }));
    }

    /// <summary>
    /// Test hook — seed a projection for a contract number directly, without driving it
    /// through a write call first. Useful for drift tests where the local row was created
    /// by another path (seeder, fixture) and we just want GetAsync to return a chosen
    /// vendor state. Overwrites any prior projection.
    /// </summary>
    public void SeedProjection(
        long contractNumber,
        int contractStatusCode,
        int extensionCount = 0,
        int? suspensionReasonCode = null,
        int? closureReasonCode = null,
        int? closureSubReasonCode = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contractNumber);
        _projection[contractNumber] = new ContractProjection(
            contractStatusCode, extensionCount, suspensionReasonCode, closureReasonCode, closureSubReasonCode);
    }

    private ContractProjection ProjectionFor(long contractNumber)
        => _projection.TryGetValue(contractNumber, out var p)
            ? p
            : new ContractProjection(ContractStatusCode: 0, ExtensionCount: 0, null, null, null);

    private sealed record ContractProjection(
        int ContractStatusCode,
        int ExtensionCount,
        int? SuspensionReasonCode,
        int? ClosureReasonCode,
        int? ClosureSubReasonCode);

    private static IntegrationResult<SaveContractResponse> DefaultSaveResponse(SaveContractRequest request, int sequenceNumber)
    {
        var contractNumber = 9_000_000_000L + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000L;
        var token = $"inmem-{Guid.NewGuid():N}";
        var rent = request.PaymentDetails.RentAmount;
        var paid = request.PaymentDetails.PaidAmount;
        var vat = Math.Round(rent * 0.15m, 2);

        var summary = new PaymentSummary
        {
            Paid = paid,
            Remaining = rent - paid,
            Total = rent,
            Vat = vat,
        };

        return IntegrationResult<SaveContractResponse>.Success(new SaveContractResponse
        {
            ContractNumber = contractNumber,
            Token = token,
            IssuanceUrl = $"https://inmemory.tajeer.local/#/public-contract/{contractNumber}/{token}",
            MainPaymentDetails = summary,
            OtherPaymentDetails = new PaymentSummary(),
            TotalPaymentDetails = summary,
        });
    }

    private static IntegrationResult<CalculatePaymentResponse> DefaultCalculateResponse(CalculatePaymentRequest request)
    {
        // Deterministic preview: no late hours, no damages by default; extra-km is echoed
        // back at a flat SAR 0.50/km synthetic rate; VAT = 15% of TotalDue.
        var extraKmFee = (request.ExtraKm ?? 0) * 0.5m;
        var damagesFee = request.AdditionalCharges ?? 0m;
        var discount = request.DiscountAmount ?? 0m;
        var totalDue = Math.Max(0m, extraKmFee + damagesFee - discount);
        var vat = Math.Round(totalDue * 0.15m, 2);

        return IntegrationResult<CalculatePaymentResponse>.Success(new CalculatePaymentResponse
        {
            ContractNumber = request.ContractNumber,
            RentAmount = 0m,                   // the in-memory adapter doesn't know base rent
            PaidAmount = 0m,
            LateHoursFee = 0m,
            ExtraKmFee = extraKmFee,
            DamagesFee = damagesFee,
            DiscountAmount = discount,
            TotalDue = totalDue,
            VatAmount = vat,
            GrandTotal = totalDue + vat,
        });
    }

    private static IntegrationResult<CloseContractResponse> DefaultCloseResponse(CloseContractRequest request)
    {
        return IntegrationResult<CloseContractResponse>.Success(new CloseContractResponse
        {
            ContractNumber = request.ContractNumber,
            ContractStatusCode = 2, // Tajeer status code for Closed
            ClosedAt = request.ReturnDate,
            FinalPaidAmount = request.FinalPaidAmount,
        });
    }

    private static IntegrationResult<ExtendContractResponse> DefaultExtendResponse(ExtendContractRequest request)
    {
        var charges = request.AdditionalChargesAmount ?? 0m;
        var vat = Math.Round(charges * 0.15m, 2);

        return IntegrationResult<ExtendContractResponse>.Success(new ExtendContractResponse
        {
            ContractNumber = request.ContractNumber,
            ContractStatusCode = 4, // Tajeer status code for Extended
            NewContractEndDate = request.NewContractEndDate,
            TotalDue = charges,
            VatAmount = vat,
            GrandTotal = charges + vat,
        });
    }

    private static IntegrationResult<SuspendContractResponse> DefaultSuspendResponse(SuspendContractRequest request)
    {
        return IntegrationResult<SuspendContractResponse>.Success(new SuspendContractResponse
        {
            ContractNumber = request.ContractNumber,
            ContractStatusCode = 3, // Tajeer status code for Suspended
            SuspendedAt = request.SuspendedAt,
        });
    }

    private static IntegrationResult<Unit> DefaultCancelResponse(CancelContractRequest request)
    {
        _ = request;
        return IntegrationResult<Unit>.Success(Unit.Value);
    }
}
