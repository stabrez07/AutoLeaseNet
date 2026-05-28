using MediatR;

namespace AutoLeaseNet.Application.Leases;

/// <summary>
/// Day-20 saga step. Pause a Lease via Tajeer <c>SuspendContract</c> + local
/// <c>Lease.MarkSuspended</c>. Spec 02 §768 — Tajeer does not allow the reverse
/// transition (SUSPENDED → ACTIVE); only SUSPENDED → CLOSED. So this command has
/// no companion "Resume" equivalent at the BFF surface.
/// </summary>
public sealed record SuspendLeaseCommand : IRequest<SuspendLeaseCommandResult>
{
    public required string IdempotencyKey { get; init; }
    public required Guid LeaseId { get; init; }

    /// <summary>Tajeer suspension reason code (e.g. NON_TRAFFIC_DAMAGE).</summary>
    public required int SuspensionReasonCode { get; init; }

    /// <summary>Optional ops note recorded against the suspension.</summary>
    public string? Notes { get; init; }
}

public sealed record SuspendLeaseCommandResult(
    bool Success,
    Guid? LeaseId,
    string? LeaseStatus,
    int? SuspensionReasonCode,
    DateTimeOffset? SuspendedAtUtc,
    string? ErrorCode,
    string? ErrorMessage);
