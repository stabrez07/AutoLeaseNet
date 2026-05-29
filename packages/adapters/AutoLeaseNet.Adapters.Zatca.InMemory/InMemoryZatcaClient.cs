using System.Collections.Concurrent;
using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Zatca;
using AutoLeaseNet.Adapters.Zatca.Configuration;
using AutoLeaseNet.Adapters.Zatca.Dtos;

namespace AutoLeaseNet.Adapters.Zatca.InMemory;

/// <summary>
/// Deterministic <see cref="IZatcaClient"/> for tests and offline dev. Default behaviour:
/// every <see cref="SubmitInvoiceAsync"/> returns <see cref="ZatcaResultStatus.Cleared"/>
/// with the configured clock. Tests can override per-invoice via <see cref="SeedRejection"/>.
///
/// <para>
/// Idempotency: a repeat Submit for the same <see cref="SubmitInvoiceRequest.Uuid"/>
/// returns the originally-recorded response (same Status, same ClearedAtUtc). This
/// matches Fatoorah's real behaviour — sandbox is idempotent on UUID — and lets the
/// saga retry timeout-failed Submits without double-recording state.
/// </para>
///
/// <para>
/// Calls are recorded in <see cref="SubmitCalls"/> in insertion order; tests assert
/// on the history to verify the saga invoked the adapter at all (and only as many
/// times as expected).
/// </para>
/// </summary>
public sealed class InMemoryZatcaClient : IZatcaClient
{
    private readonly ConcurrentDictionary<Guid, ZatcaResultStatus> _seededRejections = new();
    private readonly ConcurrentDictionary<Guid, SubmitInvoiceResponse> _recorded = new();
    private readonly List<SubmitInvoiceRequest> _calls = new();
    private readonly object _callsGate = new();
    private readonly Func<DateTimeOffset> _clock;

    public InMemoryZatcaClient() : this(() => DateTimeOffset.UtcNow) { }

    public InMemoryZatcaClient(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Snapshot of every <see cref="SubmitInvoiceAsync"/> call in insertion order.</summary>
    public IReadOnlyList<SubmitInvoiceRequest> SubmitCalls
    {
        get
        {
            lock (_callsGate)
            {
                return _calls.ToList();
            }
        }
    }

    /// <summary>
    /// Pre-seed a specific <paramref name="uuid"/> so the next Submit for that UUID
    /// returns <paramref name="status"/> instead of Cleared. Re-submits for the same UUID
    /// hit the idempotency cache (so the rejection sticks).
    /// </summary>
    public void SeedRejection(Guid uuid, ZatcaResultStatus status = ZatcaResultStatus.Rejected)
    {
        if (uuid == Guid.Empty) throw new ArgumentException("Uuid required.", nameof(uuid));
        _seededRejections[uuid] = status;
    }

    public Task<IntegrationResult<SubmitInvoiceResponse>> SubmitInvoiceAsync(
        SubmitInvoiceRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        lock (_callsGate)
        {
            _calls.Add(request);
        }

        // Idempotency: same UUID → same response.
        if (_recorded.TryGetValue(request.Uuid, out var prior))
        {
            return Task.FromResult(IntegrationResult<SubmitInvoiceResponse>.Success(prior));
        }

        var status = _seededRejections.TryGetValue(request.Uuid, out var seeded)
            ? seeded
            : ZatcaResultStatus.Cleared;

        var clearedAt = status == ZatcaResultStatus.Rejected ? (DateTimeOffset?)null : _clock();
        var response = new SubmitInvoiceResponse(
            Uuid: request.Uuid,
            Status: status,
            ClearedAtUtc: clearedAt,
            Warnings: Array.Empty<string>());
        _recorded[request.Uuid] = response;
        return Task.FromResult(IntegrationResult<SubmitInvoiceResponse>.Success(response));
    }
}
