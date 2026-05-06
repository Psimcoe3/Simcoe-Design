using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class ProjectValidationServiceTests
{
    [Fact]
    public void Conduit_WithExcessiveFill_ReportsConduitFillFinding()
    {
        var svc = CreateService();
        var conduit = new ConduitComponent
        {
            Id = "COND-1",
            Name = "Conduit 1",
            Diameter = 0.5,
            ConduitType = "EMT"
        };
        var circuits = Enumerable.Range(1, 5)
            .Select(index => new Circuit
            {
                CircuitNumber = $"C{index}",
                ConduitIds = { conduit.Id },
                Wire = new WireSpec { Size = "10", Conductors = 3 },
                SlotType = CircuitSlotType.Circuit,
            })
            .ToList();

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = new List<ElectricalComponent> { conduit },
            Circuits = circuits,
        });

        var findings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.ConduitFill)
            .ToList();

        Assert.NotEmpty(findings);
        Assert.Contains(findings, finding => finding.ComponentId == conduit.Id);
    }

    [Fact]
    public void Conduit_WithinFillLimits_DoesNotReportConduitFillFinding()
    {
        var svc = CreateService();
        var conduit = new ConduitComponent
        {
            Id = "COND-OK",
            Name = "Conduit OK",
            Diameter = 1.5,
            ConduitType = "EMT"
        };
        var circuit = new Circuit
        {
            CircuitNumber = "C1",
            ConduitIds = { conduit.Id },
            Wire = new WireSpec { Size = "12", Conductors = 2 },
            SlotType = CircuitSlotType.Circuit,
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = new List<ElectricalComponent> { conduit },
            Circuits = new List<Circuit> { circuit },
        });

        Assert.DoesNotContain(report.Findings,
            finding => finding.Category == ProjectValidationService.FindingCategory.ConduitFill);
    }

    [Fact]
    public void ImportedComponent_WithoutCurrentReview_ReportsInteropReviewWarning()
    {
        var svc = CreateService();
        var imported = new PanelComponent
        {
            Id = "IMP-1",
            Name = "Imported Panel",
            Subtype = PanelSubtype.Panelboard,
            InteropMetadata = new ComponentInteropMetadata
            {
                SourceSystem = "IFC",
                SourceDocumentName = "coord.ifc",
                LastImportedUtc = DateTime.UtcNow,
                ReviewStatus = ComponentInteropReviewStatus.Unreviewed,
            }
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = new List<ElectricalComponent> { imported },
        });

        var finding = Assert.Single(report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.InteropReview));
        Assert.Equal(ProjectValidationService.FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void ImportedComponent_MarkedNeedsChanges_ReportsInteropReviewError()
    {
        var svc = CreateService();
        var imported = new PanelComponent
        {
            Id = "IMP-2",
            Name = "Imported MDP",
            Subtype = PanelSubtype.Switchboard,
            InteropMetadata = new ComponentInteropMetadata
            {
                SourceSystem = "Revit",
                SourceDocumentName = "mdp.rvt",
                LastImportedUtc = DateTime.UtcNow,
                ReviewStatus = ComponentInteropReviewStatus.NeedsChanges,
                ReviewNote = "Clearance check required"
            }
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = new List<ElectricalComponent> { imported },
        });

        var finding = Assert.Single(report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.InteropReview));
        Assert.Equal(ProjectValidationService.FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void ImportedComponent_ReviewedAfterImport_DoesNotReportInteropReviewFinding()
    {
        var svc = CreateService();
        var importedAt = DateTime.UtcNow.AddHours(-2);
        var reviewedAt = importedAt.AddHours(1);
        var imported = new PanelComponent
        {
            Id = "IMP-3",
            Name = "Reviewed Import",
            Subtype = PanelSubtype.Panelboard,
            InteropMetadata = new ComponentInteropMetadata
            {
                SourceSystem = "IFC",
                SourceDocumentName = "reviewed.ifc",
                LastImportedUtc = importedAt,
                LastReviewedUtc = reviewedAt,
                ReviewStatus = ComponentInteropReviewStatus.Reviewed,
                ReviewedBy = "QA"
            }
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = new List<ElectricalComponent> { imported },
        });

        Assert.DoesNotContain(report.Findings,
            finding => finding.Category == ProjectValidationService.FindingCategory.InteropReview);
    }

    [Fact]
    public void Panel_WithHighIncidentEnergy_ReportsArcFlashWarning()
    {
        var svc = CreateService();
        var schedule = MakeThreePhaseSchedule("AF-1", "Panel AF-1",
            new[] { 2000.0, 2000.0, 2000.0 });
        schedule.AvailableFaultCurrentKA = 25;
        schedule.MainBreakerAmps = 400;
        schedule.VoltageConfig = PanelVoltageConfig.V277_480_3Ph;

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });

        var finding = Assert.Single(report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.ArcFlash));
        Assert.Equal(ProjectValidationService.FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Panel_WithExtremeIncidentEnergy_ReportsArcFlashError()
    {
        var svc = CreateService();
        var schedule = MakeThreePhaseSchedule("AF-2", "Panel AF-2",
            new[] { 2000.0, 2000.0, 2000.0 });
        schedule.AvailableFaultCurrentKA = 100;
        schedule.MainBreakerAmps = 1200;
        schedule.VoltageConfig = PanelVoltageConfig.V277_480_3Ph;

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });

        var finding = Assert.Single(report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.ArcFlash));
        Assert.Equal(ProjectValidationService.FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Panel_WithLowIncidentEnergy_DoesNotReportArcFlashFinding()
    {
        var svc = CreateService();
        var schedule = MakeThreePhaseSchedule("AF-3", "Panel AF-3",
            new[] { 500.0, 500.0, 500.0 });
        schedule.AvailableFaultCurrentKA = 2;
        schedule.MainBreakerAmps = 100;
        schedule.VoltageConfig = PanelVoltageConfig.V120_208_3Ph;

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });

        Assert.DoesNotContain(report.Findings,
            finding => finding.Category == ProjectValidationService.FindingCategory.ArcFlash);
    }
}