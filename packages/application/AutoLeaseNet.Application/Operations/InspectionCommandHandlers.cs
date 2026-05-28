using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Operations;

internal static class InspectionIdempotency
{
    /// <summary>24h TTL matches the BFF idempotency contract (Spec 03 §10 / CLAUDE.md §8).</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static IdempotencyKey Key(Guid tenantId, string op, string clientKey) =>
        new($"tenant:{tenantId:N}:inspection-{op}:{clientKey}");

    public static Guid RequireTenantId(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException(
                "Inspection command requires an authenticated tenant context (TenancyMiddleware should have rejected this request).");
        return tenant.TenantId;
    }
}

/// <summary>
/// Creates a new <see cref="Inspection"/> in <see cref="InspectionStatus.InProgress"/>,
/// optionally seeding photos + damage markers if the client included them in the request.
/// </summary>
public sealed partial class StartInspectionCommandHandler(
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<StartInspectionCommandHandler> logger)
    : IRequestHandler<StartInspectionCommand, InspectionCommandResult>
{
    public async Task<InspectionCommandResult> Handle(StartInspectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionIdempotency.RequireTenantId(tenant);

        var idemKey = InspectionIdempotency.Key(tenantId, "start", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<InspectionCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var nowUtc = clock.UtcNow;
        Inspection inspection;
        try
        {
            inspection = Inspection.Start(new StartInspectionInput
            {
                TenantId = tenantId,
                VehicleId = request.VehicleId,
                LeaseId = request.LeaseId,
                Type = request.Type,
                PerformedByUserId = tenant.UserId ?? Guid.Empty,
                OdometerKm = request.OdometerKm,
                FuelLevel = request.FuelLevel,
                AcCondition = request.AcCondition,
                RadioStereoCondition = request.RadioStereoCondition,
                ScreenCondition = request.ScreenCondition,
                SpeedometerCondition = request.SpeedometerCondition,
                KeysCondition = request.KeysCondition,
                CarSeatsCondition = request.CarSeatsCondition,
                SafetyTriangleCondition = request.SafetyTriangleCondition,
                FireExtinguisherCondition = request.FireExtinguisherCondition,
                FirstAidKitCondition = request.FirstAidKitCondition,
                SpareTireToolsCondition = request.SpareTireToolsCondition,
                TiresCondition = request.TiresCondition,
                SpareTireCondition = request.SpareTireCondition,
                Other1 = request.Other1,
                Other2 = request.Other2,
                Notes = request.Notes,
                SketchInfoJson = request.SketchInfoJson,
                RenterSignatureBlobUri = request.RenterSignatureBlobUri,
                NowUtc = nowUtc,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("inspection.invalid_input", ex.Message);
        }

        if (request.InitialPhotos is { Count: > 0 } photos)
        {
            var seq = 1;
            foreach (var uri in photos)
            {
                try { inspection.AddPhoto(uri, seq++, nowUtc); }
                catch (ArgumentException ex) { return Fail("inspection.invalid_input", ex.Message); }
            }
        }

        if (request.InitialDamageMarkers is { Count: > 0 } markers)
        {
            foreach (var m in markers)
            {
                try { inspection.AddDamageMarker(m.Type, m.PositionX, m.PositionY, nowUtc); }
                catch (ArgumentOutOfRangeException ex) { return Fail("inspection.marker_out_of_canvas", ex.Message); }
            }
        }

        inspections.Add(inspection);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = Ok(inspection);
        await idempotency.SetAsync(idemKey, result, InspectionIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        LogStarted(inspection.Id, inspection.Type.ToString());
        return result;
    }

    private static InspectionCommandResult Ok(Inspection i) =>
        new(Success: true, InspectionId: i.Id, Status: i.Status, ErrorCode: null, ErrorMessage: null);

    private static InspectionCommandResult Fail(string code, string message) =>
        new(Success: false, InspectionId: null, Status: null, ErrorCode: code, ErrorMessage: message);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Inspection start idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information, Message = "Inspection {InspectionId} started (Type={Type})")]
    partial void LogStarted(Guid inspectionId, string type);
}

/// <summary>Adds one photo to an IN_PROGRESS inspection.</summary>
public sealed partial class AddInspectionPhotoCommandHandler(
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<AddInspectionPhotoCommandHandler> logger)
    : IRequestHandler<AddInspectionPhotoCommand, InspectionCommandResult>
{
    public async Task<InspectionCommandResult> Handle(AddInspectionPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionIdempotency.RequireTenantId(tenant);

        var idemKey = InspectionIdempotency.Key(tenantId, $"add-photo:{request.InspectionId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<InspectionCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var inspection = await inspections.GetByIdAsync(tenantId, request.InspectionId, cancellationToken).ConfigureAwait(false);
        if (inspection is null) return Fail("inspection.not_found", $"Inspection {request.InspectionId} not found.");

        try { inspection.AddPhoto(request.BlobUri, request.Sequence, clock.UtcNow); }
        catch (ArgumentException ex) { return Fail("inspection.invalid_input", ex.Message); }
        catch (InvalidOperationException ex) { return Fail("inspection.immutable", ex.Message); }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = new InspectionCommandResult(true, inspection.Id, inspection.Status, null, null);
        await idempotency.SetAsync(idemKey, result, InspectionIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static InspectionCommandResult Fail(string code, string message) =>
        new(false, null, null, code, message);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Inspection add-photo idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

/// <summary>Adds one damage marker to an IN_PROGRESS inspection.</summary>
public sealed partial class AddDamageMarkerCommandHandler(
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<AddDamageMarkerCommandHandler> logger)
    : IRequestHandler<AddDamageMarkerCommand, InspectionCommandResult>
{
    public async Task<InspectionCommandResult> Handle(AddDamageMarkerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionIdempotency.RequireTenantId(tenant);

        var idemKey = InspectionIdempotency.Key(tenantId, $"add-marker:{request.InspectionId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<InspectionCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var inspection = await inspections.GetByIdAsync(tenantId, request.InspectionId, cancellationToken).ConfigureAwait(false);
        if (inspection is null) return Fail("inspection.not_found", $"Inspection {request.InspectionId} not found.");

        try { inspection.AddDamageMarker(request.Type, request.PositionX, request.PositionY, clock.UtcNow); }
        catch (ArgumentOutOfRangeException ex) { return Fail("inspection.marker_out_of_canvas", ex.Message); }
        catch (InvalidOperationException ex) { return Fail("inspection.immutable", ex.Message); }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = new InspectionCommandResult(true, inspection.Id, inspection.Status, null, null);
        await idempotency.SetAsync(idemKey, result, InspectionIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static InspectionCommandResult Fail(string code, string message) =>
        new(false, null, null, code, message);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Information, Message = "Inspection add-marker idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}

/// <summary>Transitions IN_PROGRESS → COMPLETED. Domain raises <c>InspectionCompletedDomainEvent</c>.</summary>
public sealed partial class CompleteInspectionCommandHandler(
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CompleteInspectionCommandHandler> logger)
    : IRequestHandler<CompleteInspectionCommand, InspectionCommandResult>
{
    public async Task<InspectionCommandResult> Handle(CompleteInspectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionIdempotency.RequireTenantId(tenant);

        var idemKey = InspectionIdempotency.Key(tenantId, $"complete:{request.InspectionId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<InspectionCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var inspection = await inspections.GetByIdAsync(tenantId, request.InspectionId, cancellationToken).ConfigureAwait(false);
        if (inspection is null) return Fail("inspection.not_found", $"Inspection {request.InspectionId} not found.");

        try { inspection.Complete(clock.UtcNow); }
        catch (InvalidOperationException ex) { return Fail("inspection.illegal_transition", ex.Message); }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = new InspectionCommandResult(true, inspection.Id, inspection.Status, null, null);
        await idempotency.SetAsync(idemKey, result, InspectionIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        LogCompleted(inspection.Id);
        return result;
    }

    private static InspectionCommandResult Fail(string code, string message) =>
        new(false, null, null, code, message);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Information, Message = "Inspection complete idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Inspection {InspectionId} completed")]
    partial void LogCompleted(Guid inspectionId);
}

/// <summary>Transitions IN_PROGRESS → ABANDONED with a captured reason.</summary>
public sealed partial class AbandonInspectionCommandHandler(
    IInspectionRepository inspections,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<AbandonInspectionCommandHandler> logger)
    : IRequestHandler<AbandonInspectionCommand, InspectionCommandResult>
{
    public async Task<InspectionCommandResult> Handle(AbandonInspectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = InspectionIdempotency.RequireTenantId(tenant);

        var idemKey = InspectionIdempotency.Key(tenantId, $"abandon:{request.InspectionId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<InspectionCommandResult>(idemKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var inspection = await inspections.GetByIdAsync(tenantId, request.InspectionId, cancellationToken).ConfigureAwait(false);
        if (inspection is null) return Fail("inspection.not_found", $"Inspection {request.InspectionId} not found.");

        try { inspection.Abandon(request.Reason, clock.UtcNow); }
        catch (ArgumentException ex) { return Fail("inspection.invalid_input", ex.Message); }
        catch (InvalidOperationException ex) { return Fail("inspection.illegal_transition", ex.Message); }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var result = new InspectionCommandResult(true, inspection.Id, inspection.Status, null, null);
        await idempotency.SetAsync(idemKey, result, InspectionIdempotency.Ttl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static InspectionCommandResult Fail(string code, string message) =>
        new(false, null, null, code, message);

    [LoggerMessage(EventId = 8007, Level = LogLevel.Information, Message = "Inspection abandon idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);
}
