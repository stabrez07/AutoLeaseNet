using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// RFQ (Request for Quotation) endpoints: list, detail, pipeline (Kanban), create, stage transition, convert.
/// </summary>
public static class RfqEndpoints
{
    public static IEndpointRouteBuilder MapRfqEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/rfqs").WithTags("rfqs");

        group.MapGet("", ListRfqsAsync)
            .WithName("ListRfqs")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetRfqByIdAsync)
            .WithName("GetRfqById")
            .RequireAuthorization();

        group.MapGet("/pipeline", GetPipelineAsync)
            .WithName("GetRfqPipeline")
            .RequireAuthorization();

        group.MapPost("", CreateRfqAsync)
            .WithName("CreateRfq")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/stage", TransitionStageAsync)
            .WithName("TransitionRfqStage")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/convert", ConvertToQuotationAsync)
            .WithName("ConvertRfqToQuotation")
            .RequireAuthorization();

        return group;
    }

    // ── GET "" (list) ───────────────────────────────────────────────────────────

    private static async Task<IResult> ListRfqsAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? stage = null,
        Guid? ownerId = null)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Rfqs.AsNoTracking().Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(stage) && Enum.TryParse<RfqStage>(stage, true, out var st))
            query = query.Where(r => r.Stage == st);

        if (ownerId.HasValue && ownerId.Value != Guid.Empty)
            query = query.Where(r => r.OwnerUserId == ownerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.RfqNumber.Contains(search));

        var total = await query.CountAsync(ct);

        var rfqs = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customerIds = rfqs.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var items = rfqs.Select(r =>
        {
            var custName = customers.TryGetValue(r.CustomerId, out var c) ? c.DisplayName : "—";
            return new
            {
                r.Id,
                r.DisplayId,
                r.RfqNumber,
                r.CustomerId,
                CustomerDisplayName = custName,
                Source = r.Source.ToString(),
                Stage = r.Stage.ToString(),
                r.Probability,
                r.VehicleQty,
                r.TenureMonths,
                r.ExpectedCloseDate,
                r.OwnerUserId,
                r.Notes,
                r.CreatedAtUtc,
            };
        }).ToList();

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    // ── GET "/{id:guid}" (detail) ───────────────────────────────────────────────

    private static async Task<IResult> GetRfqByIdAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var rfq = await db.Rfqs.AsNoTracking()
            .Include(r => r.StageHistory)
            .Include(r => r.Attachments)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (rfq is null) return Results.NotFound();

        var cust = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == rfq.CustomerId, ct);

        return Results.Ok(new
        {
            rfq.Id,
            rfq.DisplayId,
            rfq.RfqNumber,
            rfq.CustomerId,
            CustomerDisplayName = cust?.DisplayName ?? "—",
            rfq.CrmOpportunityId,
            Source = rfq.Source.ToString(),
            Stage = rfq.Stage.ToString(),
            rfq.Probability,
            rfq.VehicleCategories,
            rfq.VehicleQty,
            rfq.TenureMonths,
            rfq.AnnualMileageCapKm,
            rfq.Services,
            rfq.ExpectedCloseDate,
            rfq.OwnerUserId,
            rfq.LostReason,
            rfq.Notes,
            rfq.QuotationId,
            rfq.CreatedAtUtc,
            rfq.UpdatedAtUtc,
            StageHistory = rfq.StageHistory
                .OrderByDescending(h => h.CreatedAtUtc)
                .Select(h => new
                {
                    h.Id,
                    FromStage = h.FromStage?.ToString(),
                    ToStage = h.ToStage.ToString(),
                    h.ChangedByUserId,
                    h.Comment,
                    h.CreatedAtUtc,
                }),
            Attachments = rfq.Attachments
                .OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new
                {
                    a.Id,
                    a.FileName,
                    a.FileUrl,
                    a.FileType,
                    a.FileSizeBytes,
                    a.UploadedByUserId,
                    a.CreatedAtUtc,
                }),
        });
    }

    // ── GET "/pipeline" (Kanban data) ───────────────────────────────────────────

    private static async Task<IResult> GetPipelineAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var allRfqs = await db.Rfqs.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var customerIds = allRfqs.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var stages = Enum.GetValues<RfqStage>()
            .Select(stageVal =>
            {
                var stageRfqs = allRfqs.Where(r => r.Stage == stageVal).ToList();
                return new
                {
                    Stage = stageVal.ToString(),
                    Count = stageRfqs.Count,
                    Items = stageRfqs.Take(20).Select(r =>
                    {
                        var custName = customers.TryGetValue(r.CustomerId, out var c) ? c.DisplayName : "—";
                        return new
                        {
                            r.Id,
                            r.DisplayId,
                            r.RfqNumber,
                            r.CustomerId,
                            CustomerDisplayName = custName,
                            Source = r.Source.ToString(),
                            Stage = r.Stage.ToString(),
                            r.Probability,
                            r.VehicleQty,
                            r.TenureMonths,
                            r.ExpectedCloseDate,
                            r.OwnerUserId,
                            r.Notes,
                            r.CreatedAtUtc,
                        };
                    }),
                };
            });

        return Results.Ok(new { stages });
    }

    // ── POST "" (create) ────────────────────────────────────────────────────────

    private static async Task<IResult> CreateRfqAsync(
        HttpContext ctx,
        IMediator mediator,
        CreateRfqRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /rfqs requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /rfqs requires a JSON body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new CreateRfqCommand(
            CustomerId: body.CustomerId,
            Source: body.Source,
            VehicleQty: body.VehicleQty,
            TenureMonths: body.TenureMonths,
            VehicleCategories: body.VehicleCategories,
            Services: body.Services,
            AnnualMileageCapKm: body.AnnualMileageCapKm,
            ExpectedCloseDate: body.ExpectedCloseDate,
            Notes: body.Notes,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            return Results.Problem(
                title: result.ErrorCode ?? "rfq.create.error",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created($"/api/v1/rfqs/{result.RfqId}", new
        {
            rfqId = result.RfqId,
        });
    }

    // ── POST "/{id:guid}/stage" (transition) ────────────────────────────────────

    private static async Task<IResult> TransitionStageAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        UpdateRfqStageRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /rfqs/{id}/stage requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /rfqs/{id}/stage requires a JSON body with toStage.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<RfqStage>(body.ToStage, true, out _))
        {
            return Results.Problem(
                title: "rfq.invalid_stage",
                detail: $"'{body.ToStage}' is not a valid RFQ stage.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var command = new UpdateRfqStageCommand(
            RfqId: id,
            ToStage: body.ToStage,
            Comment: body.Comment,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "rfq.not_found" => StatusCodes.Status404NotFound,
                "rfq.invalid_transition" => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(
                title: result.ErrorCode ?? "rfq.stage.error",
                detail: result.ErrorMessage,
                statusCode: status);
        }

        return Results.Ok(new { rfqId = result.RfqId });
    }

    // ── POST "/{id:guid}/convert" (convert to quotation) ────────────────────────

    private static async Task<IResult> ConvertToQuotationAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /rfqs/{id}/convert requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new ConvertRfqToQuotationCommand(
            RfqId: id,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "rfq.not_found" => StatusCodes.Status404NotFound,
                "rfq.invalid_stage" => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(
                title: result.ErrorCode ?? "rfq.convert.error",
                detail: result.ErrorMessage,
                statusCode: status);
        }

        return Results.Ok(new
        {
            rfqId = result.RfqId,
            quotationId = result.QuotationId,
        });
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────────

public sealed record CreateRfqRequest
{
    public required Guid CustomerId { get; init; }
    public required string Source { get; init; }
    public required int VehicleQty { get; init; }
    public required int TenureMonths { get; init; }
    public string? VehicleCategories { get; init; }
    public string? Services { get; init; }
    public int? AnnualMileageCapKm { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public string? Notes { get; init; }
}

public sealed record UpdateRfqStageRequest
{
    public required string ToStage { get; init; }
    public string? Comment { get; init; }
}
