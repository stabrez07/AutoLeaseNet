using System.Globalization;
using AutoLeaseNet.Adapters.Tajeer.Contracts;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Handler for <see cref="SaveContractCommand"/>. Day-D shape — looks up domain
/// aggregates, validates them, builds the Tajeer V9.7 DTO, calls Tajeer, and persists
/// a fully-populated <see cref="Lease"/> with proper FK references.
/// <list type="number">
///   <item>Idempotency replay — same key + tenant returns the cached result.</item>
///   <item>Resolve Customer / Vehicle / Driver / RentPolicy / Branches.</item>
///   <item>Validate: vehicle Available/Reserved, driver Active, customer Active,
///        TAMM-authorised driver if delegating.</item>
///   <item>Build the Tajeer SaveContract DTO from looked-up aggregates.</item>
///   <item>Call <see cref="ITajeerContractClient.SaveAsync"/>.</item>
///   <item>On Success → reserve vehicle, persist Lease in <c>PendingIssuance</c>, cache result.</item>
///   <item>On Failure → surface the integration error (no row written, no cache entry,
///        no vehicle reservation).</item>
/// </list>
/// </summary>
public sealed partial class SaveContractCommandHandler(
    ITajeerContractClient tajeer,
    ILeaseRepository leases,
    ICustomerRepository customers,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    IRentPolicyRepository rentPolicies,
    IExtendedCoverageRepository extendedCoverages,
    IBranchRepository branches,
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<SaveContractCommandHandler> logger)
    : IRequestHandler<SaveContractCommand, SaveContractCommandResult>
{
    /// <summary>Idempotency-store TTL — 24h per Spec 03 §10 / CLAUDE.md §8.</summary>
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<SaveContractCommandResult> Handle(
        SaveContractCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = request;
        var ct = cancellationToken;

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "SaveContractCommand requires an authenticated tenant context (TenancyMiddleware should have rejected this request).");
        }

        // 1. Idempotency replay.
        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:save-contract:{command.IdempotencyKey}");
        var cached = await idempotency.GetAsync<SaveContractCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogIdempotencyReplay(idemKey.Value);
            return cached;
        }

        // 2. Resolve aggregates.
        var customer = await customers.GetByIdAsync(tenantId, command.CustomerId, ct).ConfigureAwait(false);
        if (customer is null) return BusinessError("customer.not_found", $"Customer {command.CustomerId} not found.");
        if (customer.Status != CustomerStatus.Active)
            return BusinessError("customer.not_active", $"Customer {command.CustomerId} status is {customer.Status}.");

        var vehicle = await vehicles.GetByIdAsync(tenantId, command.VehicleId, ct).ConfigureAwait(false);
        if (vehicle is null) return BusinessError("vehicle.not_found", $"Vehicle {command.VehicleId} not found.");
        if (vehicle.Status != VehicleStatus.Available && vehicle.Status != VehicleStatus.Reserved)
            return BusinessError("vehicle.not_available", $"Vehicle {command.VehicleId} status is {vehicle.Status}; must be Available or Reserved.");

        var primaryDriver = await drivers.GetByIdAsync(tenantId, command.PrimaryDriverId, ct).ConfigureAwait(false);
        if (primaryDriver is null) return BusinessError("driver.not_found", $"Driver {command.PrimaryDriverId} not found.");
        if (primaryDriver.Status != DriverStatus.Active)
            return BusinessError("driver.not_active", $"Driver {command.PrimaryDriverId} status is {primaryDriver.Status}.");
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (primaryDriver.LicenseExpiryDate < today)
            return BusinessError("driver.license_expired", $"Driver {command.PrimaryDriverId} license expired on {primaryDriver.LicenseExpiryDate:yyyy-MM-dd}.");

        var rentPolicy = await rentPolicies.GetByIdAsync(tenantId, command.RentPolicyId, ct).ConfigureAwait(false);
        if (rentPolicy is null) return BusinessError("rent_policy.not_found", $"RentPolicy {command.RentPolicyId} not found.");
        if (!rentPolicy.IsActive) return BusinessError("rent_policy.inactive", $"RentPolicy {command.RentPolicyId} is inactive.");

        var workingBranch = await branches.GetByIdAsync(tenantId, command.WorkingBranchId, ct).ConfigureAwait(false);
        if (workingBranch is null) return BusinessError("branch.not_found", $"WorkingBranch {command.WorkingBranchId} not found.");

        var receiveBranch = command.ReceiveBranchId == command.WorkingBranchId
            ? workingBranch
            : await branches.GetByIdAsync(tenantId, command.ReceiveBranchId, ct).ConfigureAwait(false);
        if (receiveBranch is null) return BusinessError("branch.not_found", $"ReceiveBranch {command.ReceiveBranchId} not found.");

        var returnBranch = command.ReturnBranchId == command.WorkingBranchId
            ? workingBranch
            : command.ReturnBranchId == command.ReceiveBranchId
                ? receiveBranch
                : await branches.GetByIdAsync(tenantId, command.ReturnBranchId, ct).ConfigureAwait(false);
        if (returnBranch is null) return BusinessError("branch.not_found", $"ReturnBranch {command.ReturnBranchId} not found.");

        AutoLeaseNet.Domain.ExtendedCoverages.ExtendedCoverage? extendedCoverage = null;
        if (command.ExtendedCoverageId is { } extId)
        {
            extendedCoverage = await extendedCoverages.GetByIdAsync(tenantId, extId, ct).ConfigureAwait(false);
            if (extendedCoverage is null) return BusinessError("extended_coverage.not_found", $"ExtendedCoverage {extId} not found.");
        }

        Driver? extraDriver = null;
        if (command.ExtraDriverId is { } eid)
        {
            extraDriver = await drivers.GetByIdAsync(tenantId, eid, ct).ConfigureAwait(false);
            if (extraDriver is null) return BusinessError("driver.not_found", $"ExtraDriver {eid} not found.");
        }

        Driver? authorizedDriver = null;
        if (command.AuthorizedDriverId is { } aid)
        {
            authorizedDriver = await drivers.GetByIdAsync(tenantId, aid, ct).ConfigureAwait(false);
            if (authorizedDriver is null) return BusinessError("driver.not_found", $"AuthorizedDriver {aid} not found.");
            if (authorizedDriver.TammAuthorizationStatus != TammAuthorizationStatus.Authorized)
                return BusinessError("driver.tamm_not_authorized",
                    $"AuthorizedDriver {aid} TAMM status is {authorizedDriver.TammAuthorizationStatus}.");
        }

        // 2b. Resolve the CHECK_OUT inspection that justifies this Lease (Day 18 / Spec 01
        //     §invariant 2). Explicit id → strict validation; omitted → best-effort
        //     auto-lookup of the most recent un-linked CHECK_OUT for this vehicle.
        //     Phase 1.x keeps the link optional; if neither path finds an inspection, the
        //     Lease is still created (the field flips to required in Phase 1.y when the
        //     web portal drives the full saga end-to-end).
        Inspection? checkOutInspection = null;
        if (command.CheckOutInspectionId is { } explicitId)
        {
            checkOutInspection = await inspections.GetByIdAsync(tenantId, explicitId, ct).ConfigureAwait(false);
            if (checkOutInspection is null)
                return BusinessError("checkout_inspection.not_found",
                    $"CHECK_OUT Inspection {explicitId} not found for tenant {tenantId}.");
            if (checkOutInspection.VehicleId != vehicle.Id)
                return BusinessError("checkout_inspection.vehicle_mismatch",
                    $"CHECK_OUT Inspection {explicitId} is for vehicle {checkOutInspection.VehicleId}, not {vehicle.Id}.");
            if (checkOutInspection.Status != InspectionStatus.Completed)
                return BusinessError("checkout_inspection.not_completed",
                    $"CHECK_OUT Inspection {explicitId} status is {checkOutInspection.Status}; must be Completed.");
            if (checkOutInspection.Type != InspectionType.CheckOut && checkOutInspection.Type != InspectionType.PreDelivery)
                return BusinessError("checkout_inspection.wrong_type",
                    $"Inspection {explicitId} type is {checkOutInspection.Type}; must be CheckOut or PreDelivery.");
            if (checkOutInspection.LeaseId is not null)
                return BusinessError("checkout_inspection.already_linked",
                    $"CHECK_OUT Inspection {explicitId} is already linked to Lease {checkOutInspection.LeaseId}.");
        }
        else
        {
            checkOutInspection = await inspections.GetLatestUnlinkedCheckOutForVehicleAsync(
                tenantId, vehicle.Id, ct).ConfigureAwait(false);
            // Phase 1.x: a missing inspection is non-fatal — Lease still gets created.
        }

        // 3. Build the Tajeer V9.7 DTO from looked-up data.
        var tajeerRequest = BuildTajeerRequest(
            command, customer, vehicle, primaryDriver, extraDriver, authorizedDriver,
            rentPolicy, extendedCoverage, workingBranch, receiveBranch, returnBranch);

        // 4. Call Tajeer.
        var integration = await tajeer.SaveAsync(tajeerRequest, ct).ConfigureAwait(false);
        if (!integration.IsSuccess || integration.Value is null)
        {
            LogTajeerFailure(integration.ErrorCode ?? "unknown", integration.IsTransient);
            return new SaveContractCommandResult(
                Success: false,
                LeaseId: null,
                TajeerContractNumber: null,
                IssuanceUrl: null,
                ErrorCode: integration.ErrorCode,
                ErrorMessage: integration.ErrorMessage,
                IsTransient: integration.IsTransient);
        }

        // 5. Persist Lease in PendingIssuance + reserve the vehicle.
        var nowUtc = clock.UtcNow;
        var payment = integration.Value.MainPaymentDetails;
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            PrimaryDriverId = primaryDriver.Id,
            ExtraDriverId = extraDriver?.Id,
            AuthorizedDriverId = authorizedDriver?.Id,
            RentPolicyId = rentPolicy.Id,
            ExtendedCoverageId = extendedCoverage?.Id,
            WorkingBranchId = workingBranch.Id,
            ReceiveBranchId = receiveBranch.Id,
            ReturnBranchId = returnBranch.Id,

            TajeerContractNumber = integration.Value.ContractNumber,
            TajeerIssuanceToken = integration.Value.Token,
            IssuanceUrl = integration.Value.IssuanceUrl,
            TajeerWorkingBranchId = workingBranch.TajeerBranchId,
            TajeerReceiveBranchId = receiveBranch.TajeerBranchId,
            TajeerReturnBranchId = returnBranch.TajeerBranchId,
            TajeerRentPolicyId = rentPolicy.TajeerRentPolicyId,
            TajeerExtendedCoverageId = extendedCoverage?.TajeerExtendedCoverageId,
            TajeerOperatorId = workingBranch.TajeerOperatorId,

            ContractTypeCode = command.ContractTypeCode,
            ContractStartUtc = command.ContractStartUtc,
            ContractEndUtc = command.ContractEndUtc,
            AllowedKmPerHour = command.AllowedKmPerHour,
            AllowedKmPerDay = command.AllowedKmPerDay,
            UnlimitedKm = command.UnlimitedKm,
            AllowedLateHours = command.AllowedLateHours,

            RentAmount = command.RentAmount,
            PaidAmount = payment.Paid,
            RemainingAmount = payment.Remaining,
            VatAmount = payment.Vat,
            TotalAmount = payment.Total,
            PaymentMethodCode = command.PaymentMethodCode,
            DiscountType = command.DiscountType,
            DiscountValue = command.DiscountValue,

            NowUtc = nowUtc,
        });
        leases.Add(lease);

        // Reserve the vehicle so concurrent SaveContract calls don't double-book it.
        if (vehicle.Status == VehicleStatus.Available)
        {
            vehicle.Reserve(nowUtc);
        }

        // Link the resolved CHECK_OUT inspection — same UoW, same transaction.
        checkOutInspection?.LinkToLease(lease.Id, nowUtc);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = new SaveContractCommandResult(
            Success: true,
            LeaseId: lease.Id,
            TajeerContractNumber: integration.Value.ContractNumber,
            IssuanceUrl: integration.Value.IssuanceUrl,
            ErrorCode: null,
            ErrorMessage: null,
            IsTransient: false);

        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, ct).ConfigureAwait(false);
        LogLeaseSaved(lease.Id, integration.Value.ContractNumber);
        return result;
    }

    private static SaveContractRequest BuildTajeerRequest(
        SaveContractCommand command,
        Customer customer,
        Vehicle vehicle,
        Driver primaryDriver,
        Driver? extraDriver,
        Driver? authorizedDriver,
        AutoLeaseNet.Domain.RentPolicies.RentPolicy rentPolicy,
        AutoLeaseNet.Domain.ExtendedCoverages.ExtendedCoverage? extendedCoverage,
        AutoLeaseNet.Domain.Branches.Branch workingBranch,
        AutoLeaseNet.Domain.Branches.Branch receiveBranch,
        AutoLeaseNet.Domain.Branches.Branch returnBranch)
    {
        return new SaveContractRequest
        {
            Renter = new RenterDto
            {
                PersonAddress = customer.NationalAddress ?? primaryDriver.NationalAddress ?? "N/A",
                Email = customer.Email,
                Mobile = customer.Mobile ?? primaryDriver.Mobile ?? "0500000000",
                IdTypeCode = customer.IdTypeCode ?? primaryDriver.IdTypeCode,
                IdNumber = long.Parse(customer.PersonIdNumber ?? primaryDriver.PersonIdNumber, CultureInfo.InvariantCulture),
                DriveLicenseNumber = primaryDriver.DriverLicenseNumber,
                LicenseExpiryDate = primaryDriver.LicenseExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NationalityCode = primaryDriver.NationalityCode is { Length: > 0 } nc ? CountryCodeToTajeer(nc) : null,
            },
            PaymentDetails = new PaymentDetailsDto
            {
                PaymentMethodCode = command.PaymentMethodCode,
                RentAmount = command.RentAmount,
                PaidAmount = command.PaidAmount,
                DiscountType = command.DiscountType,
                DiscountValue = command.DiscountValue,
            },
            VehicleDetails = new VehicleDetailsDto
            {
                PlateNumber = vehicle.PlateNumber,
                PlateLetters = vehicle.PlateLetters,
                PlateTypeCode = vehicle.PlateTypeCode,
                CurrentKm = vehicle.CurrentKm,
            },
            ExtraDriver = extraDriver is null ? null : new ExtraDriverDto
            {
                IdNumber = long.Parse(extraDriver.PersonIdNumber, CultureInfo.InvariantCulture),
                DriveLicenseNumber = extraDriver.DriverLicenseNumber,
            },
            AuthorizedDriver = authorizedDriver is null ? null : new AuthorizedDriverDto
            {
                IdNumber = long.Parse(authorizedDriver.PersonIdNumber, CultureInfo.InvariantCulture),
                DriveLicenseNumber = authorizedDriver.DriverLicenseNumber,
            },
            ExtendedCoverageId = extendedCoverage?.TajeerExtendedCoverageId,
            WorkingBranchId = workingBranch.TajeerBranchId,
            RentPolicyId = rentPolicy.TajeerRentPolicyId,
            ContractStartDate = command.ContractStartUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ContractEndDate = command.ContractEndUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            AllowedKmPerHour = command.AllowedKmPerHour,
            AllowedKmPerDay = command.AllowedKmPerDay,
            UnlimitedKm = command.UnlimitedKm,
            ReceiveBranchId = receiveBranch.TajeerBranchId,
            ReturnBranchId = returnBranch.TajeerBranchId,
            ContractTypeCode = command.ContractTypeCode,
            AllowedLateHours = command.AllowedLateHours,
            OperatorId = workingBranch.TajeerOperatorId,
        };
    }

    // Tajeer uses numeric country codes; we keep ISO-2 internally. Phase-1 only handles
    // KSA + GCC; an expanded mapping arrives with the Nationalities lookup table.
    private static int? CountryCodeToTajeer(string iso2) => iso2.ToUpperInvariant() switch
    {
        "SA" => 1,
        "AE" => 2,
        "KW" => 3,
        "BH" => 4,
        "QA" => 5,
        "OM" => 6,
        "EG" => 20,
        _ => null,
    };

    private static SaveContractCommandResult BusinessError(string code, string message)
        => new(Success: false, LeaseId: null, TajeerContractNumber: null, IssuanceUrl: null,
               ErrorCode: $"lease.{code}", ErrorMessage: message, IsTransient: false);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "SaveContract idempotency replay for key {IdempotencyKey}")]
    partial void LogIdempotencyReplay(string idempotencyKey);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning,
        Message = "Tajeer SaveContract returned failure {ErrorCode} (transient={IsTransient})")]
    partial void LogTajeerFailure(string errorCode, bool isTransient);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information,
        Message = "Lease {LeaseId} saved in PendingIssuance with Tajeer contract {TajeerContractNumber}")]
    partial void LogLeaseSaved(Guid leaseId, long tajeerContractNumber);
}
