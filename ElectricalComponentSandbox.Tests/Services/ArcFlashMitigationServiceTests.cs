using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ArcFlashMitigationServiceTests
{
    private static DistributionNode CreateNode() => new()
    {
        Id = "BUS-1",
        Name = "Main Bus",
        FaultCurrentKA = 35,
        NodeType = ComponentType.Bus,
        Component = new BusComponent { Voltage = 480 },
    };

    [Fact]
    public void EvaluateScenario_ReducedArcDurationLowersIncidentEnergy()
    {
        var service = new ShortCircuitService();
        var node = CreateNode();
        var baseline = service.CalculateArcFlash(node, 18, 0.5, 480);

        var result = ArcFlashMitigationService.EvaluateScenario(
            node,
            new ArcFlashMitigationService.MitigationScenario { Name = "Maintenance switch", ArcDurationSeconds = 0.08, WorkingDistanceInches = 18 },
            service,
            baseline,
            480);

        Assert.True(result.IncidentEnergyCal < baseline.IncidentEnergyCal);
        Assert.True(result.EnergyReductionPercent > 0);
    }

    [Fact]
    public void EvaluateScenarios_SelectsLowestEnergyScenario()
    {
        var service = new ShortCircuitService();
        var summary = ArcFlashMitigationService.EvaluateScenarios(
            CreateNode(),
            new[]
            {
                new ArcFlashMitigationService.MitigationScenario { Name = "Remote racking", ArcDurationSeconds = 0.5, WorkingDistanceInches = 36 },
                new ArcFlashMitigationService.MitigationScenario { Name = "Maintenance switch", ArcDurationSeconds = 0.08, WorkingDistanceInches = 18 },
            },
            service,
            baselineWorkingDistanceInches: 18,
            baselineArcDurationSeconds: 0.5,
            baselineSystemVoltageV: 480);

        Assert.Equal("Maintenance switch", summary.BestScenario.Name);
        Assert.True(summary.BestScenario.IncidentEnergyCal <= summary.ScenarioResults[1].IncidentEnergyCal);
    }

    [Fact]
    public void EvaluateScenarios_RequiresAtLeastOneScenario()
    {
        var service = new ShortCircuitService();

        Assert.Throws<ArgumentException>(() => ArcFlashMitigationService.EvaluateScenarios(CreateNode(), System.Array.Empty<ArcFlashMitigationService.MitigationScenario>(), service));
    }

    [Fact]
    public void EvaluateScenarios_TracksBaselineHazardCategory()
    {
        var service = new ShortCircuitService();
        var summary = ArcFlashMitigationService.EvaluateScenarios(
            CreateNode(),
            new[]
            {
                new ArcFlashMitigationService.MitigationScenario { Name = "Higher distance", ArcDurationSeconds = 0.5, WorkingDistanceInches = 30 },
            },
            service);

        Assert.True(summary.BaselineIncidentEnergyCal > 0);
        Assert.InRange(summary.BaselineHazardCategory, 0, 4);
    }
}