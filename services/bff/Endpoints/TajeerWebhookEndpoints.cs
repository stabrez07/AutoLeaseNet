using System.Net;
using System.Text.Json;
using AutoLeaseNet.Adapters.Tajeer.Configuration;
using AutoLeaseNet.Adapters.Tajeer.Webhooks;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Day 6 part 2 — inbound Tajeer webhook receiver. Anonymous (Tajeer doesn't carry a JWT)
/// but authenticated by the <c>secret-key</c> header per Spec 03 §12.2. Acks <c>200 OK</c>
/// quickly so Tajeer doesn't retry; dispatches inline for Phase 1 (a BackgroundService
/// drain pattern arrives later when volume justifies it).
/// </summary>
public static class TajeerWebhookEndpoints
{
    public const string Source = "TAJEER";

    // Phase-1 fallback tenant for webhooks that arrive before any Lease has been saved
    // (test events from Tajeer). Phase 2 will encode tenant in the registered URL.
    private static readonly Guid Phase1FallbackTenantId =
        Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapTajeerWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/webhooks/tajeer").WithTags("webhooks");

        group.MapPost("/", HandleAsync)
            .AllowAnonymous()
            .WithName("TajeerWebhookReceive")
            .WithSummary("Receives Tajeer notifications; validates secret-key, persists WebhookLog, dispatches issuance events.");

        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        IOptions<TajeerWebhookOptions> webhookOptions,
        IOptions<TajeerOptions> tajeerOptions,
        IWebhookLogRepository webhookLogs,
        ILeaseRepository leases,
        IUnitOfWork uow,
        IClock clock,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("TajeerWebhook");
        var webhook = webhookOptions.Value;
        var nowUtc = clock.UtcNow;

        // 1. Read raw body + secret-key header (validated regardless of log-only).
        var receivedSecret = ctx.Request.Headers["secret-key"].ToString();
        ctx.Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(ctx.Request.Body, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        ctx.Request.Body.Position = 0;

        var sigValid = WebhookSignatureValidator.IsValid(receivedSecret, tajeerOptions.Value.WebhookSharedSecret);
        if (!sigValid && !webhook.LogOnly)
        {
            WebhookLogMessages.SignatureRejected(logger, ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }
        if (!sigValid && webhook.LogOnly)
        {
            WebhookLogMessages.SignatureMismatchLogOnly(logger, ctx.Connection.RemoteIpAddress);
        }

        // 2. Parse payload.
        TajeerWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TajeerWebhookPayload>(rawBody, JsonOpts);
        }
        catch (JsonException ex)
        {
            WebhookLogMessages.MalformedBody(logger, ex);
            return Results.Problem(title: "webhook.body.malformed",
                detail: "Body must be a JSON object matching the Tajeer §12.1 webhook shape.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Type))
        {
            return Results.Problem(title: "webhook.body.missing_required",
                detail: "Body must include non-empty 'id' and 'type'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // 3. Resolve owning tenant via the contract number on the local Lease.
        Domain.Leases.Lease? lease = null;
        Guid tenantId = Guid.Empty;
        if (long.TryParse(payload.ReferenceId, out var tajeerContractNumber))
        {
            lease = await leases.GetByTajeerContractNumberAcrossTenantsAsync(tajeerContractNumber, ct)
                .ConfigureAwait(false);
            if (lease is not null) tenantId = lease.TenantId;
        }
        if (tenantId == Guid.Empty)
        {
            tenantId = Phase1FallbackTenantId;
        }

        // 4. Dedup probe.
        if (await webhookLogs.ExistsAsync(Source, payload.Id, ct).ConfigureAwait(false))
        {
            WebhookLogMessages.DuplicateIgnored(logger, payload.Id);
            return Results.Ok(new { status = "duplicate-ignored" });
        }

        // 5. Persist the audit row.
        var log = WebhookLog.Receive(
            tenantId: tenantId,
            source: Source,
            externalEventId: payload.Id,
            category: payload.Category ?? "unknown",
            eventType: payload.Type,
            referenceId: payload.ReferenceId,
            payload: rawBody,
            signature: receivedSecret,
            signatureValid: sigValid,
            nowUtc: nowUtc);
        webhookLogs.Add(log);

        // 6. Dispatch — issuance events flip the Lease to Active.
        if (sigValid && lease is not null && TajeerWebhookEventTypes.IsIssuanceCompletion(payload.Type))
        {
            try
            {
                lease.MarkIssued(
                    startKm: null,
                    startFuelLevelCode: null,
                    conditionNotes: null,
                    nowUtc: nowUtc);
                log.MarkProcessed(nowUtc);
            }
            catch (InvalidOperationException ex)
            {
                WebhookLogMessages.LeaseTransitionRejected(logger, ex, lease.Id, lease.Status.ToString());
                log.MarkFailed(ex.Message, nowUtc);
            }
        }
        else if (!sigValid)
        {
            log.MarkFailed("Signature invalid; LogOnly mode persisted the row but skipped dispatch.", nowUtc);
        }
        else if (lease is null && long.TryParse(payload.ReferenceId, out _))
        {
            log.MarkFailed($"No local Lease for contract number {payload.ReferenceId}.", nowUtc);
        }

        // Domain events raised during the transition (e.g. LeaseIssued for SMS dispatch)
        // are published post-commit by DomainEventDispatchInterceptor wired into the
        // DbContext, so SaveChangesAsync below transparently fans them out.
        try
        {
            await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            WebhookLogMessages.DuplicateRace(logger, payload.Id);
            return Results.Ok(new { status = "duplicate-ignored" });
        }

        return Results.Ok(new { status = "received", webhookLogId = log.Id });
    }
}

internal static partial class WebhookLogMessages
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Warning,
        Message = "Tajeer webhook rejected — invalid signature from {RemoteIp}.")]
    public static partial void SignatureRejected(ILogger logger, IPAddress? remoteIp);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning,
        Message = "Tajeer webhook signature MISMATCH but LogOnly=true; accepting + persisting with SignatureValid=false. RemoteIp={RemoteIp}.")]
    public static partial void SignatureMismatchLogOnly(ILogger logger, IPAddress? remoteIp);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Warning,
        Message = "Tajeer webhook body was not parseable JSON.")]
    public static partial void MalformedBody(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Information,
        Message = "Duplicate Tajeer webhook {EventId} — already processed; returning 200.")]
    public static partial void DuplicateIgnored(ILogger logger, string eventId);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Information,
        Message = "Tajeer webhook {EventId} hit unique-index race; treating as duplicate.")]
    public static partial void DuplicateRace(ILogger logger, string eventId);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Warning,
        Message = "Lease {LeaseId} could not transition to Active from {Status}; webhook recorded as failed.")]
    public static partial void LeaseTransitionRejected(ILogger logger, Exception ex, Guid leaseId, string status);
}
