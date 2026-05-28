using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Operations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Operations;

internal static class IncidentIdempotency
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static IdempotencyKey Key(Guid tenantId, string op, string clientKey) =>
        new($"tenant:{tenantId:N}:incident-{op}:{clientKey}");

    public static Guid RequireTenantId(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException(
                "Incident command requires an authenticated tenant context (TenancyMiddleware should have rejected this request).");
        return tenant.TenantId;
    }
}

public sealed partial class ReportIncidentCommandHandler(
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<ReportIncidentCommandHandler> logger)
    : IRequestHandler<ReportIncidentCommand, IncidentCommandResult>
{
    public async Task<IncidentCommandResult> Handle(ReportIncidentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = IncidentIdempotency.RequireTenantId(tenant);

        var idemKey = IncidentIdempotency.Key(tenantId, "report", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<IncidentCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogReplay(idemKey.Value);
            return cached;
        }

        var nowUtc = clock.UtcNow;
        Incident incident;
        try
        {
            incident = Incident.Report(new ReportIncidentInput
            {
                TenantId = tenantId,
                VehicleId = request.VehicleId,
                LeaseId = request.LeaseId,
                ReportedByPersonId = request.ReportedByPersonId,
                Type = request.Type,
                Severity = request.Severity,
                IncidentTimeUtc = request.IncidentTimeUtc,
                Description = request.Description,
                LocationLat = request.LocationLat,
                LocationLng = request.LocationLng,
                LocationDescription = request.LocationDescription,
                PoliceReportNumber = request.PoliceReportNumber,
                InsuranceClaimNumber = request.InsuranceClaimNumber,
                NowUtc = nowUtc,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("incident.invalid_input", ex.Message);
        }

        incidents.Add(incident);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = Ok(incident);
        await idempotency.SetAsync(idemKey, result, IncidentIdempotency.Ttl, ct).ConfigureAwait(false);
        LogReported(incident.Id, incident.Type.ToString(), incident.Severity.ToString());
        return result;
    }

    private static IncidentCommandResult Ok(Incident i) =>
        new(true, i.Id, i.Status, null, null);

    private static IncidentCommandResult Fail(string code, string message) =>
        new(false, null, null, code, message);

    [LoggerMessage(EventId = 8101, Level = LogLevel.Information, Message = "ReportIncident idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8102, Level = LogLevel.Information, Message = "Incident {IncidentId} reported (Type={Type}, Severity={Severity})")]
    partial void LogReported(Guid incidentId, string type, string severity);
}

public sealed partial class StartIncidentInvestigationCommandHandler(
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<StartIncidentInvestigationCommandHandler> logger)
    : IRequestHandler<StartIncidentInvestigationCommand, IncidentCommandResult>
{
    public async Task<IncidentCommandResult> Handle(StartIncidentInvestigationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = IncidentIdempotency.RequireTenantId(tenant);

        var idemKey = IncidentIdempotency.Key(tenantId, $"investigate:{request.IncidentId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<IncidentCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var incident = await incidents.GetByIdAsync(tenantId, request.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return Fail("incident.not_found", $"Incident {request.IncidentId} not found.");

        try { incident.StartInvestigation(clock.UtcNow); }
        catch (InvalidOperationException ex) { return Fail("incident.invalid_transition", ex.Message); }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = Ok(incident);
        await idempotency.SetAsync(idemKey, result, IncidentIdempotency.Ttl, ct).ConfigureAwait(false);
        LogInvestigating(incident.Id);
        return result;
    }

    private static IncidentCommandResult Ok(Incident i) => new(true, i.Id, i.Status, null, null);
    private static IncidentCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 8111, Level = LogLevel.Information, Message = "StartInvestigation idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8112, Level = LogLevel.Information, Message = "Incident {IncidentId} moved to UnderInvestigation")]
    partial void LogInvestigating(Guid incidentId);
}

public sealed partial class ResolveIncidentCommandHandler(
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<ResolveIncidentCommandHandler> logger)
    : IRequestHandler<ResolveIncidentCommand, IncidentCommandResult>
{
    public async Task<IncidentCommandResult> Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = IncidentIdempotency.RequireTenantId(tenant);

        var idemKey = IncidentIdempotency.Key(tenantId, $"resolve:{request.IncidentId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<IncidentCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var incident = await incidents.GetByIdAsync(tenantId, request.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return Fail("incident.not_found", $"Incident {request.IncidentId} not found.");

        try { incident.MarkResolved(request.ResolutionNotes, clock.UtcNow); }
        catch (ArgumentException ex) { return Fail("incident.invalid_input", ex.Message); }
        catch (InvalidOperationException ex) { return Fail("incident.invalid_transition", ex.Message); }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = Ok(incident);
        await idempotency.SetAsync(idemKey, result, IncidentIdempotency.Ttl, ct).ConfigureAwait(false);
        LogResolved(incident.Id);
        return result;
    }

    private static IncidentCommandResult Ok(Incident i) => new(true, i.Id, i.Status, null, null);
    private static IncidentCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 8121, Level = LogLevel.Information, Message = "Resolve idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8122, Level = LogLevel.Information, Message = "Incident {IncidentId} marked Resolved")]
    partial void LogResolved(Guid incidentId);
}

public sealed partial class CloseIncidentCommandHandler(
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<CloseIncidentCommandHandler> logger)
    : IRequestHandler<CloseIncidentCommand, IncidentCommandResult>
{
    public async Task<IncidentCommandResult> Handle(CloseIncidentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = IncidentIdempotency.RequireTenantId(tenant);

        var idemKey = IncidentIdempotency.Key(tenantId, $"close:{request.IncidentId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<IncidentCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var incident = await incidents.GetByIdAsync(tenantId, request.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return Fail("incident.not_found", $"Incident {request.IncidentId} not found.");

        incident.MarkClosed(clock.UtcNow); // unconditionally allowed; idempotent
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = Ok(incident);
        await idempotency.SetAsync(idemKey, result, IncidentIdempotency.Ttl, ct).ConfigureAwait(false);
        LogClosed(incident.Id);
        return result;
    }

    private static IncidentCommandResult Ok(Incident i) => new(true, i.Id, i.Status, null, null);
    private static IncidentCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 8131, Level = LogLevel.Information, Message = "Close idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8132, Level = LogLevel.Information, Message = "Incident {IncidentId} closed")]
    partial void LogClosed(Guid incidentId);
}

public sealed partial class UpdateIncidentClaimCommandHandler(
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IIdempotencyStore idempotency,
    ITenantContext tenant,
    IClock clock,
    ILogger<UpdateIncidentClaimCommandHandler> logger)
    : IRequestHandler<UpdateIncidentClaimCommand, IncidentCommandResult>
{
    public async Task<IncidentCommandResult> Handle(UpdateIncidentClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ct = cancellationToken;
        var tenantId = IncidentIdempotency.RequireTenantId(tenant);

        var idemKey = IncidentIdempotency.Key(tenantId, $"claim:{request.IncidentId:N}", request.IdempotencyKey);
        var cached = await idempotency.GetAsync<IncidentCommandResult>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null) { LogReplay(idemKey.Value); return cached; }

        var incident = await incidents.GetByIdAsync(tenantId, request.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return Fail("incident.not_found", $"Incident {request.IncidentId} not found.");

        try { incident.UpdateClaim(request.PoliceReportNumber, request.InsuranceClaimNumber, clock.UtcNow); }
        catch (InvalidOperationException ex) { return Fail("incident.immutable", ex.Message); }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var result = Ok(incident);
        await idempotency.SetAsync(idemKey, result, IncidentIdempotency.Ttl, ct).ConfigureAwait(false);
        LogClaimUpdated(incident.Id);
        return result;
    }

    private static IncidentCommandResult Ok(Incident i) => new(true, i.Id, i.Status, null, null);
    private static IncidentCommandResult Fail(string code, string message) => new(false, null, null, code, message);

    [LoggerMessage(EventId = 8141, Level = LogLevel.Information, Message = "UpdateClaim idempotency replay for key {IdempotencyKey}")]
    partial void LogReplay(string idempotencyKey);

    [LoggerMessage(EventId = 8142, Level = LogLevel.Information, Message = "Incident {IncidentId} claim numbers updated")]
    partial void LogClaimUpdated(Guid incidentId);
}
