using AutoLeaseNet.Application.Customers;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AutoLeaseNet.Bff.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/customers").WithTags("customers");

        group.MapPost("/b2b", CreateB2BAsync).WithName("CreateCustomerB2B").RequireAuthorization();
        group.MapPost("/b2c", CreateB2CAsync).WithName("CreateCustomerB2C").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetCustomer").RequireAuthorization();
        group.MapPost("/{id:guid}/status", UpdateStatusAsync).WithName("UpdateCustomerStatus").RequireAuthorization();

        // Documents
        group.MapGet("/{id:guid}/documents", ListDocumentsAsync).WithName("ListCustomerDocuments").RequireAuthorization();
        group.MapPost("/{id:guid}/documents", CreateDocumentAsync).WithName("CreateCustomerDocument").RequireAuthorization();
        group.MapPost("/{id:guid}/documents/{docId:guid}/verify", VerifyDocumentAsync).WithName("VerifyCustomerDocument").RequireAuthorization();

        // Timeline / activities
        group.MapGet("/{id:guid}/timeline", GetTimelineAsync).WithName("GetCustomerTimeline").RequireAuthorization();
        group.MapPost("/{id:guid}/activities", CreateActivityAsync).WithName("CreateCustomerActivity").RequireAuthorization();

        // RFQs for a customer
        group.MapGet("/{id:guid}/rfqs", ListCustomerRfqsAsync).WithName("ListCustomerRfqs").RequireAuthorization();

        // Drivers for a customer
        group.MapGet("/{id:guid}/drivers", ListCustomerDriversAsync).WithName("ListCustomerDrivers").RequireAuthorization();

        // Invoices & Payments for a customer
        group.MapGet("/{id:guid}/invoices", ListCustomerInvoicesAsync).WithName("ListCustomerInvoices").RequireAuthorization();
        group.MapGet("/{id:guid}/payments", ListCustomerPaymentsAsync).WithName("ListCustomerPayments").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CreateB2BAsync(
        HttpContext ctx, IMediator mediator, CreateCustomerB2BRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateCustomerB2BCommand(
            LegalName: body.LegalName,
            LegalNameAr: body.LegalNameAr,
            CommercialRegistration: body.CommercialRegistration,
            VatNumber: body.VatNumber,
            Email: body.Email,
            Mobile: body.Mobile,
            NationalAddress: body.NationalAddress,
            BillingAddress: body.BillingAddress,
            CreditLimit: body.CreditLimit,
            CreditCurrency: body.CreditCurrency,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> CreateB2CAsync(
        HttpContext ctx, IMediator mediator, CreateCustomerB2CRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new CreateCustomerB2CCommand(
            PersonNameEn: body.PersonNameEn,
            PersonNameAr: body.PersonNameAr,
            IdTypeCode: body.IdTypeCode,
            PersonIdNumber: body.PersonIdNumber,
            DateOfBirth: body.DateOfBirth,
            NationalityCode: body.NationalityCode,
            Email: body.Email,
            Mobile: body.Mobile,
            NationalAddress: body.NationalAddress,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, ICustomerRepository customers, ITenantContext tenant, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(tenant.TenantId, id, ct).ConfigureAwait(false);
        if (customer is null) return Results.NotFound();
        return Results.Ok(ToDto(customer));
    }

    private static async Task<IResult> UpdateStatusAsync(
        HttpContext ctx, IMediator mediator, Guid id, UpdateCustomerStatusRequest body, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");
        if (body is null)
            return Results.BadRequest("Missing request body.");

        var cmd = new UpdateCustomerStatusCommand(
            CustomerId: id,
            Action: body.Action,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(cmd, ct);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(detail: result.ErrorMessage, title: result.ErrorCode, statusCode: 400);
    }

    // ─── Documents ────────────────────────────────────────────────────────

    private static async Task<IResult> ListDocumentsAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var docs = await db.CustomerDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CustomerId == id)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new CustomerDocumentDto(
                d.Id, d.CustomerId, d.DocType, d.FileName, d.FileUrl,
                d.ExpiryDate, d.VerifiedAtUtc, d.VerifiedByUserId, d.Notes,
                d.CreatedAtUtc, d.UpdatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(docs);
    }

    private static async Task<IResult> CreateDocumentAsync(
        Guid id, CreateCustomerDocumentRequest body,
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var customerExists = await db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Id == id, ct);
        if (!customerExists) return Results.NotFound("Customer not found.");

        var doc = CustomerDocument.Create(
            tenantId, id, body.DocType, body.FileName, body.FileUrl,
            body.ExpiryDate, body.Notes);

        db.CustomerDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new CustomerDocumentDto(
            doc.Id, doc.CustomerId, doc.DocType, doc.FileName, doc.FileUrl,
            doc.ExpiryDate, doc.VerifiedAtUtc, doc.VerifiedByUserId, doc.Notes,
            doc.CreatedAtUtc, doc.UpdatedAtUtc));
    }

    private static async Task<IResult> VerifyDocumentAsync(
        Guid id, Guid docId,
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var doc = await db.CustomerDocuments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CustomerId == id && d.Id == docId, ct);
        if (doc is null) return Results.NotFound("Document not found.");

        // TODO: extract real user ID from auth claims; using a placeholder for now.
        var userId = Guid.NewGuid();
        doc.Verify(userId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new CustomerDocumentDto(
            doc.Id, doc.CustomerId, doc.DocType, doc.FileName, doc.FileUrl,
            doc.ExpiryDate, doc.VerifiedAtUtc, doc.VerifiedByUserId, doc.Notes,
            doc.CreatedAtUtc, doc.UpdatedAtUtc));
    }

    // ─── Timeline / Activities ──────────────────────────────────────────

    private static async Task<IResult> GetTimelineAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.AccountActivities
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.CustomerId == id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AccountActivityDto(
                a.Id, a.CustomerId, a.ActivityType, a.Subject, a.Body,
                a.Direction, a.DurationMinutes, a.PerformedByUserId,
                a.LinkedEntityType, a.LinkedEntityId, a.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task<IResult> CreateActivityAsync(
        Guid id, CreateAccountActivityRequest body,
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var customerExists = await db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Id == id, ct);
        if (!customerExists) return Results.NotFound("Customer not found.");

        var activity = AccountActivity.Create(
            tenantId, id, body.ActivityType, body.Subject, body.Body,
            body.Direction, body.DurationMinutes, body.PerformedByUserId,
            body.LinkedEntityType, body.LinkedEntityId);

        db.AccountActivities.Add(activity);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new AccountActivityDto(
            activity.Id, activity.CustomerId, activity.ActivityType,
            activity.Subject, activity.Body, activity.Direction,
            activity.DurationMinutes, activity.PerformedByUserId,
            activity.LinkedEntityType, activity.LinkedEntityId,
            activity.CreatedAtUtc));
    }

    // ─── Customer RFQs ─────────────────────────────────────────────────

    private static async Task<IResult> ListCustomerRfqsAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Rfqs
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CustomerId == id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CustomerRfqSummaryDto(
                r.Id, r.DisplayId, r.RfqNumber, r.Stage.ToString(),
                r.Probability, r.VehicleQty, r.TenureMonths,
                r.ExpectedCloseDate, r.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items });
    }

    // ─── Customer Invoices ──────────────────────────────────────────────

    private static async Task<IResult> ListCustomerDriversAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CustomerId == id)
            .OrderBy(d => d.PersonNameEn)
            .Select(d => new
            {
                d.Id,
                d.DisplayId,
                d.PersonNameEn,
                d.PersonNameAr,
                d.DriverLicenseNumber,
                LicenseExpiryDate = d.LicenseExpiryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                IdTypeCode = d.IdTypeCode,
                PersonIdNumber = d.PersonIdNumber,
                Status = (int)d.Status,
            })
            .ToListAsync(ct);

        return Results.Ok(drivers);
    }

    private static async Task<IResult> ListCustomerInvoicesAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CustomerId == id);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                db.Leases.AsNoTracking().Where(l => l.TenantId == tenantId),
                i => i.LeaseId,
                l => l.Id,
                (i, l) => new { Invoice = i, Lease = l })
            .Select(x => new CustomerInvoiceDto(
                x.Invoice.Id,
                x.Invoice.DisplayId,
                x.Invoice.InvoiceNumber,
                x.Invoice.LeaseId,
                "L-" + x.Lease.DisplayId.ToString(CultureInfo.InvariantCulture),
                x.Invoice.Status.ToString(),
                x.Invoice.IssueDateUtc,
                x.Invoice.DueDateUtc,
                x.Invoice.BaseAmountSar,
                x.Invoice.VatSar,
                x.Invoice.TotalSar,
                db.PaymentAllocations
                    .AsNoTracking()
                    .Where(pa => pa.InvoiceId == x.Invoice.Id)
                    .Sum(pa => (decimal?)pa.AllocatedAmountSar) ?? 0m))
            .ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items });
    }

    // ─── Customer Payments ──────────────────────────────────────────────

    private static async Task<IResult> ListCustomerPaymentsAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.AdvancePayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.CustomerId == id);

        var total = await query.CountAsync(ct);

        var payments = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new CustomerPaymentDto(
                p.Id,
                p.DisplayId,
                p.Amount,
                p.PaymentMethod,
                p.ReceivedDate,
                p.ReferenceNumber,
                p.Notes,
                p.RemainingBalance,
                p.CreatedAtUtc,
                db.PaymentAllocations
                    .AsNoTracking()
                    .Where(pa => pa.AdvancePaymentId == p.Id)
                    .Select(pa => new PaymentAllocationDto(
                        pa.InvoiceId,
                        pa.InvoiceNumber,
                        pa.AllocatedAmountSar))
                    .ToList()))
            .ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items = payments });
    }

    // ─── Mappers ────────────────────────────────────────────────────────

    private static CustomerDetailDto ToDto(Customer c) => new(
        Id: c.Id,
        TenantId: c.TenantId,
        Type: c.Type.ToString(),
        Status: c.Status.ToString(),
        DisplayName: c.DisplayName,
        DisplayNameAr: c.DisplayNameAr,
        Email: c.Email,
        Mobile: c.Mobile,
        NationalAddress: c.NationalAddress,
        PreferredLanguage: c.PreferredLanguage.ToString(),
        LegalName: c.LegalName,
        LegalNameAr: c.LegalNameAr,
        CommercialRegistration: c.CommercialRegistration,
        VatNumber: c.VatNumber,
        BillingAddress: c.BillingAddress,
        CreditLimit: c.CreditLimit,
        CreditCurrency: c.CreditCurrency,
        PersonNameEn: c.PersonNameEn,
        PersonNameAr: c.PersonNameAr,
        IdTypeCode: c.IdTypeCode,
        PersonIdNumber: c.PersonIdNumber,
        DateOfBirth: c.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        NationalityCode: c.NationalityCode,
        KycVerified: c.KycVerified,
        KycVerifiedAtUtc: c.KycVerifiedAtUtc,
        KycVerifiedBy: c.KycVerifiedBy,
        CreatedAtUtc: c.CreatedAtUtc,
        UpdatedAtUtc: c.UpdatedAtUtc);
}

public sealed record CreateCustomerB2BRequest(
    string LegalName, string? LegalNameAr,
    string CommercialRegistration, string? VatNumber,
    string? Email, string? Mobile, string? NationalAddress, string? BillingAddress,
    decimal? CreditLimit, string? CreditCurrency);

public sealed record CreateCustomerB2CRequest(
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, string PersonIdNumber,
    string? DateOfBirth,
    string? NationalityCode, string? Email, string? Mobile, string? NationalAddress);

public sealed record UpdateCustomerStatusRequest(string Action);

public sealed record CreateCustomerDocumentRequest(
    string DocType, string FileName, string FileUrl,
    DateOnly? ExpiryDate, string? Notes);

public sealed record CreateAccountActivityRequest(
    string ActivityType, string Subject, string? Body,
    string? Direction, int? DurationMinutes, Guid PerformedByUserId,
    string? LinkedEntityType, Guid? LinkedEntityId);

public sealed record CustomerDocumentDto(
    Guid Id, Guid CustomerId, string DocType, string FileName, string FileUrl,
    DateOnly? ExpiryDate, DateTimeOffset? VerifiedAtUtc, Guid? VerifiedByUserId,
    string? Notes, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record AccountActivityDto(
    Guid Id, Guid CustomerId, string ActivityType, string Subject, string? Body,
    string? Direction, int? DurationMinutes, Guid PerformedByUserId,
    string? LinkedEntityType, Guid? LinkedEntityId, DateTimeOffset CreatedAtUtc);

public sealed record CustomerRfqSummaryDto(
    Guid Id, int DisplayId, string RfqNumber, string Stage,
    int Probability, int VehicleQty, int TenureMonths,
    DateOnly? ExpectedCloseDate, DateTimeOffset CreatedAtUtc);

public sealed record CustomerDetailDto(
    Guid Id, Guid TenantId,
    string Type, string Status,
    string DisplayName, string? DisplayNameAr,
    string? Email, string? Mobile, string? NationalAddress, string PreferredLanguage,
    string? LegalName, string? LegalNameAr,
    string? CommercialRegistration, string? VatNumber, string? BillingAddress,
    decimal? CreditLimit, string? CreditCurrency,
    string? PersonNameEn, string? PersonNameAr,
    int? IdTypeCode, string? PersonIdNumber, string? DateOfBirth, string? NationalityCode,
    bool KycVerified, DateTimeOffset? KycVerifiedAtUtc, string? KycVerifiedBy,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record CustomerInvoiceDto(
    Guid Id, int DisplayId, string InvoiceNumber, Guid LeaseId, string LeaseNumber,
    string Status, DateOnly IssueDateUtc, DateOnly DueDateUtc,
    decimal BaseAmountSar, decimal VatAmountSar, decimal TotalAmountSar,
    decimal PaidAmountSar);

public sealed record CustomerPaymentDto(
    Guid Id, int DisplayId, decimal Amount, string PaymentMethod,
    DateOnly ReceivedDate, string? ReferenceNumber, string? Notes,
    decimal RemainingBalance, DateTimeOffset CreatedAtUtc,
    List<PaymentAllocationDto> Allocations);

public sealed record PaymentAllocationDto(
    Guid InvoiceId, string InvoiceNumber, decimal AllocatedAmountSar);
