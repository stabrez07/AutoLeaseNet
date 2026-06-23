using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/accounts").WithTags("accounts");

        group.MapGet("/", ListAsync).WithName("ListAccounts").RequireAuthorization();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetAccount").RequireAuthorization();
        group.MapPost("/", CreateAsync).WithName("CreateAccount").RequireAuthorization();
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateAccount").RequireAuthorization();
        group.MapPost("/{id:guid}/delete", DeleteAsync).WithName("DeleteAccount").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ListAsync(
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct,
        int page = 1, int pageSize = 30, string? search = null, Guid? customerId = null)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Accounts.AsNoTracking().Where(a => a.TenantId == tenantId);

        if (customerId.HasValue)
            query = query.Where(a => a.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a =>
                a.NatureOfBusiness.Contains(s) ||
                a.CustomerContactNameEn.Contains(s) ||
                a.AccountHolderNameEn.Contains(s) ||
                (a.City != null && a.City.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId),
                a => a.CustomerId, c => c.Id, (a, c) => new { Account = a, Customer = c })
            .Select(x => new AccountSummaryDto(
                x.Account.Id, x.Account.DisplayId, x.Account.CustomerId,
                x.Customer.DisplayName,
                x.Account.NatureOfBusiness,
                x.Account.CustomerContactNameEn,
                x.Account.AccountHolderNameEn,
                x.Account.City,
                x.Account.Status.ToString(),
                x.Account.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new
        {
            items,
            page,
            pageSize,
            totalCount = total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
        });
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var account = await db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == id)
            .FirstOrDefaultAsync(ct);
        if (account is null) return Results.NotFound();

        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == account.CustomerId)
            .Select(c => new { c.DisplayName, c.DisplayNameAr })
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new AccountDetailDto(
            account.Id, account.DisplayId, account.CustomerId,
            customer?.DisplayName ?? "", customer?.DisplayNameAr,
            account.NatureOfBusiness,
            account.CustomerContactNameEn, account.CustomerContactNameAr,
            account.CustomerContactPosition, account.CustomerContactMobile, account.CustomerContactEmail,
            account.AccountHolderNameEn, account.AccountHolderNameAr,
            account.AccountHolderPosition, account.AccountHolderMobile, account.AccountHolderEmail,
            account.Street, account.City, account.Region, account.PostalCode, account.Country,
            account.Status.ToString(),
            account.CreatedAtUtc, account.UpdatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext ctx, CreateAccountRequest body,
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var customerExists = await db.Customers.AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Id == body.CustomerId, ct);
        if (!customerExists) return Results.NotFound("Customer not found.");

        var account = Account.Create(new AccountCreateInput
        {
            TenantId = tenantId,
            CustomerId = body.CustomerId,
            NatureOfBusiness = body.NatureOfBusiness,
            CustomerContactNameEn = body.CustomerContactNameEn,
            CustomerContactNameAr = body.CustomerContactNameAr,
            CustomerContactPosition = body.CustomerContactPosition,
            CustomerContactMobile = body.CustomerContactMobile,
            CustomerContactEmail = body.CustomerContactEmail,
            AccountHolderNameEn = body.AccountHolderNameEn,
            AccountHolderNameAr = body.AccountHolderNameAr,
            AccountHolderPosition = body.AccountHolderPosition,
            AccountHolderMobile = body.AccountHolderMobile,
            AccountHolderEmail = body.AccountHolderEmail,
            Street = body.Street,
            City = body.City,
            Region = body.Region,
            PostalCode = body.PostalCode,
            Country = body.Country,
            NowUtc = DateTimeOffset.UtcNow,
        });

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { accountId = account.Id, displayId = account.DisplayId, status = "Active" });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, HttpContext ctx, UpdateAccountRequest body,
        AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
        if (account is null) return Results.NotFound();

        account.Update(new AccountUpdateInput
        {
            NatureOfBusiness = body.NatureOfBusiness,
            CustomerContactNameEn = body.CustomerContactNameEn,
            CustomerContactNameAr = body.CustomerContactNameAr,
            CustomerContactPosition = body.CustomerContactPosition,
            CustomerContactMobile = body.CustomerContactMobile,
            CustomerContactEmail = body.CustomerContactEmail,
            AccountHolderNameEn = body.AccountHolderNameEn,
            AccountHolderNameAr = body.AccountHolderNameAr,
            AccountHolderPosition = body.AccountHolderPosition,
            AccountHolderMobile = body.AccountHolderMobile,
            AccountHolderEmail = body.AccountHolderEmail,
            Street = body.Street,
            City = body.City,
            Region = body.Region,
            PostalCode = body.PostalCode,
            Country = body.Country,
            NowUtc = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { accountId = account.Id, status = account.Status.ToString() });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, HttpContext ctx, AutoLeaseNetDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest("Missing Idempotency-Key header.");

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
        if (account is null) return Results.NotFound();

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { deleted = true, accountId = id });
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────

public sealed record AccountSummaryDto(
    Guid Id, int DisplayId, Guid CustomerId, string CustomerDisplayName,
    string NatureOfBusiness, string CustomerContactNameEn,
    string AccountHolderNameEn, string? City,
    string Status, DateTimeOffset CreatedAtUtc);

public sealed record AccountDetailDto(
    Guid Id, int DisplayId, Guid CustomerId,
    string CustomerDisplayName, string? CustomerDisplayNameAr,
    string NatureOfBusiness,
    string CustomerContactNameEn, string? CustomerContactNameAr,
    string? CustomerContactPosition, string? CustomerContactMobile, string? CustomerContactEmail,
    string AccountHolderNameEn, string? AccountHolderNameAr,
    string? AccountHolderPosition, string? AccountHolderMobile, string? AccountHolderEmail,
    string? Street, string? City, string? Region, string? PostalCode, string? Country,
    string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record CreateAccountRequest(
    Guid CustomerId,
    string? NatureOfBusiness,
    string CustomerContactNameEn, string? CustomerContactNameAr,
    string? CustomerContactPosition, string? CustomerContactMobile, string? CustomerContactEmail,
    string AccountHolderNameEn, string? AccountHolderNameAr,
    string? AccountHolderPosition, string? AccountHolderMobile, string? AccountHolderEmail,
    string? Street, string? City, string? Region, string? PostalCode, string? Country);

public sealed record UpdateAccountRequest(
    string? NatureOfBusiness,
    string? CustomerContactNameEn, string? CustomerContactNameAr,
    string? CustomerContactPosition, string? CustomerContactMobile, string? CustomerContactEmail,
    string? AccountHolderNameEn, string? AccountHolderNameAr,
    string? AccountHolderPosition, string? AccountHolderMobile, string? AccountHolderEmail,
    string? Street, string? City, string? Region, string? PostalCode, string? Country);
