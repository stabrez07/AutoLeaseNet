using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Drivers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Drivers;

public sealed partial class CreateDriverCommandHandler(
    IDriverRepository drivers,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateDriverCommandHandler> logger)
    : IRequestHandler<CreateDriverCommand, DriverCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<DriverCommandResult> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("driver.idempotency_required", "CreateDriver requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:driver-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<DriverCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        if (!DateOnly.TryParseExact(request.LicenseExpiryDate, "yyyy-MM-dd", out var licenseExpiry))
            return Fail("driver.invalid_license_expiry", "LicenseExpiryDate must be in YYYY-MM-DD format.");

        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (!DateOnly.TryParseExact(request.DateOfBirth, "yyyy-MM-dd", out var parsedDob))
                return Fail("driver.invalid_dob", "DateOfBirth must be in YYYY-MM-DD format.");
            dob = parsedDob;
        }

        Driver driver;
        try
        {
            driver = Driver.Create(new DriverCreateInput
            {
                TenantId = tenantId,
                CustomerId = request.CustomerId,
                PersonNameEn = request.PersonNameEn,
                PersonNameAr = request.PersonNameAr,
                IdTypeCode = request.IdTypeCode,
                PersonIdNumber = request.PersonIdNumber,
                DateOfBirth = dob,
                NationalityCode = request.NationalityCode,
                DriverLicenseNumber = request.DriverLicenseNumber,
                LicenseClass = request.LicenseClass,
                LicenseExpiryDate = licenseExpiry,
                Mobile = request.Mobile,
                Email = request.Email,
                NationalAddress = request.NationalAddress,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("driver.invalid_input", ex.Message);
        }

        drivers.Add(driver);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new DriverCommandResult(true, driver.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(driver.Id, tenantId);
        return result;
    }

    private static DriverCommandResult Fail(string code, string message) => new(false, null, code, message);

    [LoggerMessage(EventId = 9701, Level = LogLevel.Information,
        Message = "Driver {DriverId} created for tenant {TenantId}")]
    partial void LogCreated(Guid driverId, Guid tenantId);

    [LoggerMessage(EventId = 9702, Level = LogLevel.Debug,
        Message = "CreateDriver idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
