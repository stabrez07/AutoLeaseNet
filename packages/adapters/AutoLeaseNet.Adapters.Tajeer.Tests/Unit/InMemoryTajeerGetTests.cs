using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

/// <summary>
/// InMemory sibling coverage for <see cref="InMemoryTajeerContractClient.GetAsync"/> —
/// projects the most recent state-changing call back as a synthetic GetContractResponse.
/// This lets drift tests drive a Save→Extend→Suspend sequence and assert that a
/// subsequent Get reflects the latest vendor-side status without writing any HTTP code.
/// </summary>
public sealed class InMemoryTajeerGetTests
{
    private static SaveContractRequest SaveReq() => new()
    {
        Renter = new RenterDto
        {
            PersonAddress = "Riyadh",
            Mobile = "0501234567",
            IdTypeCode = 1,
            IdNumber = 1234567890,
        },
        PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = 100m },
        VehicleDetails = new VehicleDetailsDto { VehicleId = 4242 },
        WorkingBranchId = 1,
        RentPolicyId = 1,
        ContractStartDate = "2026-05-23T10:00",
        ContractEndDate = "2026-05-25T10:00",
        ReceiveBranchId = 1,
        ReturnBranchId = 1,
        ContractTypeCode = 1,
        OperatorId = 999,
    };

    [Fact]
    public async Task GetAsync_for_unknown_contract_returns_vendor_not_found()
    {
        var sut = new InMemoryTajeerContractClient();
        var result = await sut.GetAsync(7777L);

        result.IsSuccess.Should().BeFalse();
        result.IsTransient.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.vendor.contract.not_found");
    }

    [Fact]
    public async Task GetAsync_after_Save_reflects_Saved_status()
    {
        var sut = new InMemoryTajeerContractClient();
        var saveResult = await sut.SaveAsync(SaveReq());
        var contractNumber = saveResult.Value!.ContractNumber;

        var get = await sut.GetAsync(contractNumber);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractNumber.Should().Be(contractNumber);
        get.Value.ContractStatusCode.Should().Be(1, because: "Saved=1 per Spec 03 §7.1");
        get.Value.ExtensionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_after_Close_reflects_Closed_with_reason_codes()
    {
        var sut = new InMemoryTajeerContractClient();
        var saveResult = await sut.SaveAsync(SaveReq());
        var contractNumber = saveResult.Value!.ContractNumber;

        await sut.CloseAsync(new CloseContractRequest
        {
            ContractNumber = contractNumber,
            ClosureMainReasonCode = 1,
            ClosureSubReasonCode = 4,
            ReturnDate = "2026-05-25T10:00",
            ReturnedKm = 50,
            ReturnedFuelLevelCode = 3,
            FinalPaidAmount = 100m,
        });

        var get = await sut.GetAsync(contractNumber);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(2);
        get.Value.ClosureReasonCode.Should().Be(1);
        get.Value.ClosureSubReasonCode.Should().Be(4);
    }

    [Fact]
    public async Task GetAsync_after_Suspend_reflects_Suspended_with_reason()
    {
        var sut = new InMemoryTajeerContractClient();
        var saveResult = await sut.SaveAsync(SaveReq());
        var contractNumber = saveResult.Value!.ContractNumber;

        await sut.SuspendAsync(new SuspendContractRequest
        {
            ContractNumber = contractNumber,
            SuspensionReasonCode = 1,
            SuspendedAt = "2026-05-25T10:00",
        });

        var get = await sut.GetAsync(contractNumber);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(3);
        get.Value.SuspensionReasonCode.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_after_Extend_keeps_Issued_status_but_increments_extension_count()
    {
        var sut = new InMemoryTajeerContractClient();
        var saveResult = await sut.SaveAsync(SaveReq());
        var contractNumber = saveResult.Value!.ContractNumber;

        await sut.ExtendAsync(new ExtendContractRequest
        {
            ContractNumber = contractNumber,
            NewContractEndDate = "2026-06-01T10:00",
            AdditionalChargesAmount = 50m,
        });
        await sut.ExtendAsync(new ExtendContractRequest
        {
            ContractNumber = contractNumber,
            NewContractEndDate = "2026-06-05T10:00",
            AdditionalChargesAmount = 50m,
        });

        var get = await sut.GetAsync(contractNumber);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(4, because: "Tajeer keeps Issued (4) after extensions");
        get.Value.ExtensionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_records_each_call()
    {
        var sut = new InMemoryTajeerContractClient();
        _ = await sut.SaveAsync(SaveReq());

        await sut.GetAsync(101L);
        await sut.GetAsync(202L);
        await sut.GetAsync(101L);

        sut.GetCalls.Should().Equal(101L, 202L, 101L);
    }

    [Fact]
    public async Task GetAsync_factory_override_short_circuits_projection()
    {
        var fakeResponse = IntegrationResult<GetContractResponse>.Success(new GetContractResponse
        {
            ContractNumber = 999L,
            ContractStatusCode = 5,
        });
        var sut = new InMemoryTajeerContractClient(getFactory: _ => fakeResponse);

        var get = await sut.GetAsync(999L);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(5);
    }

    [Fact]
    public async Task SeedProjection_lets_GetAsync_return_a_state_without_a_prior_write_call()
    {
        var sut = new InMemoryTajeerContractClient();
        sut.SeedProjection(5555L, contractStatusCode: 4, extensionCount: 0);

        var get = await sut.GetAsync(5555L);

        get.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(4);
    }

    [Fact]
    public async Task GetAsync_throws_on_non_positive_contract_number()
    {
        var sut = new InMemoryTajeerContractClient();
        await FluentActions.Awaiting(() => sut.GetAsync(0L)).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => sut.GetAsync(-1L)).Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
