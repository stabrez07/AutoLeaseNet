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
    private readonly List<SaveContractRequest> _saveCalls = new();

    /// <summary>Default factory — returns a deterministic success response.</summary>
    public InMemoryTajeerContractClient()
    {
        _saveFactoryOverride = null;
    }

    /// <summary>Override the response per request for negative-path tests.</summary>
    public InMemoryTajeerContractClient(
        Func<SaveContractRequest, IntegrationResult<SaveContractResponse>> saveFactory)
    {
        _saveFactoryOverride = saveFactory ?? throw new ArgumentNullException(nameof(saveFactory));
    }

    /// <summary>All <see cref="SaveAsync"/> calls observed since construction.</summary>
    public IReadOnlyList<SaveContractRequest> SaveCalls => _saveCalls;

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
}
