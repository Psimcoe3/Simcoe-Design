using System;
using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Builds a project-level protection-program summary from the current
/// distribution graph and available short-circuit data.
/// </summary>
public class ProjectProtectionProgramService
{
    private readonly DistributionGraphService _graphService;
    private readonly ShortCircuitService _shortCircuitService;

    private sealed record EstimatedRelayContext
    {
        public required DistributionNode Node { get; init; }
        public required ProtectiveRelayService.RelaySettings BaselineSettings { get; init; }
        public required ProtectiveRelayService.RelaySettings StudySettings { get; init; }
        public int Depth { get; init; }
        public double LoadAmps { get; init; }
    }

    private static readonly ArcFlashMitigationService.MitigationScenario[] DefaultMitigationScenarios =
    {
        new() { Name = "Maintenance switch", ArcDurationSeconds = 0.08, WorkingDistanceInches = 18.0 },
        new() { Name = "Remote racking", ArcDurationSeconds = 0.5, WorkingDistanceInches = 36.0 },
    };

    public ProjectProtectionProgramService(
        DistributionGraphService? graphService = null,
        ShortCircuitService? shortCircuitService = null)
    {
        _graphService = graphService ?? new DistributionGraphService();
        _shortCircuitService = shortCircuitService ?? new ShortCircuitService();
    }

    public ProtectionProgramService.ProgramReport BuildReport(IEnumerable<ElectricalComponent> components)
        => BuildReport(components, null);

    public ProtectionProgramService.ProgramReport BuildReport(
        IEnumerable<ElectricalComponent> components,
        IEnumerable<PanelSchedule>? panelSchedules)
    {
        var componentList = (components ?? Array.Empty<ElectricalComponent>()).ToList();
        if (componentList.Count == 0)
            return new ProtectionProgramService.ProgramReport();

        var roots = _graphService.BuildGraph(componentList);
        if (roots.Count == 0)
            return new ProtectionProgramService.ProgramReport();

        _graphService.ComputeCumulativeDemand(roots, BuildPanelDemandMap(panelSchedules));
        _graphService.PropagateFaultCurrent(roots);

        var studyNodes = FlattenNodes(roots)
            .Where(node => node.NodeType != ComponentType.PowerSource && node.FaultCurrentKA > 0)
            .ToList();
        if (studyNodes.Count == 0)
            return new ProtectionProgramService.ProgramReport();

        var dutyResults = _shortCircuitService.ValidateAIC(roots)
            .Where(result => result.NodeType != ComponentType.PowerSource && result.AvailableFaultKA > 0)
            .ToList();
        var dutySummary = InterruptingDutyAuditService.AuditResults(dutyResults);

        var nodeLookup = studyNodes.ToDictionary(node => node.Id, node => node, StringComparer.Ordinal);
        var voltageLookup = BuildVoltageLookup(roots);
        var relayContexts = BuildRelayContexts(roots, voltageLookup);
        var relayAudits = BuildRelayAudits(relayContexts);
        var coordinationSweeps = BuildCoordinationSweeps(roots, relayContexts);
        var mitigations = BuildMitigationSummaries(studyNodes, voltageLookup);
        var upgrades = BuildUpgradeRecommendations(relayAudits, coordinationSweeps, dutySummary, mitigations, nodeLookup);

        return ProtectionProgramService.BuildReport(
            relayAudits,
            coordinationSweeps,
            dutySummary,
            mitigations,
            upgrades);
    }

    public static bool HasMeaningfulContent(ProtectionProgramService.ProgramReport? report)
    {
        return report is not null
            && (report.ReadinessScore > 0
                || report.DutyViolationCount > 0
                || report.AverageArcFlashReductionPercent > 0
                || report.Actions.Count > 0
                || report.RecommendedUpgrades.Count > 0);
    }

    private List<ArcFlashMitigationService.MitigationSummary> BuildMitigationSummaries(
        IReadOnlyList<DistributionNode> studyNodes,
        IReadOnlyDictionary<string, double> voltageLookup)
    {
        var summaries = new List<ArcFlashMitigationService.MitigationSummary>();

        foreach (var node in studyNodes)
        {
            double systemVoltage = voltageLookup.TryGetValue(node.Id, out var voltage) ? voltage : 480.0;
            var baseline = _shortCircuitService.CalculateArcFlash(node, systemVoltageV: systemVoltage);
            if (baseline.IncidentEnergyCal < 4.0)
                continue;

            var summary = ArcFlashMitigationService.EvaluateScenarios(
                node,
                DefaultMitigationScenarios,
                _shortCircuitService,
                baselineSystemVoltageV: systemVoltage);

            if (summary.BestScenario.EnergyReductionPercent > 0)
                summaries.Add(summary);
        }

        return summaries
            .OrderByDescending(summary => summary.BaselineIncidentEnergyCal)
            .ToList();
    }

    private static IReadOnlyDictionary<string, double> BuildPanelDemandMap(IEnumerable<PanelSchedule>? panelSchedules)
    {
        return (panelSchedules ?? Array.Empty<PanelSchedule>())
            .Where(schedule => !string.IsNullOrWhiteSpace(schedule.PanelId))
            .GroupBy(schedule => schedule.PanelId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().TotalDemandVA,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, EstimatedRelayContext> BuildRelayContexts(
        IEnumerable<DistributionNode> roots,
        IReadOnlyDictionary<string, double> voltageLookup)
    {
        var contexts = new Dictionary<string, EstimatedRelayContext>(StringComparer.Ordinal);
        foreach (var root in roots)
            BuildRelayContextsRecursive(root, 0, voltageLookup, contexts);

        return contexts;
    }

    private static void BuildRelayContextsRecursive(
        DistributionNode node,
        int depth,
        IReadOnlyDictionary<string, double> voltageLookup,
        IDictionary<string, EstimatedRelayContext> contexts)
    {
        if (node.NodeType != ComponentType.PowerSource)
        {
            double voltage = voltageLookup.TryGetValue(node.Id, out var resolvedVoltage) ? resolvedVoltage : 480.0;
            double loadAmps = ResolveLoadAmps(node, voltage);
            var baselineSettings = ResolveFieldRelaySettings(node, loadAmps);
            var studySettings = ResolveStudyRelaySettings(node, loadAmps, depth, baselineSettings.CtRatio);

            contexts[node.Id] = new EstimatedRelayContext
            {
                Node = node,
                BaselineSettings = baselineSettings,
                StudySettings = studySettings,
                Depth = depth,
                LoadAmps = loadAmps,
            };
        }

        foreach (var child in node.Children)
            BuildRelayContextsRecursive(child, depth + 1, voltageLookup, contexts);
    }

    private static List<RelaySettingsAuditService.RelayAuditResult> BuildRelayAudits(
        IReadOnlyDictionary<string, EstimatedRelayContext> relayContexts)
    {
        return relayContexts.Values
            .Select(context => RelaySettingsAuditService.AuditSettings(context.StudySettings, context.BaselineSettings))
            .Where(audit => audit.WarningCount > 0 || audit.CriticalCount > 0)
            .OrderByDescending(audit => audit.CriticalCount)
            .ThenByDescending(audit => audit.WarningCount)
            .ToList();
    }

    private static List<CoordinationSweepService.SweepSummary> BuildCoordinationSweeps(
        IEnumerable<DistributionNode> roots,
        IReadOnlyDictionary<string, EstimatedRelayContext> relayContexts)
    {
        var sweeps = new List<CoordinationSweepService.SweepSummary>();

        foreach (var root in roots)
            BuildCoordinationSweepsRecursive(root, relayContexts, sweeps);

        return sweeps
            .OrderBy(summary => summary.MinimumMarginSec)
            .ToList();
    }

    private static void BuildCoordinationSweepsRecursive(
        DistributionNode node,
        IReadOnlyDictionary<string, EstimatedRelayContext> relayContexts,
        ICollection<CoordinationSweepService.SweepSummary> sweeps)
    {
        foreach (var child in node.Children)
        {
            if (relayContexts.TryGetValue(node.Id, out var parentContext)
                && relayContexts.TryGetValue(child.Id, out var childContext)
                && child.FaultCurrentKA > 0)
            {
                double minimumFaultCurrent = Math.Max(childContext.BaselineSettings.PickupAmps * 2.0, child.FaultCurrentKA * 1000.0 * 0.35);
                double maximumFaultCurrent = Math.Max(minimumFaultCurrent, child.FaultCurrentKA * 1000.0);

                sweeps.Add(CoordinationSweepService.SweepRange(
                    parentContext.BaselineSettings,
                    childContext.BaselineSettings,
                    minimumFaultCurrent,
                    maximumFaultCurrent,
                    pointCount: 6,
                    minimumCtiSec: 0.35));
            }

            BuildCoordinationSweepsRecursive(child, relayContexts, sweeps);
        }
    }

    private static List<ProtectionUpgradePlannerService.UpgradeRecommendation> BuildUpgradeRecommendations(
        IReadOnlyList<RelaySettingsAuditService.RelayAuditResult> relayAudits,
        IReadOnlyList<CoordinationSweepService.SweepSummary> coordinationSweeps,
        InterruptingDutyAuditService.DutyAuditSummary dutySummary,
        IReadOnlyList<ArcFlashMitigationService.MitigationSummary> mitigations,
        IReadOnlyDictionary<string, DistributionNode> nodeLookup)
    {
        var candidates = new List<ProtectionUpgradePlannerService.UpgradeCandidate>();

        AddCoordinationUpgradeCandidates(candidates, relayAudits, coordinationSweeps, nodeLookup);
        AddDutyUpgradeCandidates(candidates, dutySummary, mitigations, nodeLookup);
        AddMitigationUpgradeCandidates(candidates, mitigations, nodeLookup);

        return ProtectionUpgradePlannerService.RankCandidates(candidates);
    }

    private static void AddCoordinationUpgradeCandidates(
        ICollection<ProtectionUpgradePlannerService.UpgradeCandidate> candidates,
        IReadOnlyList<RelaySettingsAuditService.RelayAuditResult> relayAudits,
        IReadOnlyList<CoordinationSweepService.SweepSummary> coordinationSweeps,
        IReadOnlyDictionary<string, DistributionNode> nodeLookup)
    {
        foreach (var sweep in coordinationSweeps.Where(summary => summary.Violations.Count > 0).Take(3))
        {
            string downstreamName = nodeLookup.TryGetValue(sweep.DownstreamId, out var downstreamNode)
                ? downstreamNode.Name
                : sweep.DownstreamId;
            var relayAudit = relayAudits.FirstOrDefault(audit => string.Equals(audit.RelayId, sweep.DownstreamId, StringComparison.Ordinal))
                ?? new RelaySettingsAuditService.RelayAuditResult { RelayId = sweep.DownstreamId };

            candidates.Add(ProtectionUpgradePlannerService.CreateSettingsChangeCandidate(
                $"Retune protection at {downstreamName}",
                relayAudit,
                sweep,
                estimatedCost: 3500,
                arcFlashReductionPercent: 0));
        }
    }

    private static void AddDutyUpgradeCandidates(
        ICollection<ProtectionUpgradePlannerService.UpgradeCandidate> candidates,
        InterruptingDutyAuditService.DutyAuditSummary dutySummary,
        IReadOnlyList<ArcFlashMitigationService.MitigationSummary> mitigations,
        IReadOnlyDictionary<string, DistributionNode> nodeLookup)
    {
        foreach (var exposure in dutySummary.Exposures
                     .Where(exposure => exposure.Severity is InterruptingDutyAuditService.DutySeverity.High
                         or InterruptingDutyAuditService.DutySeverity.Critical)
                     .Take(3))
        {
            if (!nodeLookup.TryGetValue(exposure.NodeId, out var node))
                continue;

            var mitigation = mitigations.FirstOrDefault(summary => string.Equals(summary.NodeId, exposure.NodeId, StringComparison.Ordinal))?.BestScenario;
            candidates.Add(ProtectionUpgradePlannerService.CreateEquipmentUpgradeCandidate(
                $"Upgrade interrupting rating at {node.Name}",
                exposure,
                mitigation,
                EstimateEquipmentUpgradeCost(node, exposure.Severity)));
        }
    }

    private static void AddMitigationUpgradeCandidates(
        ICollection<ProtectionUpgradePlannerService.UpgradeCandidate> candidates,
        IReadOnlyList<ArcFlashMitigationService.MitigationSummary> mitigations,
        IReadOnlyDictionary<string, DistributionNode> nodeLookup)
    {
        foreach (var mitigation in mitigations
                     .Where(summary => summary.BaselineHazardCategory >= 2 && summary.BestScenario.EnergyReductionPercent >= 20)
                     .Take(3))
        {
            if (!nodeLookup.TryGetValue(mitigation.NodeId, out var node))
                continue;

            candidates.Add(new ProtectionUpgradePlannerService.UpgradeCandidate
            {
                Name = $"Install {mitigation.BestScenario.Name} at {node.Name}",
                Type = ResolveMitigationUpgradeType(mitigation.BestScenario.Name),
                EstimatedCost = EstimateMitigationUpgradeCost(mitigation.BestScenario.Name),
                ArcFlashReductionPercent = mitigation.BestScenario.EnergyReductionPercent,
            });
        }
    }

    private static double EstimateEquipmentUpgradeCost(
        DistributionNode node,
        InterruptingDutyAuditService.DutySeverity severity)
    {
        double baseCost = node.Component switch
        {
            PanelComponent panel when panel.Subtype is PanelSubtype.Switchboard or PanelSubtype.MCCSection => 18000,
            PanelComponent => 12000,
            TransferSwitchComponent => 22000,
            BusComponent => 20000,
            TransformerComponent => 28000,
            _ => 10000,
        };

        return severity == InterruptingDutyAuditService.DutySeverity.Critical
            ? baseCost * 1.25
            : baseCost;
    }

    private static double EstimateMitigationUpgradeCost(string scenarioName)
    {
        return scenarioName.Contains("Maintenance switch", StringComparison.OrdinalIgnoreCase)
            ? 6500
            : 2500;
    }

    private static ProtectionUpgradePlannerService.UpgradeType ResolveMitigationUpgradeType(string scenarioName)
    {
        return scenarioName.Contains("Maintenance switch", StringComparison.OrdinalIgnoreCase)
            ? ProtectionUpgradePlannerService.UpgradeType.MaintenanceSwitch
            : ProtectionUpgradePlannerService.UpgradeType.SettingsChange;
    }

    private static Dictionary<string, double> BuildVoltageLookup(IEnumerable<DistributionNode> roots)
    {
        var lookup = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            double startingVoltage = root.Component is PowerSourceComponent source ? source.Voltage : 480.0;
            PopulateVoltageLookup(root, startingVoltage, lookup);
        }

        return lookup;
    }

    private static void PopulateVoltageLookup(DistributionNode node, double inheritedVoltage, IDictionary<string, double> lookup)
    {
        double voltage = node.Component switch
        {
            TransformerComponent transformer => transformer.SecondaryVoltage,
            BusComponent bus => bus.Voltage,
            PowerSourceComponent source => source.Voltage,
            _ => inheritedVoltage,
        };

        lookup[node.Id] = voltage;

        foreach (var child in node.Children)
            PopulateVoltageLookup(child, voltage, lookup);
    }

    private static List<DistributionNode> FlattenNodes(IEnumerable<DistributionNode> roots)
    {
        var nodes = new List<DistributionNode>();
        foreach (var root in roots)
            FlattenNode(root, nodes);

        return nodes;
    }

    private static void FlattenNode(DistributionNode node, List<DistributionNode> nodes)
    {
        nodes.Add(node);
        foreach (var child in node.Children)
            FlattenNode(child, nodes);
    }

    private static double ResolveLoadAmps(DistributionNode node, double voltage)
    {
        if (voltage <= 0)
            return Math.Max(ResolveRatedAmps(node.Component) * 0.6, 25.0);

        if (node.CumulativeDemandVA > 0)
        {
            double divisor = IsThreePhase(node.Component) ? voltage * Math.Sqrt(3) : voltage;
            if (divisor > 0)
                return Math.Max(node.CumulativeDemandVA / divisor, 25.0);
        }

        return Math.Max(ResolveRatedAmps(node.Component) * 0.6, 25.0);
    }

    private static ProtectiveRelayService.RelaySettings CreateBaselineRelaySettings(DistributionNode node, double loadAmps)
    {
        double ctRatio = ResolveCtRatio(loadAmps);
        var pickup = ProtectiveRelayService.RecommendPickup(loadAmps, ctRatio, ProtectiveRelayService.RelayFunction.Function51);

        return new ProtectiveRelayService.RelaySettings
        {
            Id = node.Id,
            Function = ProtectiveRelayService.RelayFunction.Function51,
            Curve = ProtectiveRelayService.CurveType.VeryInverse,
            CtRatio = ctRatio,
            PickupAmps = pickup.RecommendedPickupAmps,
            TimeDial = 0.45,
            InstantaneousAmps = 0,
        };
    }

    private static ProtectiveRelayService.RelaySettings ResolveFieldRelaySettings(DistributionNode node, double loadAmps)
    {
        ArgumentNullException.ThrowIfNull(node);

        var fallback = CreateBaselineRelaySettings(node, loadAmps);
        return MergeStoredRelaySettings(node.Id, fallback, node.Component.ProtectionSettings.FieldRelay);
    }

    private static ProtectiveRelayService.RelaySettings CreateStudyRelaySettings(
        DistributionNode node,
        double loadAmps,
        int depth,
        double ctRatio)
    {
        var pickup = ProtectiveRelayService.RecommendPickup(loadAmps, ctRatio, ProtectiveRelayService.RelayFunction.Function51);
        double timeDial = Math.Max(0.35, 0.75 - (depth * 0.1));
        double pickupMultiplier = depth <= 1 ? 1.1 : 1.0;

        return new ProtectiveRelayService.RelaySettings
        {
            Id = node.Id,
            Function = ProtectiveRelayService.RelayFunction.Function51,
            Curve = ProtectiveRelayService.CurveType.VeryInverse,
            CtRatio = ctRatio,
            PickupAmps = Math.Round(pickup.RecommendedPickupAmps * pickupMultiplier, 2),
            TimeDial = Math.Round(timeDial, 2),
            InstantaneousAmps = 0,
        };
    }

    private static ProtectiveRelayService.RelaySettings ResolveStudyRelaySettings(
        DistributionNode node,
        double loadAmps,
        int depth,
        double ctRatio)
    {
        ArgumentNullException.ThrowIfNull(node);

        var fallback = CreateStudyRelaySettings(node, loadAmps, depth, ctRatio);
        return MergeStoredRelaySettings(node.Id, fallback, node.Component.ProtectionSettings.StudyRelay);
    }

    private static ProtectiveRelayService.RelaySettings MergeStoredRelaySettings(
        string relayId,
        ProtectiveRelayService.RelaySettings fallback,
        StoredProtectiveRelaySettings? storedSettings)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        if (storedSettings is null || !storedSettings.HasValues)
            return fallback with { Id = relayId };

        return new ProtectiveRelayService.RelaySettings
        {
            Id = relayId,
            Function = ResolveStoredValue(storedSettings.Function, fallback.Function),
            Curve = ResolveStoredValue(storedSettings.Curve, fallback.Curve),
            CtRatio = ResolvePositiveStoredValue(storedSettings.CtRatio, fallback.CtRatio),
            PickupAmps = ResolvePositiveStoredValue(storedSettings.PickupAmps, fallback.PickupAmps),
            TimeDial = ResolveStoredValue(storedSettings.TimeDial, fallback.TimeDial),
            InstantaneousAmps = ResolveStoredValue(storedSettings.InstantaneousAmps, fallback.InstantaneousAmps),
        };
    }

    private static T ResolveStoredValue<T>(T? configuredValue, T fallbackValue)
        where T : struct
    {
        return configuredValue ?? fallbackValue;
    }

    private static double ResolvePositiveStoredValue(double? configuredValue, double fallbackValue)
    {
        return configuredValue.GetValueOrDefault() > 0
            ? configuredValue.Value
            : fallbackValue;
    }

    private static double ResolveCtRatio(double loadAmps)
    {
        double ctRatio = ProtectiveRelayService.SelectCtRatio(Math.Max(loadAmps, 1.0));
        return ctRatio > 0 ? ctRatio : 5000;
    }

    private static double ResolveRatedAmps(ElectricalComponent component)
    {
        return component switch
        {
            PanelComponent panel => Math.Max(panel.Amperage, panel.BusAmpacity),
            BusComponent bus => bus.BusAmps,
            TransferSwitchComponent transferSwitch => transferSwitch.AmpsRating,
            TransformerComponent transformer when transformer.SecondaryVoltage > 0
                => transformer.KVA * 1000.0 / (transformer.SecondaryVoltage * Math.Sqrt(3)),
            _ => 100.0,
        };
    }

    private static bool IsThreePhase(ElectricalComponent component)
    {
        return component is not null && component switch
        {
            TransformerComponent => true,
            PanelComponent => true,
            BusComponent => true,
            TransferSwitchComponent => true,
            _ => false,
        };
    }
}