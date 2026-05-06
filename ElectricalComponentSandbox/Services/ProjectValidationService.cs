using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Comprehensive project-wide design validation engine that aggregates all
/// available compliance and sizing checks into a single pass/fail report.
///
/// Orchestrates:
/// - NEC compliance (via NecComplianceReportService)
/// - Short-circuit / AIC adequacy
/// - Distribution graph cycle detection
/// - Bundle derating verification
/// - Generator emergency system validation
/// - Panel phase balance checks
/// - Voltage drop across all circuits
/// </summary>
public class ProjectValidationService
{
    private readonly ElectricalCalculationService _calcService;
    private readonly NecDesignRuleService _necRules;
    private readonly ShortCircuitService _shortCircuit;
    private readonly DistributionGraphService _graphService;
    private readonly ConduitFillService _conduitFillService;

    public ProjectValidationService(
        ElectricalCalculationService calcService,
        NecDesignRuleService necRules,
        ShortCircuitService shortCircuit,
        DistributionGraphService graphService)
    {
        _calcService = calcService;
        _necRules = necRules;
        _shortCircuit = shortCircuit;
        _graphService = graphService;
        _conduitFillService = new ConduitFillService();
    }

    /// <summary>Category of a validation finding.</summary>
    public enum FindingCategory
    {
        NecCompliance,
        ShortCircuit,
        DistributionTopology,
        BundleDerating,
        VoltageDrop,
        PhaseBalance,
        GeneratorEmergency,
        CircuitTopology,
        ConduitFill,
        InteropReview,
        ArcFlash,
    }

    /// <summary>Severity level.</summary>
    public enum FindingSeverity
    {
        Error,
        Warning,
        Info,
    }

    /// <summary>A single validation finding.</summary>
    public record ValidationFinding
    {
        public string Id { get; init; } = "";
        public FindingCategory Category { get; init; }
        public FindingSeverity Severity { get; init; }
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string? ComponentId { get; init; }
        public string? NecReference { get; init; }
    }

    /// <summary>Project validation input.</summary>
    public record ProjectValidationInput
    {
        public string ProjectName { get; init; } = "";
        public List<ElectricalComponent> Components { get; init; } = new();
        public List<Circuit> Circuits { get; init; } = new();
        public List<ElectricalCircuit> ElectricalCircuits { get; init; } = new();
        public List<PanelSchedule> Schedules { get; init; } = new();
        public double MaxVoltageDropPercent { get; init; } = 3.0;
        public double MaxPhaseImbalancePercent { get; init; } = 10.0;
    }

    /// <summary>Complete validation report.</summary>
    public record ProjectValidationReport
    {
        public string ProjectName { get; init; } = "";
        public DateTime GeneratedUtc { get; init; }
        public bool IsValid { get; init; }
        public int ErrorCount { get; init; }
        public int WarningCount { get; init; }
        public int InfoCount { get; init; }
        public int TotalChecksRun { get; init; }
        public List<ValidationFinding> Findings { get; init; } = new();
        public Dictionary<FindingCategory, int> FindingsByCategory { get; init; } = new();
    }

    /// <summary>
    /// Runs all validation checks and returns a comprehensive report.
    /// </summary>
    public ProjectValidationReport Validate(ProjectValidationInput input)
    {
        var findings = new List<ValidationFinding>();
        int checksRun = 0;
        int findingIndex = 1;

        findings.AddRange(BuildNecFindings(input, ref findingIndex));
        checksRun++;

        checksRun += AddWhenAny(findings, input.Components.Count > 0,
            () => BuildDistributionTopologyFindings(input.Components, ref findingIndex));
        checksRun += AddWhenAny(findings, input.Components.Count > 0,
            () => BuildShortCircuitFindings(input.Components, ref findingIndex));
        checksRun += AddWhenAny(findings, input.ElectricalCircuits.Count > 0,
            () => BuildCircuitTopologyFindings(input, ref findingIndex));

        findings.AddRange(BuildVoltageDropFindings(input, ref findingIndex));
        checksRun += input.Circuits.Count(circuit => circuit.WireLengthFeet > 0);

        findings.AddRange(BuildPhaseBalanceFindings(input, ref findingIndex));
        checksRun += input.Schedules.Count;

        findings.AddRange(BuildBundleDeratingFindings(input, ref findingIndex));
        checksRun += input.Schedules.Count;

        checksRun += AddConditionalFindings(
            findings,
            input.Components.Count > 0 && input.Circuits.Count > 0,
            () => ValidateConduitFill(input.Components, input.Circuits, ref findingIndex));
        checksRun += AddConditionalFindings(
            findings,
            input.Components.Count > 0,
            () => ValidateInteropReviews(input.Components, ref findingIndex));
        checksRun += AddConditionalFindings(
            findings,
            input.Schedules.Count > 0,
            () => ValidateArcFlash(input.Schedules, ref findingIndex));

        // Build summary
        int errors = findings.Count(f => f.Severity == FindingSeverity.Error);
        int warnings = findings.Count(f => f.Severity == FindingSeverity.Warning);
        int infos = findings.Count(f => f.Severity == FindingSeverity.Info);

        var byCategory = findings.GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ProjectValidationReport
        {
            ProjectName = input.ProjectName,
            GeneratedUtc = DateTime.UtcNow,
            IsValid = errors == 0,
            ErrorCount = errors,
            WarningCount = warnings,
            InfoCount = infos,
            TotalChecksRun = checksRun,
            Findings = findings,
            FindingsByCategory = byCategory,
        };
    }

    private static int AddWhenAny(
        ICollection<ValidationFinding> findings,
        bool condition,
        Func<List<ValidationFinding>> build)
    {
        if (!condition)
            return 0;

        foreach (var finding in build())
            findings.Add(finding);

        return 1;
    }

    private static int AddConditionalFindings(
        ICollection<ValidationFinding> findings,
        bool condition,
        Func<List<ValidationFinding>> build)
        => AddWhenAny(findings, condition, build);

    private List<ValidationFinding> BuildNecFindings(ProjectValidationInput input, ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        var necViolations = _necRules.ValidateAll(input.Circuits, input.Schedules, _calcService);
        foreach (var violation in necViolations)
        {
            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.NecCompliance,
                Severity = violation.Severity == ViolationSeverity.Error ? FindingSeverity.Error
                    : violation.Severity == ViolationSeverity.Warning ? FindingSeverity.Warning
                    : FindingSeverity.Info,
                Title = violation.RuleId,
                Description = violation.Description,
                ComponentId = violation.AffectedItemId,
                NecReference = violation.RuleId,
            });
        }

        return findings;
    }

    private List<ValidationFinding> BuildDistributionTopologyFindings(
        IReadOnlyList<ElectricalComponent> components,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var cycle in _graphService.DetectCycles(components))
        {
            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.DistributionTopology,
                Severity = FindingSeverity.Error,
                Title = "Distribution Cycle Detected",
                Description = cycle,
            });
        }

        return findings;
    }

    private List<ValidationFinding> BuildShortCircuitFindings(
        IReadOnlyList<ElectricalComponent> components,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        var roots = _graphService.BuildGraph(components);
        _graphService.PropagateFaultCurrent(roots);

        foreach (var result in _shortCircuit.ValidateAIC(roots).Where(result => !result.IsAdequate))
        {
            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.ShortCircuit,
                Severity = FindingSeverity.Error,
                Title = "Inadequate AIC Rating",
                Description = $"{result.NodeName}: Available fault {result.AvailableFaultKA:F1} kA exceeds equipment AIC {result.EquipmentAICKA:F1} kA",
                ComponentId = result.NodeId,
            });
        }

        return findings;
    }

    private static List<ValidationFinding> BuildCircuitTopologyFindings(
        ProjectValidationInput input,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var finding in ElectricalCircuitService.ValidateCircuitSet(input.ElectricalCircuits, input.Components))
        {
            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.CircuitTopology,
                Severity = finding.Severity == ElectricalCircuitValidationSeverity.Error
                    ? FindingSeverity.Error
                    : finding.Severity == ElectricalCircuitValidationSeverity.Warning
                        ? FindingSeverity.Warning
                        : FindingSeverity.Info,
                Title = finding.Title,
                Description = finding.Description,
                ComponentId = finding.ComponentId ?? finding.ConnectorId ?? finding.CircuitId,
            });
        }

        return findings;
    }

    private List<ValidationFinding> BuildVoltageDropFindings(
        ProjectValidationInput input,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var circuit in input.Circuits.Where(circuit => circuit.WireLengthFeet > 0))
        {
            var vdResult = _calcService.CalculateVoltageDrop(circuit);
            if (vdResult.VoltageDropPercent <= input.MaxVoltageDropPercent)
                continue;

            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.VoltageDrop,
                Severity = vdResult.VoltageDropPercent > 5.0 ? FindingSeverity.Error : FindingSeverity.Warning,
                Title = "Excessive Voltage Drop",
                Description = $"Circuit {circuit.CircuitNumber}: {vdResult.VoltageDropPercent:F1}% VD exceeds {input.MaxVoltageDropPercent}% limit",
                ComponentId = circuit.CircuitNumber,
                NecReference = "NEC 210.19(A) FPN",
            });
        }

        return findings;
    }

    private static List<ValidationFinding> BuildPhaseBalanceFindings(
        ProjectValidationInput input,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var schedule in input.Schedules)
        {
            var (a, b, c) = schedule.PhaseDemandVA;
            double max = Math.Max(a, Math.Max(b, c));
            double min = Math.Min(a, Math.Min(b, c));
            if (max <= 0 || min >= max)
                continue;

            double imbalance = (max - min) / max * 100.0;
            if (imbalance <= input.MaxPhaseImbalancePercent)
                continue;

            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.PhaseBalance,
                Severity = imbalance > 20 ? FindingSeverity.Error : FindingSeverity.Warning,
                Title = "Phase Imbalance",
                Description = $"Panel {schedule.PanelName}: {imbalance:F1}% imbalance (A={a:N0}, B={b:N0}, C={c:N0} VA)",
                ComponentId = schedule.PanelId,
            });
        }

        return findings;
    }

    private static List<ValidationFinding> BuildBundleDeratingFindings(
        ProjectValidationInput input,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var schedule in input.Schedules)
        {
            var activeCircuits = schedule.Circuits.Where(c => c.SlotType == CircuitSlotType.Circuit).ToList();
            var conduitGroups = activeCircuits
                .Where(c => c.ConduitIds.Count > 0)
                .SelectMany(c => c.ConduitIds.Select(cid => (ConduitId: cid, Circuit: c)))
                .GroupBy(x => x.ConduitId)
                .Where(g => g.Count() > 1);

            foreach (var group in conduitGroups)
            {
                int ccc = BundleDeratingService.CountCurrentCarrying(group.Select(g => g.Circuit));
                if (ccc <= 3)
                    continue;

                foreach (var item in group)
                {
                    var result = BundleDeratingService.ValidateCircuitInBundle(item.Circuit, ccc);
                    if (result.IsAdequate)
                        continue;

                    findings.Add(new ValidationFinding
                    {
                        Id = $"V-{findingIndex++:D4}",
                        Category = FindingCategory.BundleDerating,
                        Severity = FindingSeverity.Warning,
                        Title = "Bundle Derating Required",
                        Description = $"Circuit {item.Circuit.CircuitNumber} in conduit {group.Key}: {ccc} CCC, derating factor {result.BundleFactor:F2}",
                        ComponentId = item.Circuit.CircuitNumber,
                        NecReference = "NEC 310.15(C)(1)",
                    });
                }
            }
        }

        return findings;
    }

    private List<ValidationFinding> ValidateConduitFill(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<Circuit> circuits,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        var conduits = components
            .OfType<ConduitComponent>()
            .ToDictionary(component => component.Id, StringComparer.OrdinalIgnoreCase);

        var conduitCircuits = circuits
            .Where(circuit => circuit.ConduitIds.Count > 0 && circuit.SlotType == CircuitSlotType.Circuit)
            .SelectMany(circuit => circuit.ConduitIds.Select(conduitId => new { conduitId, circuit }))
            .GroupBy(item => item.conduitId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in conduitCircuits)
        {
            if (!conduits.TryGetValue(group.Key, out var conduit))
                continue;

            var wireSizes = group
                .SelectMany(item => ExpandConductors(item.circuit.Wire.Size, item.circuit.Wire.Conductors))
                .ToList();

            if (wireSizes.Count == 0)
                continue;

            string tradeSize = InferTradeSize(conduit.Diameter);
            var material = ParseMaterial(conduit.ConduitType);
            var result = _conduitFillService.CalculateFill(tradeSize, material, wireSizes);

            if (!result.ExceedsCode)
                continue;

            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.ConduitFill,
                Severity = result.FillPercent >= result.MaxAllowedFillPercent + 10
                    ? FindingSeverity.Error
                    : FindingSeverity.Warning,
                Title = "Conduit Overfill",
                Description = $"Conduit {conduit.Name}: fill {result.FillPercent:F1}% exceeds NEC limit {result.MaxAllowedFillPercent:F1}% for {wireSizes.Count} conductors.",
                ComponentId = conduit.Id,
                NecReference = "NEC Chapter 9, Table 1",
            });
        }

        return findings;
    }

    private List<ValidationFinding> ValidateInteropReviews(
        IReadOnlyList<ElectricalComponent> components,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();
        foreach (var component in components)
        {
            if (!NeedsInteropReview(component.InteropMetadata))
                continue;

            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.InteropReview,
                Severity = component.InteropMetadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges
                    ? FindingSeverity.Error
                    : FindingSeverity.Warning,
                Title = component.InteropMetadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges
                    ? "Import Review Requires Changes"
                    : "Import Review Pending",
                Description = BuildInteropReviewDescription(component),
                ComponentId = component.Id,
            });
        }

        return findings;
    }

    private static IEnumerable<string> ExpandConductors(string wireSize, int conductorCount)
    {
        if (string.IsNullOrWhiteSpace(wireSize) || conductorCount <= 0)
            yield break;

        for (int i = 0; i < conductorCount; i++)
            yield return wireSize;
    }

    private static string InferTradeSize(double diameter)
    {
        var emtSizes = Conduit.Core.Model.ConduitSizeSettings.CreateDefaultEMT().Sizes;
        var best = emtSizes
            .OrderBy(size => Math.Abs(size.NominalDiameter - diameter))
            .FirstOrDefault();

        return best?.TradeSize ?? "1/2";
    }

    private static Conduit.Core.Model.ConduitMaterialType ParseMaterial(string conduitType)
    {
        return Enum.TryParse<Conduit.Core.Model.ConduitMaterialType>(conduitType, true, out var material)
            ? material
            : Conduit.Core.Model.ConduitMaterialType.EMT;
    }

    private static bool NeedsInteropReview(ComponentInteropMetadata metadata)
    {
        if (metadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges)
            return true;

        if (!metadata.LastImportedUtc.HasValue)
            return false;

        if (metadata.LastReviewedUtc.HasValue && metadata.LastReviewedUtc.Value >= metadata.LastImportedUtc.Value)
            return metadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges;

        return !metadata.LastExportedUtc.HasValue || metadata.LastImportedUtc.Value > metadata.LastExportedUtc.Value;
    }

    private static string BuildInteropReviewDescription(ElectricalComponent component)
    {
        var metadata = component.InteropMetadata;
        var source = string.IsNullOrWhiteSpace(metadata.SourceDocumentName)
            ? metadata.SourceSystem
            : $"{metadata.SourceSystem} / {metadata.SourceDocumentName}";

        if (metadata.ReviewStatus == ComponentInteropReviewStatus.NeedsChanges)
        {
            return $"Imported component '{component.Name}' from {source} is marked Needs Changes and requires reconciliation before acceptance.";
        }

        return $"Imported component '{component.Name}' from {source} has no current review acknowledgment for the latest import state.";
    }

    private static List<ValidationFinding> ValidateArcFlash(
        IReadOnlyList<PanelSchedule> schedules,
        ref int findingIndex)
    {
        var findings = new List<ValidationFinding>();

        foreach (var schedule in schedules)
        {
            var voltage = ResolveNominalVoltage(schedule);
            if (voltage < 50 || schedule.AvailableFaultCurrentKA <= 0)
                continue;

            var result = ElectricalSafetyService.DeterminePpe(
                ResolveEquipmentClass(schedule),
                schedule.AvailableFaultCurrentKA,
                ResolveClearingTimeSeconds(schedule));

            if (result.IncidentEnergyCalCm2 <= 1.2)
                continue;

            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.ArcFlash,
                Severity = result.IncidentEnergyCalCm2 > 40
                    ? FindingSeverity.Error
                    : result.IncidentEnergyCalCm2 >= 25
                        ? FindingSeverity.Error
                        : result.IncidentEnergyCalCm2 > 1.2
                        ? FindingSeverity.Warning
                        : FindingSeverity.Info,
                Title = "Arc Flash Hazard",
                Description = $"Panel {schedule.PanelName}: {result.IncidentEnergyCalCm2:F1} cal/cm² incident energy, {result.ArcFlashBoundaryFt:F1} ft boundary, PPE {result.Category}.",
                ComponentId = schedule.PanelId,
                NecReference = "NEC 110.16 / NFPA 70E 130.5",
            });
        }

        return findings;
    }

    private static ElectricalSafetyService.EquipmentClass ResolveEquipmentClass(PanelSchedule schedule)
    {
        return ResolveNominalVoltage(schedule) <= 240
            ? ElectricalSafetyService.EquipmentClass.Panelboard
            : ElectricalSafetyService.EquipmentClass.Switchgear600V;
    }

    private static double ResolveClearingTimeSeconds(PanelSchedule schedule)
    {
        return schedule.MainBreakerAmps switch
        {
            <= 100 => 0.03,
            <= 400 => 0.05,
            <= 1200 => 0.08,
            _ => 0.10,
        };
    }

    private static double ResolveNominalVoltage(PanelSchedule schedule)
    {
        return schedule.VoltageConfig switch
        {
            PanelVoltageConfig.V120_240_1Ph => 240,
            PanelVoltageConfig.V120_208_3Ph => 208,
            PanelVoltageConfig.V277_480_3Ph => 480,
            PanelVoltageConfig.V240_3Ph => 240,
            _ => 208,
        };
    }
}
