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

    private readonly List<SaveContractRequest> _saveCalls = new();
    private readonly List<CalculatePaymentRequest> _calculateCalls = new();
    private readonly List<CloseContractRequest> _closeCalls = new();

    /// <summary>
    /// Construct with optional per-method failure injections. Pass <c>null</c> (or omit) for
    /// any factory you want to leave on the default success path. The parameterless form
    /// (<c>new InMemoryTajeerContractClient()</c>) keeps all three on defaults.
    /// </summary>
    public InMemoryTajeerContractClient(
        Func<SaveContractRequest, IntegrationResult<SaveContractResponse>>? saveFactory = null,
        Func<CalculatePaymentRequest, IntegrationResult<CalculatePaymentResponse>>? calculateFactory = null,
        Func<CloseContractRequest, IntegrationResult<CloseContractResponse>>? closeFactory = null)
    {
        _saveFactoryOverride = saveFactory;
        _calculateFactoryOverride = calculateFactory;
        _closeFactoryOverride = closeFactory;
    }

    /// <summary>All <see cref="SaveAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<SaveContractRequest> SaveCalls => _saveCalls;

    /// <summary>All <see cref="CalculatePaymentAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<CalculatePaymentRequest> CalculateCalls => _calculateCalls;

    /// <summary>All <see cref="CloseAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<CloseContractRequest> CloseCalls => _closeCalls;

    public Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _saveCalls.Add(request);

        var result = _saveFactoryOverride is not null
            ? _saveFactoryOverride(request)
            : DefaultSaveResponse(request, _saveCalls.Count);

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

        return Task.FromResult(result);
    }

    private static IntegrationResult<SaveContractResponse> DefaultSaveResponse(SaveContractRequest request, int sequenceNumber)
    {
        var contractNumber = 1_000_000_000L + sequenceNumber;
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
}
