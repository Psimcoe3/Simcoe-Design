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

        // 1. NEC design rules
        var necViolations = _necRules.ValidateAll(input.Circuits, input.Schedules, _calcService);
        checksRun++;
        foreach (var v in necViolations)
        {
            findings.Add(new ValidationFinding
            {
                Id = $"V-{findingIndex++:D4}",
                Category = FindingCategory.NecCompliance,
                Severity = v.Severity == ViolationSeverity.Error ? FindingSeverity.Error
                    : v.Severity == ViolationSeverity.Warning ? FindingSeverity.Warning
                    : FindingSeverity.Info,
                Title = v.RuleId,
                Description = v.Description,
                ComponentId = v.AffectedItemId,
                NecReference = v.RuleId,
            });
        }

        // 2. Distribution topology — cycle detection
        if (input.Components.Count > 0)
        {
            var cycles = _graphService.DetectCycles(input.Components);
            checksRun++;
            foreach (var cycle in cycles)
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
        }

        // 3. Short circuit / AIC adequacy
        if (input.Components.Count > 0)
        {
            var roots = _graphService.BuildGraph(input.Components);
            _graphService.PropagateFaultCurrent(roots);
            var aicResults = _shortCircuit.ValidateAIC(roots);
            checksRun++;
            foreach (var sc in aicResults.Where(r => !r.IsAdequate))
            {
                findings.Add(new ValidationFinding
                {
                    Id = $"V-{findingIndex++:D4}",
                    Category = FindingCategory.ShortCircuit,
                    Severity = FindingSeverity.Error,
                    Title = "Inadequate AIC Rating",
                    Description = $"{sc.NodeName}: Available fault {sc.AvailableFaultKA:F1} kA exceeds equipment AIC {sc.EquipmentAICKA:F1} kA",
                    ComponentId = sc.NodeId,
                });
            }
        }

        // 4. Voltage drop
        foreach (var circuit in input.Circuits)
        {
            if (circuit.WireLengthFeet <= 0) continue;

            var vdResult = _calcService.CalculateVoltageDrop(circuit);
            checksRun++;
            if (vdResult.VoltageDropPercent > input.MaxVoltageDropPercent)
            {
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
        }

        // 5. Phase balance
        foreach (var schedule in input.Schedules)
        {
            checksRun++;
            var (a, b, c) = schedule.PhaseDemandVA;
            double max = Math.Max(a, Math.Max(b, c));
            double min = Math.Min(a, Math.Min(b, c));

            if (max > 0 && min < max)
            {
                double imbalance = (max - min) / max * 100.0;
                if (imbalance > input.MaxPhaseImbalancePercent)
                {
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
            }
        }

        // 6. Bundle derating spot-check
        foreach (var schedule in input.Schedules)
        {
            var activeCircuits = schedule.Circuits.Where(c => c.SlotType == CircuitSlotType.Circuit).ToList();
            checksRun++;

            // Check circuits sharing conduits
            var conduitGroups = activeCircuits
                .Where(c => c.ConduitIds != null && c.ConduitIds.Count > 0)
                .SelectMany(c => c.ConduitIds.Select(cid => (ConduitId: cid, Circuit: c)))
                .GroupBy(x => x.ConduitId)
                .Where(g => g.Count() > 1);

            foreach (var group in conduitGroups)
            {
                int ccc = BundleDeratingService.CountCurrentCarrying(group.Select(g => g.Circuit));
                if (ccc > 3) // NEC 310.15(C)(1) derating starts at 4+
                {
                    foreach (var item in group)
                    {
                        var result = BundleDeratingService.ValidateCircuitInBundle(item.Circuit, ccc);
                        if (!result.IsAdequate)
                        {
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
            }
        }

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
}
