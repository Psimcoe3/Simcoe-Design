using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public partial class ProjectValidationServiceTests
{
    [Fact]
    public void Schedule_WithBundledCircuits_ReportsBundleDeratingFinding()
    {
        var svc = CreateService();
        var schedule = new PanelSchedule
        {
            PanelId = "BD-1",
            PanelName = "Panel BD-1",
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            MainBreakerAmps = 225,
            BusAmps = 225,
            Circuits = new List<Circuit>
            {
                BuildBundledCircuit("1", "CND-1"),
                BuildBundledCircuit("3", "CND-1"),
            }
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });

        var findings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.BundleDerating)
            .ToList();

        Assert.NotEmpty(findings);
        Assert.Contains(findings, finding => finding.ComponentId == "1" || finding.ComponentId == "3");
        Assert.All(findings, finding => Assert.Equal(ProjectValidationService.FindingSeverity.Warning, finding.Severity));
    }

    [Fact]
    public void Schedule_WithNoSharedConduits_DoesNotReportBundleDeratingFinding()
    {
        var svc = CreateService();
        var schedule = new PanelSchedule
        {
            PanelId = "BD-2",
            PanelName = "Panel BD-2",
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            MainBreakerAmps = 225,
            BusAmps = 225,
            Circuits = new List<Circuit>
            {
                BuildBundledCircuit("1", "CND-1"),
                BuildBundledCircuit("3", "CND-2"),
            }
        };

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });

        Assert.DoesNotContain(
            report.Findings,
            finding => finding.Category == ProjectValidationService.FindingCategory.BundleDerating);
    }

    [Fact]
    public void CustomPhaseImbalanceThreshold_TriggersExpectedPhaseBalanceFinding()
    {
        var svc = CreateService();
        var schedule = MakeThreePhaseSchedule("PB-OV", "Panel PB-OV", new[] { 9000.0, 4500.0, 4500.0 });

        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
            MaxPhaseImbalancePercent = 15.0,
        });

        var finding = Assert.Single(report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.PhaseBalance));
        Assert.Equal(ProjectValidationService.FindingSeverity.Error, finding.Severity);
        Assert.Contains("Panel PB-OV", finding.Description);
    }

    private static Circuit BuildBundledCircuit(string circuitNumber, string conduitId)
    {
        var circuit = new Circuit
        {
            CircuitNumber = circuitNumber,
            Voltage = 120,
            Poles = 1,
            Phase = "A",
            Breaker = new CircuitBreaker { TripAmps = 30, Poles = 1 },
            ConnectedLoadVA = 1800,
            DemandFactor = 1.0,
            Wire = new WireSpec
            {
                Size = "12",
                Conductors = 3,
                GroundSize = "12",
                Material = ConductorMaterial.Copper,
            },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Power,
        };

        circuit.ConduitIds.Add(conduitId);
        return circuit;
    }
}
