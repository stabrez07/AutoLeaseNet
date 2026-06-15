using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Adapters.Tajeer.Tests.Unit;

public sealed class InMemoryTajeerCancelTests
{
    [Fact]
    public async Task CancelAsync_default_returns_success_and_marks_projection_cancelled()
    {
        var sut = new InMemoryTajeerContractClient();
        var save = await sut.SaveAsync(new SaveContractRequest
        {
            Renter = new RenterDto { PersonAddress = "Riyadh", Mobile = "050", IdTypeCode = 1, IdNumber = 1 },
            PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = 100m },
            VehicleDetails = new VehicleDetailsDto { VehicleId = 1 },
            WorkingBranchId = 1,
            RentPolicyId = 1,
            ContractStartDate = "2026-05-01T10:00",
            ContractEndDate = "2026-05-02T10:00",
            ReceiveBranchId = 1,
            ReturnBranchId = 1,
            ContractTypeCode = 1,
            OperatorId = 1,
        });

        var result = await sut.CancelAsync(new CancelContractRequest { ContractNumber = save.Value!.ContractNumber });
        var get = await sut.GetAsync(save.Value!.ContractNumber);

        result.IsSuccess.Should().BeTrue();
        get.Value!.ContractStatusCode.Should().Be(5);
        sut.CancelCalls.Should().ContainSingle();
    }
}
