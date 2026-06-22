using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Notifications;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/notifications")
            .WithTags("notifications")
            .RequireAuthorization();

        group.MapGet("", ListAsync).WithName("ListNotifications");
        group.MapGet("/unread-count", UnreadCountAsync).WithName("UnreadNotificationCount");
        group.MapPost("/{id:guid}/read", MarkReadAsync).WithName("MarkNotificationRead");
        group.MapPost("/mark-all-read", MarkAllReadAsync).WithName("MarkAllNotificationsRead");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var userId = tenant.UserId ?? Guid.Empty;
        var query = db.Set<Notification>().AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.UserId == userId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.LinkedEntityType,
                n.LinkedEntityId,
                n.IsRead,
                n.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    private static async Task<IResult> UnreadCountAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var userId = tenant.UserId ?? Guid.Empty;
        var count = await db.Set<Notification>().AsNoTracking()
            .CountAsync(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead, ct);

        return Results.Ok(new { unreadCount = count });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var notif = await db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);
        if (notif is null) return Results.NotFound();

        notif.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return Results.Ok();
    }

    private static async Task<IResult> MarkAllReadAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var userId = tenant.UserId ?? Guid.Empty;
        var unread = await db.Set<Notification>()
            .Where(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var n in unread) n.MarkRead(now);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { markedRead = unread.Count });
    }
}
