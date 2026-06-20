using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Quotation (Spec 02 §6.1) endpoints for PDF generation and distribution.
/// </summary>
public static class QuotationEndpoints
{
    private static readonly TimeSpan CreateAddIdempotencyTtl = TimeSpan.FromHours(24);

    public static IEndpointRouteBuilder MapQuotationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/quotations").WithTags("quotations");

        group.MapGet("", GetQuotationsAsync).WithName("GetQuotations").RequireAuthorization();
        group.MapGet("/{id:guid}", GetQuotationByIdAsync).WithName("GetQuotationById").RequireAuthorization();
        group.MapPost("", CreateQuotationAsync).WithName("CreateQuotation").RequireAuthorization();
        group.MapPost("/{id:guid}/lines", AddQuotationLineAsync).WithName("AddQuotationLine").RequireAuthorization();

        group.MapPost("/{id:guid}/submit-approval", SubmitApprovalAsync).WithName("SubmitQuotationApproval").RequireAuthorization();
        group.MapPost("/{id:guid}/approvals/{tierLevel:int}/decision", RecordApprovalDecisionAsync).WithName("RecordQuotationApprovalDecision").RequireAuthorization();
        group.MapGet("/approvals/pending", GetPendingApprovalsAsync).WithName("GetPendingQuotationApprovals").RequireAuthorization();
        group.MapPost("/{id:guid}/send-pdf", SendPdfAsync).WithName("SendQuotationPdf").RequireAuthorization();
        group.MapPost("/{id:guid}/accept", AcceptAsync).WithName("AcceptQuotation").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetQuotationsAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        if (tenant.TenantId == Guid.Empty)
            return Results.Unauthorized();

        var safePage = page < 1 ? 1 : page;
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = tenant.TenantId;

        var query = db.Quotations
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId)
            .Join(
                db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId),
                q => q.CustomerId,
                c => c.Id,
                (q, c) => new { Quotation = q, CustomerName = c.DisplayName });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Quotation.QuoteNumber.Contains(term) || x.CustomerName.Contains(term));
        }

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.Quotation.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new QuotationSummaryResponse(
                x.Quotation.Id,
                x.Quotation.QuoteNumber,
                x.Quotation.CustomerId,
                x.CustomerName,
                x.Quotation.Status.ToString(),
                x.Quotation.ContractType.ToString(),
                x.Quotation.TotalSar,
                x.Quotation.SubTotalSar,
                x.Quotation.VatSar,
                x.Quotation.DiscountPercent,
                x.Quotation.QuoteDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                x.Quotation.ValidUntilDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                x.Quotation.EstimatedDurationMonths,
                x.Quotation.SubmittedAtUtc,
                x.Quotation.ApprovedAtUtc,
                x.Quotation.SentAtUtc,
                x.Quotation.AcceptedAtUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(new
        {
            items,
            page = safePage,
            pageSize = safePageSize,
            totalCount,
            totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize))
        });
    }

    private static async Task<IResult> GetQuotationByIdAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        Guid id,
        CancellationToken ct)
    {
        if (tenant.TenantId == Guid.Empty)
            return Results.Unauthorized();

        var tenantId = tenant.TenantId;
        var row = await db.Quotations
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.Id == id)
            .Join(
                db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId),
                q => q.CustomerId,
                c => c.Id,
                (q, c) => new { Quotation = q, CustomerName = c.DisplayName })
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (row is null)
            return Results.NotFound($"Quotation {id} not found.");

        var quotation = await db.Quotations
            .AsNoTracking()
            .Include(q => q.Lines)
            .Include(q => q.Approvals)
            .SingleAsync(q => q.TenantId == tenantId && q.Id == id, ct)
            .ConfigureAwait(false);

        return Results.Ok(ToDetail(quotation, row.CustomerName));
    }

    private static async Task<IResult> CreateQuotationAsync(
        HttpContext ctx,
        AutoLeaseNetDbContext db,
        IIdempotencyStore idempotency,
        ITenantContext tenant,
        IClock clock,
        CreateQuotationRequest body,
        CancellationToken ct)
    {
        if (tenant.TenantId == Guid.Empty)
            return Results.Unauthorized();

        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var tenantId = tenant.TenantId;
        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:quotation:create:{idempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationDetailResponse>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
            return Results.Ok(cached);

        var customerExists = await db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Id == body.CustomerId, ct)
            .ConfigureAwait(false);
        if (!customerExists)
            return Results.BadRequest($"Customer {body.CustomerId} not found.");

        var quoteNumber = await GenerateQuoteNumberAsync(db, tenantId, clock.UtcNow, ct).ConfigureAwait(false);
        Quotation quotation;
        try
        {
            quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = tenantId,
                QuoteNumber = quoteNumber,
                CustomerId = body.CustomerId,
                AccountManagerId = body.AccountManagerId,
                QuoteDate = body.QuoteDate,
                ValidUntilDate = body.ValidUntilDate,
                ContractType = body.ContractType,
                EstimatedDurationMonths = body.EstimatedDurationMonths,
                DiscountPercent = body.DiscountPercent,
                TermsAndConditionsMd = body.TermsAndConditionsMd,
                NowUtc = clock.UtcNow,
            });
        }
        catch (Exception ex) when (ex is ArgumentException || ex is ArgumentOutOfRangeException)
        {
            return Results.BadRequest(ex.Message);
        }

        db.Quotations.Add(quotation);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var customerName = await db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == body.CustomerId)
            .Select(c => c.DisplayName)
            .SingleAsync(ct)
            .ConfigureAwait(false);

        var dto = ToDetail(quotation, customerName);
        await idempotency.SetAsync(idemKey, dto, CreateAddIdempotencyTtl, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/quotations/{quotation.Id}", dto);
    }

    private static async Task<IResult> AddQuotationLineAsync(
        HttpContext ctx,
        AutoLeaseNetDbContext db,
        IIdempotencyStore idempotency,
        ITenantContext tenant,
        IClock clock,
        Guid id,
        AddQuotationLineRequest body,
        CancellationToken ct)
    {
        if (tenant.TenantId == Guid.Empty)
            return Results.Unauthorized();

        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var tenantId = tenant.TenantId;
        var idemKey = new IdempotencyKey($"tenant:{tenantId:N}:quotation:add-line:{id:N}:{idempotencyKey}");
        var cached = await idempotency.GetAsync<QuotationDetailResponse>(idemKey, ct).ConfigureAwait(false);
        if (cached is not null)
            return Results.Ok(cached);

        var quotation = await db.Quotations
            .Include(q => q.Lines)
            .Include(q => q.Approvals)
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.Id == id, ct)
            .ConfigureAwait(false);
        if (quotation is null)
            return Results.NotFound($"Quotation {id} not found.");

        try
        {
            quotation.AddLine(new AddQuotationLineInput
            {
                ItemType = body.ItemType,
                Description = body.Description,
                VehicleSpecRef = body.VehicleSpecRef,
                Quantity = body.Quantity,
                UnitPriceSar = body.UnitPriceSar,
                DiscountPercent = body.DiscountPercent,
                NowUtc = clock.UtcNow,
            });
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
        {
            return Results.BadRequest(ex.Message);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var customerName = await db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == quotation.CustomerId)
            .Select(c => c.DisplayName)
            .SingleAsync(ct)
            .ConfigureAwait(false);

        var dto = ToDetail(quotation, customerName);
        await idempotency.SetAsync(idemKey, dto, CreateAddIdempotencyTtl, ct).ConfigureAwait(false);
        return Results.Ok(dto);
    }

    private static async Task<IResult> SubmitApprovalAsync(HttpContext ctx, IMediator mediator, Guid id, SubmitQuotationApprovalRequest? body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        IReadOnlyList<NamedApproverInput>? namedApprovers = null;
        if (body?.NamedApprovers is { Count: > 0 })
        {
            if (body.NamedApprovers.Count is < 2 or > 5)
                return Results.BadRequest("Named approvers must be between 2 and 5 people.");

            namedApprovers = body.NamedApprovers
                .Select(a => new NamedApproverInput(a.UserId, a.Name))
                .ToList();
        }

        var command = new SubmitQuotationForApprovalCommand(idempotencyKey, id, namedApprovers);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> RecordApprovalDecisionAsync(
        HttpContext ctx,
        IMediator mediator,
        IQuotationRepository quotations,
        ITenantContext tenant,
        Guid id,
        int tierLevel,
        QuotationApprovalDecisionRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");
        if (tierLevel is < 1 or > 5)
            return Results.BadRequest("tierLevel must be between 1 and 5.");

        var quotation = await quotations.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (quotation is null)
            return Results.NotFound($"Quotation {id} not found.");

        var approval = quotation.Approvals.SingleOrDefault(a => a.TierLevel == tierLevel);
        if (approval is null)
            return Results.NotFound($"Quotation {id} has no approval tier {tierLevel}.");
        if (approval.Status != QuotationApprovalStatus.Pending)
            return Results.Conflict($"Tier {tierLevel} is already {approval.Status}.");
        if (approval.AssignedUserId.HasValue)
        {
            if (tenant.UserId is null || tenant.UserId.Value != approval.AssignedUserId.Value)
                return Results.Forbid();
        }
        else if (!ctx.User.IsInRole(approval.RequiredRoleCode))
        {
            return Results.Forbid();
        }

        var command = new RecordQuotationApprovalDecisionCommand(
            IdempotencyKey: idempotencyKey,
            QuotationId: id,
            TierLevel: (byte)tierLevel,
            Approved: body.Approved,
            Comment: body.Comment);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetPendingApprovalsAsync(IMediator mediator, CancellationToken ct)
    {
        var pending = await mediator.Send(new GetPendingQuotationApprovalsQuery(), ct);
        return Results.Ok(pending);
    }

    private static async Task<IResult> SendPdfAsync(HttpContext ctx, IMediator mediator, Guid id, SendQuotePdfRequest body, CancellationToken ct)
    {
        if (body is null) return Results.BadRequest("Missing request body.");

        var idemKey = ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key) ? key.ToString() : Guid.NewGuid().ToString();

        var command = new SendQuotePdfCommand(idemKey, id, body.RecipientEmail);
        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Accepted()
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> AcceptAsync(HttpContext ctx, IMediator mediator, Guid id, AcceptQuotationRequest? body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var command = new AcceptQuotationCommand(
            QuotationId: id,
            CustomerSignature: body?.CustomerSignature,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);

        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static QuotationDetailResponse ToDetail(Quotation quotation, string? customerDisplayName)
    {
        return new QuotationDetailResponse(
            Id: quotation.Id,
            QuoteNumber: quotation.QuoteNumber,
            CustomerId: quotation.CustomerId,
            CustomerDisplayName: customerDisplayName,
            Status: quotation.Status.ToString(),
            ContractType: quotation.ContractType.ToString(),
            TotalSar: quotation.TotalSar,
            SubTotalSar: quotation.SubTotalSar,
            VatSar: quotation.VatSar,
            DiscountPercent: quotation.DiscountPercent,
            QuoteDate: quotation.QuoteDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ValidUntilDate: quotation.ValidUntilDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EstimatedDurationMonths: quotation.EstimatedDurationMonths,
            SubmittedAtUtc: quotation.SubmittedAtUtc,
            ApprovedAtUtc: quotation.ApprovedAtUtc,
            SentAtUtc: quotation.SentAtUtc,
            AcceptedAtUtc: quotation.AcceptedAtUtc,
            Lines: quotation.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new QuotationLineResponse(
                    l.Id,
                    l.LineNumber,
                    l.ItemType.ToString(),
                    l.Description,
                    l.VehicleSpecRef,
                    l.Quantity,
                    l.UnitPriceSar,
                    l.DiscountPercent,
                    l.LineTotalSar))
                .ToList(),
            Approvals: quotation.Approvals
                .OrderBy(a => a.TierLevel)
                .Select(a => new QuotationApprovalResponse(
                    a.TierLevel,
                    a.RequiredRoleCode,
                    a.Status.ToString(),
                    a.DecidedByUserId,
                    a.Comment,
                    a.DecisionAtUtc,
                    a.AssignedUserId))
                .ToList(),
            PdfBlobUri: quotation.PdfBlobUri,
            AcceptedByCustomerSignature: quotation.AcceptedByCustomerSignature);
    }

    private static async Task<string> GenerateQuoteNumberAsync(AutoLeaseNetDbContext db, Guid tenantId, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var prefix = $"Q-{nowUtc:yyyyMMdd}-";
        var count = await db.Quotations
            .AsNoTracking()
            .CountAsync(q => q.TenantId == tenantId && q.QuoteNumber.StartsWith(prefix), ct)
            .ConfigureAwait(false);

        return $"{prefix}{(count + 1):D4}";
    }
}

public sealed record CreateQuotationRequest(
    Guid CustomerId,
    Guid AccountManagerId,
    DateOnly QuoteDate,
    DateOnly ValidUntilDate,
    QuotationContractType ContractType,
    int EstimatedDurationMonths,
    decimal DiscountPercent,
    string? TermsAndConditionsMd);

public sealed record AddQuotationLineRequest(
    QuotationItemType ItemType,
    string Description,
    string? VehicleSpecRef,
    int Quantity,
    decimal UnitPriceSar,
    decimal DiscountPercent);

public sealed record SendQuotePdfRequest(string RecipientEmail);
public sealed record SubmitQuotationApprovalRequest(IReadOnlyList<NamedApproverRequest>? NamedApprovers);
public sealed record NamedApproverRequest(Guid UserId, string Name);
public sealed record QuotationApprovalDecisionRequest(bool Approved, string? Comment);
public sealed record AcceptQuotationRequest(string? CustomerSignature);

public sealed record QuotationSummaryResponse(
    Guid Id,
    string QuoteNumber,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    string ContractType,
    decimal TotalSar,
    decimal SubTotalSar,
    decimal VatSar,
    decimal DiscountPercent,
    string QuoteDate,
    string ValidUntilDate,
    int EstimatedDurationMonths,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? AcceptedAtUtc);

public sealed record QuotationLineResponse(
    Guid Id,
    int LineNumber,
    string ItemType,
    string Description,
    string? VehicleSpecRef,
    int Quantity,
    decimal UnitPriceSar,
    decimal DiscountPercent,
    decimal LineTotalSar);

public sealed record QuotationApprovalResponse(
    byte TierLevel,
    string RequiredRoleCode,
    string Status,
    Guid? DecidedByUserId,
    string? Comment,
    DateTimeOffset? DecidedAtUtc,
    Guid? AssignedUserId);

public sealed record QuotationDetailResponse(
    Guid Id,
    string QuoteNumber,
    Guid CustomerId,
    string? CustomerDisplayName,
    string Status,
    string ContractType,
    decimal TotalSar,
    decimal SubTotalSar,
    decimal VatSar,
    decimal DiscountPercent,
    string QuoteDate,
    string ValidUntilDate,
    int EstimatedDurationMonths,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    IReadOnlyList<QuotationLineResponse> Lines,
    IReadOnlyList<QuotationApprovalResponse> Approvals,
    string? PdfBlobUri,
    string? AcceptedByCustomerSignature);
