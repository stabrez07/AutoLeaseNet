using FluentAssertions;
using AutoLeaseNet.Adapters.Tajeer.Client;
using AutoLeaseNet.Adapters.Tajeer.InMemory;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// Smoke test that the InMemory client wires up. Real test coverage will be added per
/// workstream as ITajeerContracts/Lookups/Webhooks/Execution methods are implemented.
/// </summary>
public sealed class InMemoryTajeerClientTests
{
    [Fact]
    public void InMemoryClient_exposes_all_sub_interfaces()
    {
        ITajeerClient client = new InMemoryTajeerClient();

        client.Contracts.Should().NotBeNull();
        client.Lookups.Should().NotBeNull();
        client.Webhooks.Should().NotBeNull();
        client.Execution.Should().NotBeNull();
    }
}
