using AutoLeaseNet.Application.Lookups;
using MediatR;

namespace AutoLeaseNet.Application.Sales;

// Handlers live in AutoLeaseNet.Infrastructure.Sales (need DbContext directly).

/// <summary>Returns pending approval rows the calling user's role can decide on.</summary>
public sealed record GetApprovalInboxQuery(
    int Page,
    int PageSize,
    string? RequiredRoleCode) : IRequest<PagedResult<ApprovalInboxItemDto>>;

// ─── DTO ──────────────────────────────────────────────────────────────────────

public sealed record ApprovalInboxItemDto(
    Guid QuotationId,
    string QuoteNumber,
    byte TierLevel,
    string RequiredRoleCode,
    decimal TotalSar,
    string CustomerName,
    DateTimeOffset SubmittedAtUtc);
