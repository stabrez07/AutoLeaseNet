using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

public sealed class Rfq : Entity
{
    public string RfqNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public string? CrmOpportunityId { get; private set; }
    public RfqSource Source { get; private set; }
    public RfqStage Stage { get; private set; }
    public int Probability { get; private set; }
    public string? VehicleCategories { get; private set; }
    public int VehicleQty { get; private set; }
    public int TenureMonths { get; private set; }
    public int? AnnualMileageCapKm { get; private set; }
    public string? Services { get; private set; }
    public DateOnly? ExpectedCloseDate { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string? LostReason { get; private set; }
    public string? Notes { get; private set; }
    public Guid? QuotationId { get; private set; }

    private readonly List<RfqStageHistory> _stageHistory = new();
    public IReadOnlyCollection<RfqStageHistory> StageHistory => _stageHistory.AsReadOnly();

    private readonly List<RfqAttachment> _attachments = new();
    public IReadOnlyCollection<RfqAttachment> Attachments => _attachments.AsReadOnly();

    private Rfq() { }

    public static Rfq Create(RfqCreateInput input)
    {
        if (input.VehicleQty < 1)
            throw new ArgumentOutOfRangeException(nameof(input), "VehicleQty must be >= 1.");
        if (input.TenureMonths < 1 || input.TenureMonths > 96)
            throw new ArgumentOutOfRangeException(nameof(input), "TenureMonths must be 1-96.");

        var rfq = new Rfq
        {
            TenantId = input.TenantId,
            RfqNumber = input.RfqNumber,
            CustomerId = input.CustomerId,
            CrmOpportunityId = input.CrmOpportunityId,
            Source = input.Source,
            Stage = RfqStage.Draft,
            Probability = input.Probability,
            VehicleCategories = input.VehicleCategories,
            VehicleQty = input.VehicleQty,
            TenureMonths = input.TenureMonths,
            AnnualMileageCapKm = input.AnnualMileageCapKm,
            Services = input.Services,
            ExpectedCloseDate = input.ExpectedCloseDate,
            OwnerUserId = input.OwnerUserId,
            Notes = input.Notes,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };

        rfq._stageHistory.Add(RfqStageHistory.Create(
            input.TenantId, rfq.Id, null, RfqStage.Draft, input.OwnerUserId, "Created"));

        return rfq;
    }

    public void TransitionStage(RfqStage toStage, Guid userId, string? comment, DateTimeOffset nowUtc)
    {
        ValidateTransition(Stage, toStage);
        var from = Stage;
        Stage = toStage;
        UpdatedAtUtc = nowUtc;

        if (toStage == RfqStage.Qualified) Probability = Math.Max(Probability, 25);
        if (toStage == RfqStage.Proposal) Probability = Math.Max(Probability, 50);
        if (toStage == RfqStage.Negotiation) Probability = Math.Max(Probability, 70);
        if (toStage == RfqStage.Won) Probability = 100;
        if (toStage == RfqStage.Lost) Probability = 0;

        _stageHistory.Add(RfqStageHistory.Create(TenantId, Id, from, toStage, userId, comment));
    }

    public void MarkWon(Guid quotationId, Guid userId, DateTimeOffset nowUtc)
    {
        if (Stage != RfqStage.Qualified && Stage != RfqStage.Proposal && Stage != RfqStage.Negotiation)
            throw new InvalidOperationException($"Cannot mark Won from stage {Stage}.");

        QuotationId = quotationId;
        TransitionStage(RfqStage.Won, userId, "Converted to quotation", nowUtc);
    }

    public void MarkLost(string reason, Guid userId, DateTimeOffset nowUtc)
    {
        if (Stage == RfqStage.Won)
            throw new InvalidOperationException("Cannot mark a Won RFQ as Lost.");

        LostReason = reason;
        TransitionStage(RfqStage.Lost, userId, reason, nowUtc);
    }

    public void UpdateDetails(RfqUpdateInput input, DateTimeOffset nowUtc)
    {
        if (Stage == RfqStage.Won || Stage == RfqStage.Lost)
            throw new InvalidOperationException($"Cannot update RFQ in stage {Stage}.");

        if (input.VehicleQty.HasValue) VehicleQty = input.VehicleQty.Value;
        if (input.TenureMonths.HasValue) TenureMonths = input.TenureMonths.Value;
        if (input.VehicleCategories is not null) VehicleCategories = input.VehicleCategories;
        if (input.Services is not null) Services = input.Services;
        if (input.AnnualMileageCapKm.HasValue) AnnualMileageCapKm = input.AnnualMileageCapKm.Value;
        if (input.ExpectedCloseDate.HasValue) ExpectedCloseDate = input.ExpectedCloseDate.Value;
        if (input.Notes is not null) Notes = input.Notes;
        if (input.Probability.HasValue) Probability = input.Probability.Value;
        UpdatedAtUtc = nowUtc;
    }

    public void AddAttachment(string fileName, string fileUrl, string? fileType, long? sizeBytes, Guid uploadedBy)
    {
        _attachments.Add(RfqAttachment.Create(TenantId, Id, fileName, fileUrl, fileType, sizeBytes, uploadedBy));
    }

    private static void ValidateTransition(RfqStage from, RfqStage to)
    {
        var valid = (from, to) switch
        {
            (RfqStage.Draft, RfqStage.Qualified) => true,
            (RfqStage.Draft, RfqStage.Lost) => true,
            (RfqStage.Qualified, RfqStage.Proposal) => true,
            (RfqStage.Qualified, RfqStage.Won) => true,
            (RfqStage.Qualified, RfqStage.Lost) => true,
            (RfqStage.Proposal, RfqStage.Negotiation) => true,
            (RfqStage.Proposal, RfqStage.Won) => true,
            (RfqStage.Proposal, RfqStage.Lost) => true,
            (RfqStage.Negotiation, RfqStage.Won) => true,
            (RfqStage.Negotiation, RfqStage.Lost) => true,
            (RfqStage.Lost, RfqStage.Draft) => true,
            _ => false,
        };

        if (!valid)
            throw new InvalidOperationException($"Invalid RFQ stage transition: {from} → {to}.");
    }
}

public sealed record RfqCreateInput
{
    public required Guid TenantId { get; init; }
    public required string RfqNumber { get; init; }
    public required Guid CustomerId { get; init; }
    public string? CrmOpportunityId { get; init; }
    public required RfqSource Source { get; init; }
    public int Probability { get; init; } = 10;
    public string? VehicleCategories { get; init; }
    public required int VehicleQty { get; init; }
    public required int TenureMonths { get; init; }
    public int? AnnualMileageCapKm { get; init; }
    public string? Services { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public required Guid OwnerUserId { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}

public sealed record RfqUpdateInput
{
    public int? VehicleQty { get; init; }
    public int? TenureMonths { get; init; }
    public string? VehicleCategories { get; init; }
    public string? Services { get; init; }
    public int? AnnualMileageCapKm { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public string? Notes { get; init; }
    public int? Probability { get; init; }
}
