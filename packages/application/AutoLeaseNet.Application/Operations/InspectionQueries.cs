using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Domain.Operations;
using MediatR;

namespace AutoLeaseNet.Application.Operations;

// ─── Query records ─────────────────────────────────────────────────────────────
// Handlers live in AutoLeaseNet.Infrastructure.Operations so they can use DbContext
// directly without inverting the Application → Infrastructure dependency direction
// (matches the Lookups pattern).

/// <summary>Single-aggregate lookup with photos + damage markers eagerly loaded.</summary>
public sealed record GetInspectionByIdQuery(Guid InspectionId) : IRequest<InspectionDetailDto?>;

/// <summary>Tenant-scoped paged search ordered by PerformedAtUtc DESC.</summary>
public sealed record SearchInspectionsQuery(
    int Page,
    int PageSize,
    Guid? VehicleId,
    Guid? LeaseId,
    InspectionType? Type,
    InspectionStatus? Status) : IRequest<PagedResult<InspectionSummaryDto>>;

// ─── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record InspectionSummaryDto(
    Guid Id,
    Guid VehicleId,
    Guid? LeaseId,
    InspectionType Type,
    InspectionStatus Status,
    DateTimeOffset PerformedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int OdometerKm,
    FuelLevel FuelLevel,
    int PhotoCount,
    int DamageMarkerCount);

public sealed record InspectionDetailDto(
    Guid Id,
    Guid VehicleId,
    Guid? LeaseId,
    InspectionType Type,
    InspectionStatus Status,
    Guid PerformedByUserId,
    DateTimeOffset PerformedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? AbandonedAtUtc,
    string? AbandonedReason,
    int OdometerKm,
    FuelLevel FuelLevel,
    byte? AcCondition,
    byte? RadioStereoCondition,
    byte? ScreenCondition,
    byte? SpeedometerCondition,
    byte? KeysCondition,
    byte? CarSeatsCondition,
    byte? SafetyTriangleCondition,
    byte? FireExtinguisherCondition,
    byte? FirstAidKitCondition,
    byte? SpareTireToolsCondition,
    byte? TiresCondition,
    byte? SpareTireCondition,
    string? Other1,
    string? Other2,
    string? Notes,
    string? SketchInfoJson,
    string? RenterSignatureBlobUri,
    IReadOnlyList<InspectionPhotoDto> Photos,
    IReadOnlyList<InspectionDamageMarkerDto> DamageMarkers);

public sealed record InspectionPhotoDto(Guid Id, string BlobUri, int Sequence);

public sealed record InspectionDamageMarkerDto(Guid Id, DamageMarkerType Type, decimal PositionX, decimal PositionY);
