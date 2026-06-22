using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

public sealed class RfqStageHistory : Entity
{
    public Guid RfqId { get; private set; }
    public RfqStage? FromStage { get; private set; }
    public RfqStage ToStage { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Comment { get; private set; }

    private RfqStageHistory() { }

    internal static RfqStageHistory Create(
        Guid tenantId, Guid rfqId, RfqStage? from, RfqStage to, Guid userId, string? comment)
    {
        return new RfqStageHistory
        {
            TenantId = tenantId,
            RfqId = rfqId,
            FromStage = from,
            ToStage = to,
            ChangedByUserId = userId,
            Comment = comment,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
