using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Branches;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Branches;

public sealed partial class CreateBranchCommandHandler(
    IBranchRepository branches,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CreateBranchCommandHandler> logger)
    : IRequestHandler<CreateBranchCommand, BranchCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<BranchCommandResult> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("branch.idempotency_required", "CreateBranch requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:branch-create:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<BranchCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        Branch branch;
        try
        {
            branch = Branch.Create(new BranchCreateInput
            {
                TenantId = tenantId,
                Code = request.Code,
                NameEn = request.NameEn,
                NameAr = request.NameAr,
                CityEn = request.CityEn,
                CityAr = request.CityAr,
                RegionEn = request.RegionEn,
                RegionAr = request.RegionAr,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                LicenseNumber = request.LicenseNumber,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                TajeerBranchId = request.TajeerBranchId,
                TajeerOperatorId = request.TajeerOperatorId,
                NowUtc = clock.UtcNow,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("branch.invalid_input", ex.Message);
        }

        branches.Add(branch);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new BranchCommandResult(true, branch.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogCreated(branch.Id, tenantId);
        return result;
    }

    private static BranchCommandResult Fail(string code, string message) => new(false, null, code, message);

    [LoggerMessage(EventId = 9801, Level = LogLevel.Information,
        Message = "Branch {BranchId} created for tenant {TenantId}")]
    partial void LogCreated(Guid branchId, Guid tenantId);

    [LoggerMessage(EventId = 9802, Level = LogLevel.Debug,
        Message = "CreateBranch idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

public sealed partial class UpdateBranchStatusCommandHandler(
    IBranchRepository branches,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateBranchStatusCommandHandler> logger)
    : IRequestHandler<UpdateBranchStatusCommand, BranchCommandResult>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<BranchCommandResult> Handle(UpdateBranchStatusCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Fail("branch.idempotency_required", "UpdateBranchStatus requires an Idempotency-Key.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Fail("tenant.required", "Tenant context required.");

        var action = request.Activate ? "activate" : "deactivate";
        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:branch-status:{request.BranchId:N}:{action}:{request.IdempotencyKey}");
        var cached = await idempotency.GetAsync<BranchCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var branch = await branches.GetByIdAsync(tenantId, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (branch is null)
            return Fail("branch.not_found", $"Branch {request.BranchId} not found.");

        if (request.Activate)
            branch.Activate(clock.UtcNow);
        else
            branch.Deactivate(clock.UtcNow);

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new BranchCommandResult(true, branch.Id, null, null);
        await idempotency.SetAsync(idemKey, result, IdempotencyTtl, cancellationToken).ConfigureAwait(false);
        LogUpdated(branch.Id, action, tenantId);
        return result;
    }

    private static BranchCommandResult Fail(string code, string message) => new(false, null, code, message);

    [LoggerMessage(EventId = 9803, Level = LogLevel.Information,
        Message = "Branch {BranchId} {Action} for tenant {TenantId}")]
    partial void LogUpdated(Guid branchId, string action, Guid tenantId);

    [LoggerMessage(EventId = 9804, Level = LogLevel.Debug,
        Message = "UpdateBranchStatus idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
