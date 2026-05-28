using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// Workstream 2026-05-28-tajeer-close-saga — InMemory sibling coverage for the two new
/// methods. Asserts the default factory's deterministic shape, override-injection, and
/// call recording.
/// </summary>
public sealed class InMemoryTajeerCalculateAndCloseTests
{
    private static CalculatePaymentRequest CalcReq(int extraKm = 50, decimal damages = 80m, decimal discount = 20m) => new()
    {
        ContractNumber = 5000L,
        ReturnDate = "2026-05-28T14:00",
        ReturnedKm = 50320,
        ReturnedFuelLevelCode = 3,
        ExtraKm = extraKm,
        AdditionalCharges = damages,
        DiscountAmount = discount,
    };

    private static CloseContractRequest CloseReq(decimal finalPaid = 100m) => new()
    {
        ContractNumber = 5000L,
        ClosureMainReasonCode = 1,
        ReturnDate = "2026-05-28T14:00",
        ReturnedKm = 50320,
        ReturnedFuelLevelCode = 3,
        FinalPaidAmount = finalPaid,
    };

    [Fact]
    public async Task CalculatePaymentAsync_default_returns_deterministic_success_shape()
    {
        var sut = new InMemoryTajeerContractClient();

        var result = await sut.CalculatePaymentAsync(CalcReq(extraKm: 50, damages: 80m, discount: 20m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ContractNumber.Should().Be(5000L);
        result.Value.ExtraKmFee.Should().Be(25m, because: "default applies a flat SAR 0.50 / extra km");
        result.Value.DamagesFee.Should().Be(80m);
        result.Value.DiscountAmount.Should().Be(20m);
        result.Value.TotalDue.Should().Be(85m, because: "25 + 80 - 20 = 85");
        result.Value.VatAmount.Should().Be(12.75m, because: "15% of 85");
        result.Value.GrandTotal.Should().Be(97.75m);
    }

    [Fact]
    public async Task CalculatePaymentAsync_captures_each_call_in_order()
    {
        var sut = new InMemoryTajeerContractClient();

        await sut.CalculatePaymentAsync(CalcReq(extraKm: 10));
        await sut.CalculatePaymentAsync(CalcReq(extraKm: 20));

        sut.CalculateCalls.Should().HaveCount(2);
        sut.CalculateCalls[0].ExtraKm.Should().Be(10);
        sut.CalculateCalls[1].ExtraKm.Should().Be(20);
    }

    [Fact]
    public async Task CalculatePaymentAsync_override_factory_simulates_vendor_failure()
    {
        var sut = new InMemoryTajeerContractClient(
            calculateFactory: _ => IntegrationResult<CalculatePaymentResponse>.Failure(
                errorCode: "tajeer.vendor.server.error.contract.not_active",
                errorMessage: "Simulated",
                isTransient: false));

        var result = await sut.CalculatePaymentAsync(CalcReq());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.contract.not_active");
        sut.CalculateCalls.Should().HaveCount(1, because: "the call is still observed when the simulated response is a failure");
    }

    [Fact]
    public async Task CloseAsync_default_returns_status_code_2_and_echoes_finalPaidAmount()
    {
        var sut = new InMemoryTajeerContractClient();

        var result = await sut.CloseAsync(CloseReq(finalPaid: 615.25m));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContractStatusCode.Should().Be(2);
        result.Value.FinalPaidAmount.Should().Be(615.25m);
        result.Value.ClosedAt.Should().Be("2026-05-28T14:00");
    }

    [Fact]
    public async Task CloseAsync_override_factory_simulates_transient_failure()
    {
        var sut = new InMemoryTajeerContractClient(
            closeFactory: _ => IntegrationResult<CloseContractResponse>.Failure(
                errorCode: "tajeer.http.503", errorMessage: "down", isTransient: true));

        var result = await sut.CloseAsync(CloseReq());

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeTrue();
        sut.CloseCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Save_Calculate_Close_overrides_are_independent_when_partially_supplied()
    {
        // Only override Calculate to fail; Save + Close stay on defaults.
        var sut = new InMemoryTajeerContractClient(
            calculateFactory: _ => IntegrationResult<CalculatePaymentResponse>.Failure(
                errorCode: "x", errorMessage: "x", isTransient: false));

        var closeOk = await sut.CloseAsync(CloseReq());
        var calcFail = await sut.CalculatePaymentAsync(CalcReq());

        closeOk.IsSuccess.Should().BeTrue();
        calcFail.IsSuccess.Should().BeFalse();
    }
}
