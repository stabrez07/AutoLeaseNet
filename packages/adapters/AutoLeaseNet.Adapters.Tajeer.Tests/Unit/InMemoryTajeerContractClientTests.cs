using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// T4.7 — InMemory sibling for <see cref="ITajeerContractClient"/>. Covers the contract
/// the real client must also satisfy: idempotent return shape, request capture, and
/// pluggable failure injection. The Mode-switch wiring is asserted separately in
/// <see cref="TajeerModeRegistrationTests"/>.
/// </summary>
public sealed class InMemoryTajeerContractClientTests
{
    private static SaveContractRequest MinimalRequest(decimal rent = 200m, decimal paid = 50m) => new()
    {
        Renter = new RenterDto { PersonAddress = "Jeddah", Mobile = "0501112222", IdTypeCode = 1, IdNumber = 1 },
        PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = rent, PaidAmount = paid },
        VehicleDetails = new VehicleDetailsDto { VehicleId = 7 },
        WorkingBranchId = 1,
        RentPolicyId = 1,
        ContractStartDate = "2026-05-23T10:00",
        ContractEndDate = "2026-05-25T10:00",
        ReceiveBranchId = 1,
        ReturnBranchId = 1,
        ContractTypeCode = 1,
        OperatorId = 99,
    };

    [Fact]
    public async Task SaveAsync_default_factory_returns_deterministic_success_shape()
    {
        var sut = new InMemoryTajeerContractClient();

        var result = await sut.SaveAsync(MinimalRequest(rent: 100m, paid: 30m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ContractNumber.Should().BeGreaterThan(0);
        result.Value.IssuanceUrl.Should().StartWith("https://inmemory.tajeer.local/#/public-contract/");
        result.Value.MainPaymentDetails.Total.Should().Be(100m);
        result.Value.MainPaymentDetails.Paid.Should().Be(30m);
        result.Value.MainPaymentDetails.Remaining.Should().Be(70m);
        result.Value.MainPaymentDetails.Vat.Should().Be(15m, because: "default factory applies 15% VAT for the dev stub");
    }

    [Fact]
    public async Task SaveAsync_captures_each_call_in_order()
    {
        var sut = new InMemoryTajeerContractClient();

        await sut.SaveAsync(MinimalRequest(rent: 100m));
        await sut.SaveAsync(MinimalRequest(rent: 200m));

        sut.SaveCalls.Should().HaveCount(2);
        sut.SaveCalls[0].PaymentDetails.RentAmount.Should().Be(100m);
        sut.SaveCalls[1].PaymentDetails.RentAmount.Should().Be(200m);
    }

    [Fact]
    public async Task SaveAsync_override_factory_can_simulate_vendor_business_error()
    {
        var sut = new InMemoryTajeerContractClient(
            _ => IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.vendor.server.error.renter.mobile.invalid",
                errorMessage: "Mobile invalid (simulated)",
                isTransient: false));

        var result = await sut.SaveAsync(MinimalRequest());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.renter.mobile.invalid");
        sut.SaveCalls.Should().HaveCount(1, because: "the call is still observed even when the simulated response is a failure");
    }

    [Fact]
    public async Task SaveAsync_throws_on_null_request()
    {
        var sut = new InMemoryTajeerContractClient();

        var act = () => sut.SaveAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
