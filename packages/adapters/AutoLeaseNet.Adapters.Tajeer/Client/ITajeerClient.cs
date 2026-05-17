namespace AutoLeaseNet.Adapters.Tajeer.Client;

/// <summary>
/// Root Tajeer client interface. Per doc 03 §5.1.
/// </summary>
public interface ITajeerClient
{
    ITajeerContracts Contracts { get; }
    ITajeerLookups Lookups { get; }
    ITajeerWebhookRegistration Webhooks { get; }
    ITajeerExecution Execution { get; }
}

/// <summary>
/// Contract lifecycle operations per doc 03 §5.2.
/// Implementations of all methods will be added incrementally per workstream.
/// </summary>
public interface ITajeerContracts
{
    // Placeholder marker — actual methods (SaveAsync, GetAsync, CloseAsync, ExtendAsync, etc.)
    // will be added with their request/response DTOs as each workstream lands in Phase 1.
}

public interface ITajeerLookups
{
    // Placeholder for GetAllRentPoliciesAsync, GetAllBranchesAsync, GetAllExtendedCoveragesAsync, etc.
}

public interface ITajeerWebhookRegistration
{
    // Placeholder for RegisterAsync
}

public interface ITajeerExecution
{
    // Placeholder for GetExecutionStatusAsync (MOJ status check per spec §6.15)
}
