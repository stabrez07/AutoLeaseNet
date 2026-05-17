using AutoLeaseNet.Adapters.Tajeer.Client;

namespace AutoLeaseNet.Adapters.Tajeer.InMemory;

/// <summary>
/// In-memory implementation of ITajeerClient — captures calls and returns canned responses.
/// Used for unit tests and offline dev. Per doc 04 §8.
/// </summary>
public sealed class InMemoryTajeerClient : ITajeerClient
{
    public ITajeerContracts Contracts { get; } = new InMemoryContracts();
    public ITajeerLookups Lookups { get; } = new InMemoryLookups();
    public ITajeerWebhookRegistration Webhooks { get; } = new InMemoryWebhooks();
    public ITajeerExecution Execution { get; } = new InMemoryExecution();

    private sealed class InMemoryContracts : ITajeerContracts;
    private sealed class InMemoryLookups : ITajeerLookups;
    private sealed class InMemoryWebhooks : ITajeerWebhookRegistration;
    private sealed class InMemoryExecution : ITajeerExecution;
}
