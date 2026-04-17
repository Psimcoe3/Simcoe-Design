using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Compares arc-flash mitigation scenarios against a baseline operating condition.
/// </summary>
public static class ArcFlashMitigationService
{
    public record MitigationScenario
    {
        public string Name { get; init; } = string.Empty;
        public double ArcDurationSeconds { get; init; } = 0.5;
        public double WorkingDistanceInches { get; init; } = 18.0;
        public double? SystemVoltageV { get; init; }
    }

    public record ScenarioResult
    {
        public string Name { get; init; } = string.Empty;
        public double IncidentEnergyCal { get; init; }
        public double ArcFlashBoundaryInches { get; init; }
        public int HazardCategory { get; init; }
        public double EnergyReductionPercent { get; init; }
    }

    public record MitigationSummary
    {
        public string NodeId { get; init; } = string.Empty;
        public double BaselineIncidentEnergyCal { get; init; }
        public int BaselineHazardCategory { get; init; }
        public ScenarioResult BestScenario { get; init; } = new();
        public List<ScenarioResult> ScenarioResults { get; init; } = new();
    }

    public static MitigationSummary EvaluateScenarios(
        DistributionNode node,
        IEnumerable<MitigationScenario> scenarios,
        ShortCircuitService shortCircuitService,
        double baselineWorkingDistanceInches = 18.0,
        double baselineArcDurationSeconds = 0.5,
        double baselineSystemVoltageV = 480.0)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(shortCircuitService);

        var baseline = shortCircuitService.CalculateArcFlash(
            node,
            baselineWorkingDistanceInches,
            baselineArcDurationSeconds,
            baselineSystemVoltageV);

        var results = (scenarios ?? Array.Empty<MitigationScenario>())
            .Select(scenario => EvaluateScenario(node, scenario, shortCircuitService, baseline, baselineSystemVoltageV))
            .OrderBy(result => result.IncidentEnergyCal)
            .ToList();

        if (results.Count == 0)
            throw new ArgumentException("At least one mitigation scenario is required.");

        return new MitigationSummary
        {
            NodeId = node.Id,
            BaselineIncidentEnergyCal = baseline.IncidentEnergyCal,
            BaselineHazardCategory = baseline.HazardCategory,
            BestScenario = results[0],
            ScenarioResults = results,
        };
    }

    public static ScenarioResult EvaluateScenario(
        DistributionNode node,
        MitigationScenario scenario,
        ShortCircuitService shortCircuitService,
        ArcFlashResult baseline,
        double baselineSystemVoltageV = 480.0)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(shortCircuitService);
        ArgumentNullException.ThrowIfNull(baseline);

        var result = shortCircuitService.CalculateArcFlash(
            node,
            scenario.WorkingDistanceInches,
            scenario.ArcDurationSeconds,
            scenario.SystemVoltageV ?? baselineSystemVoltageV);

        double reductionPercent = baseline.IncidentEnergyCal > 0
            ? Math.Round((baseline.IncidentEnergyCal - result.IncidentEnergyCal) / baseline.IncidentEnergyCal * 100.0, 2)
            : 0;

        return new ScenarioResult
        {
            Name = scenario.Name,
            IncidentEnergyCal = result.IncidentEnergyCal,
            ArcFlashBoundaryInches = result.ArcFlashBoundaryInches,
            HazardCategory = result.HazardCategory,
            EnergyReductionPercent = reductionPercent,
        };
    }
}