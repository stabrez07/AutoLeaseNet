using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// Day-20 workstream — InMemory sibling coverage for the two new methods.
/// </summary>
public sealed class InMemoryTajeerExtendAndSuspendTests
{
    private static ExtendContractRequest ExtendReq(decimal charges = 100m) => new()
    {
        ContractNumber = 6000L,
        NewContractEndDate = "2026-06-10T18:00",
        ExtensionReasonCode = 2,
        AdditionalChargesAmount = charges,
    };

    private static SuspendContractRequest SuspendReq() => new()
    {
        ContractNumber = 6000L,
        SuspensionReasonCode = 7,
        SuspendedAt = "2026-05-28T14:00",
    };

    [Fact]
    public async Task ExtendAsync_default_returns_status_code_4_and_applies_15_percent_VAT_to_charges()
    {
        var sut = new InMemoryTajeerContractClient();

        var result = await sut.ExtendAsync(ExtendReq(charges: 100m));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContractStatusCode.Should().Be(4);
        result.Value.TotalDue.Should().Be(100m);
        result.Value.VatAmount.Should().Be(15m);
        result.Value.GrandTotal.Should().Be(115m);
        sut.ExtendCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtendAsync_override_factory_simulates_vendor_failure()
    {
        var sut = new InMemoryTajeerContractClient(
            extendFactory: _ => IntegrationResult<ExtendContractResponse>.Failure(
                "tajeer.vendor.server.error.contract.max_extensions", "simulated", isTransient: false));

        var result = await sut.ExtendAsync(ExtendReq());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.max_extensions");
    }

    [Fact]
    public async Task SuspendAsync_default_returns_status_code_3_and_echoes_suspendedAt()
    {
        var sut = new InMemoryTajeerContractClient();

        var result = await sut.SuspendAsync(SuspendReq());

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContractStatusCode.Should().Be(3);
        result.Value.SuspendedAt.Should().Be("2026-05-28T14:00");
        sut.SuspendCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task SuspendAsync_override_factory_simulates_transient_failure()
    {
        var sut = new InMemoryTajeerContractClient(
            suspendFactory: _ => IntegrationResult<SuspendContractResponse>.Failure(
                "tajeer.http.503", "down", isTransient: true));

        var result = await sut.SuspendAsync(SuspendReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
    }
}
