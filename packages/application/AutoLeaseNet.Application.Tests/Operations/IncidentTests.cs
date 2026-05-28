using AutoLeaseNet.Domain.Operations;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Operations;

/// <summary>
/// Domain coverage for <see cref="Incident"/>: state-machine transitions per Spec 02
/// §4.7, RequiresReplacement derivation, claim-mutation guard, and idempotent re-entry.
/// </summary>
public sealed class IncidentTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa1111-0000-0000-0000-000000000001");
    private static readonly Guid VehicleId = Guid.Parse("bbbb1111-0000-0000-0000-000000000001");
    private static readonly Guid LeaseId = Guid.Parse("cccc1111-0000-0000-0000-000000000001");
    private static readonly Guid ReporterId = Guid.Parse("dddd1111-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private static Incident NewMinorReport(IncidentSeverity severity = IncidentSeverity.Minor) =>
        Incident.Report(new ReportIncidentInput
        {
            TenantId = TenantId,
            VehicleId = VehicleId,
            LeaseId = LeaseId,
            ReportedByPersonId = ReporterId,
            Type = IncidentType.TrafficAccident,
            Severity = severity,
            IncidentTimeUtc = Now.AddHours(-1),
            Description = "Minor bumper graze",
            NowUtc = Now,
        });

    [Fact]
    public void Report_starts_Open_and_raises_IncidentReportedDomainEvent()
    {
        var incident = NewMinorReport();

        incident.Status.Should().Be(IncidentStatus.Open);
        incident.ReportedAtUtc.Should().Be(Now);
        incident.RequiresReplacement.Should().BeFalse(because: "Minor severity does not trigger replacement");
        incident.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IncidentReportedDomainEvent>();
        var evt = (IncidentReportedDomainEvent)incident.DomainEvents.Single();
        evt.IncidentId.Should().Be(incident.Id);
        evt.RequiresReplacement.Should().BeFalse();
    }

    [Fact]
    public void Report_with_TotalLoss_severity_flags_RequiresReplacement()
    {
        var incident = NewMinorReport(IncidentSeverity.TotalLoss);

        incident.RequiresReplacement.Should().BeTrue();
        ((IncidentReportedDomainEvent)incident.DomainEvents.Single()).RequiresReplacement.Should().BeTrue();
    }

    [Fact]
    public void Report_rejects_future_IncidentTimeUtc()
    {
        var act = () => Incident.Report(new ReportIncidentInput
        {
            TenantId = TenantId, VehicleId = VehicleId, ReportedByPersonId = ReporterId,
            Type = IncidentType.TrafficAccident, Severity = IncidentSeverity.Minor,
            IncidentTimeUtc = Now.AddMinutes(1),
            Description = "x", NowUtc = Now,
        });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StartInvestigation_moves_Open_to_UnderInvestigation()
    {
        var incident = NewMinorReport();

        incident.StartInvestigation(Now.AddHours(1));

        incident.Status.Should().Be(IncidentStatus.UnderInvestigation);
        incident.InvestigationStartedAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void StartInvestigation_is_idempotent_on_second_call()
    {
        var incident = NewMinorReport();
        incident.StartInvestigation(Now.AddHours(1));
        var firstStartedAt = incident.InvestigationStartedAtUtc;

        incident.StartInvestigation(Now.AddHours(2));

        incident.Status.Should().Be(IncidentStatus.UnderInvestigation);
        incident.InvestigationStartedAtUtc.Should().Be(firstStartedAt, because: "first-call timestamp wins on idempotent re-entry");
    }

    [Fact]
    public void StartInvestigation_rejects_from_Closed()
    {
        var incident = NewMinorReport();
        incident.MarkClosed(Now.AddHours(1));

        var act = () => incident.StartInvestigation(Now.AddHours(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkResolved_moves_Open_directly_to_Resolved_with_notes()
    {
        var incident = NewMinorReport();

        incident.MarkResolved("Polished on-site", Now.AddHours(2));

        incident.Status.Should().Be(IncidentStatus.Resolved);
        incident.ResolutionNotes.Should().Be("Polished on-site");
        incident.ResolvedAtUtc.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public void MarkResolved_rejects_when_already_Closed()
    {
        var incident = NewMinorReport();
        incident.MarkClosed(Now.AddHours(1));

        var act = () => incident.MarkResolved("late note", Now.AddHours(2));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already Closed*");
    }

    [Fact]
    public void MarkClosed_from_any_non_terminal_state_terminates_aggregate()
    {
        var fromOpen = NewMinorReport();
        var fromInvestigation = NewMinorReport();
        var fromResolved = NewMinorReport();

        fromInvestigation.StartInvestigation(Now.AddHours(1));
        fromResolved.MarkResolved("x", Now.AddHours(1));

        fromOpen.MarkClosed(Now.AddHours(2));
        fromInvestigation.MarkClosed(Now.AddHours(2));
        fromResolved.MarkClosed(Now.AddHours(2));

        fromOpen.Status.Should().Be(IncidentStatus.Closed);
        fromInvestigation.Status.Should().Be(IncidentStatus.Closed);
        fromResolved.Status.Should().Be(IncidentStatus.Closed);
    }

    [Fact]
    public void UpdateClaim_appends_provided_fields_only()
    {
        var incident = NewMinorReport();

        incident.UpdateClaim(policeReportNumber: "RP-2026-0001", insuranceClaimNumber: null, Now.AddHours(2));
        incident.UpdateClaim(policeReportNumber: null, insuranceClaimNumber: "IC-99-X", Now.AddHours(3));

        incident.PoliceReportNumber.Should().Be("RP-2026-0001", because: "second call must not overwrite with null");
        incident.InsuranceClaimNumber.Should().Be("IC-99-X");
    }

    [Fact]
    public void UpdateClaim_rejects_when_Closed()
    {
        var incident = NewMinorReport();
        incident.MarkClosed(Now.AddHours(1));

        var act = () => incident.UpdateClaim("RP-x", null, Now.AddHours(2));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public void LinkReplacementLease_sets_back_reference_once_and_then_idempotent()
    {
        var incident = NewMinorReport(IncidentSeverity.TotalLoss);
        var replacementId = Guid.NewGuid();

        incident.LinkReplacementLease(replacementId, Now.AddHours(2));
        incident.LinkReplacementLease(replacementId, Now.AddHours(3)); // idempotent

        incident.ReplacementLeaseId.Should().Be(replacementId);
    }

    [Fact]
    public void LinkReplacementLease_rejects_relink_to_different_lease()
    {
        var incident = NewMinorReport(IncidentSeverity.TotalLoss);
        incident.LinkReplacementLease(Guid.NewGuid(), Now);

        var act = () => incident.LinkReplacementLease(Guid.NewGuid(), Now.AddHours(1));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already linked*");
    }
}
