using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC Compliance Report Aggregator.
/// Runs all available NEC validation checks across circuit design, conduit fill,
/// voltage drop, grounding, panel loading, and distribution coordination to produce
/// a single unified compliance report.
///
/// Code sections covered:
///   NEC 110.9  — Available fault current / AIC rating
///   NEC 210    — Branch circuit requirements
///   NEC 215    — Feeder requirements
///   NEC 220    — Load calculations / demand factors
///   NEC 240    — Overcurrent protection
///   NEC 250    — Grounding and bonding
///   NEC 310    — Conductor ampacity
///   NEC 344-362 — Conduit fill
///   NEC 430    — Motor circuits
///   NEC 450    — Transformer protection
///   NEC 700/701 — Emergency / legally required standby
/// </summary>
public static class NecComplianceReportService
{
    /// <summary>
    /// A section of the compliance report organized by NEC article.
    /// </summary>
    public record ComplianceSection
    {
        public string ArticleNumber { get; init; } = "";
        public string ArticleTitle { get; init; } = "";
        public ComplianceStatus Status { get; init; }
        public int ErrorCount { get; init; }
        public int WarningCount { get; init; }
        public int InfoCount { get; init; }
        public List<NecViolation> Violations { get; init; } = new();
    }

    /// <summary>
    /// Full compliance report.
    /// </summary>
    public record ComplianceReport
    {
        public DateTime GeneratedUtc { get; init; }
        public string ProjectName { get; init; } = "";
        public ComplianceStatus OverallStatus { get; init; }
        public int TotalErrors { get; init; }
        public int TotalWarnings { get; init; }
        public int TotalInfos { get; init; }
        public int TotalChecksRun { get; init; }
        public int SectionsChecked { get; init; }
        public List<ComplianceSection> Sections { get; init; } = new();
        public List<string> SummaryNotes { get; init; } = new();
    }

    public enum ComplianceStatus
    {
        Pass,
        PassWithWarnings,
        Fail,
    }

    /// <summary>
    /// Conduit fill input for compliance checking (decoupled from conduit model).
    /// </summary>
    public record ConduitFillInput
    {
        public string RunId { get; init; } = "";
        public string TradeSize { get; init; } = "1";
        public ConduitMaterialType Material { get; init; } = ConduitMaterialType.EMT;
        public List<string> WireSizes { get; init; } = new();
    }

    /// <summary>
    /// Input parameters for generating a compliance report.
    /// </summary>
    public record ComplianceInput
    {
        public string ProjectName { get; init; } = "Untitled Project";
        public List<Circuit> Circuits { get; init; } = new();
        public List<PanelSchedule> PanelSchedules { get; init; } = new();
        public List<DistributionNode> DistributionRoots { get; init; } = new();
        public List<ConduitFillInput> ConduitFillInputs { get; init; } = new();
        public List<FeederSegment> FeederSegments { get; init; } = new();
    }

    /// <summary>
    /// Generates a comprehensive NEC compliance report.
    /// </summary>
    public static ComplianceReport GenerateReport(ComplianceInput input)
    {
        var allViolations = new List<NecViolation>();
        var checksRun = 0;

        // 1. Circuit & panel validation via NecDesignRuleService
        var designRuleService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        if (input.Circuits.Count > 0 || input.PanelSchedules.Count > 0)
        {
            List<NecViolation> designViolations;
            if (input.DistributionRoots.Count > 0)
            {
                designViolations = designRuleService.ValidateAll(
                    input.Circuits, input.PanelSchedules, calcService, input.DistributionRoots);
            }
            else
            {
                designViolations = designRuleService.ValidateAll(
                    input.Circuits, input.PanelSchedules, calcService);
            }
            allViolations.AddRange(designViolations);
            checksRun += input.Circuits.Count + input.PanelSchedules.Count;
        }

        // 2. Conduit fill validation
        var fillService = new ConduitFillService();
        foreach (var conduit in input.ConduitFillInputs)
        {
            var fillResult = fillService.CalculateFill(conduit.TradeSize, conduit.Material, conduit.WireSizes);
            if (fillResult.ExceedsCode)
            {
                allViolations.Add(new NecViolation
                {
                    RuleId = "NEC 344.22",
                    Description = $"Conduit fill {fillResult.FillPercent:F0}% exceeds NEC limit of {fillResult.MaxAllowedFillPercent}%",
                    Severity = ViolationSeverity.Error,
                    AffectedItemId = conduit.RunId,
                    AffectedItemName = $"Conduit Run {conduit.RunId}",
                    Suggestion = "Increase conduit trade size or reduce conductors",
                });
            }
            checksRun++;
        }

        // 3. Feeder voltage drop validation
        foreach (var segment in input.FeederSegments)
        {
            double dropPercent = FeederVoltageDropService.CalculateSegmentDropPercent(segment);
            if (dropPercent > FeederVoltageDropService.MaxSegmentDropPercent)
            {
                allViolations.Add(new NecViolation
                {
                    RuleId = "NEC 215.2(A)(4)",
                    Description = $"Feeder voltage drop {dropPercent:F1}% exceeds recommended {FeederVoltageDropService.MaxSegmentDropPercent}%",
                    Severity = ViolationSeverity.Warning,
                    AffectedItemId = segment.ToNodeId,
                    AffectedItemName = $"Feeder {segment.FromNodeId} → {segment.ToNodeId}",
                    Suggestion = "Increase conductor size to reduce voltage drop",
                });
            }
            checksRun++;
        }

        // 4. Grounding conductor validation
        foreach (var circuit in input.Circuits)
        {
            if (circuit.SlotType != CircuitSlotType.Circuit) continue;
            int ocpdAmps = circuit.Breaker.TripAmps;
            if (ocpdAmps <= 0) continue;

            string minEGC = GroundingService.GetMinEGCSize(ocpdAmps, circuit.Wire.Material);
            // Compare sizes if ground size is specified
            if (!string.IsNullOrEmpty(circuit.Wire.GroundSize))
            {
                int minIdx = GetWireSizeIndex(minEGC);
                int actualIdx = GetWireSizeIndex(circuit.Wire.GroundSize);
                if (actualIdx >= 0 && minIdx >= 0 && actualIdx < minIdx)
                {
                    allViolations.Add(new NecViolation
                    {
                        RuleId = "NEC 250.122",
                        Description = $"EGC size {circuit.Wire.GroundSize} is undersized; minimum is {minEGC} for {ocpdAmps}A OCPD",
                        Severity = ViolationSeverity.Error,
                        AffectedItemId = circuit.Id,
                        AffectedItemName = $"Circuit {circuit.CircuitNumber} '{circuit.Description}'",
                        Suggestion = $"Increase EGC to {minEGC} or larger",
                    });
                }
            }
            checksRun++;
        }

        // Organize by NEC article
        var sections = OrganizeBySections(allViolations);
        int totalErrors = allViolations.Count(v => v.Severity == ViolationSeverity.Error);
        int totalWarnings = allViolations.Count(v => v.Severity == ViolationSeverity.Warning);
        int totalInfos = allViolations.Count(v => v.Severity == ViolationSeverity.Info);

        var overallStatus = totalErrors > 0 ? ComplianceStatus.Fail
            : totalWarnings > 0 ? ComplianceStatus.PassWithWarnings
            : ComplianceStatus.Pass;

        var notes = new List<string>();
        if (checksRun == 0)
            notes.Add("No items were provided for compliance checking");
        if (totalErrors > 0)
            notes.Add($"{totalErrors} code violation(s) must be corrected before approval");
        if (totalWarnings > 0)
            notes.Add($"{totalWarnings} advisory warning(s) should be reviewed");
        if (totalErrors == 0 && totalWarnings == 0 && checksRun > 0)
            notes.Add("All checked items comply with NEC 2023 requirements");

        return new ComplianceReport
        {
            GeneratedUtc = DateTime.UtcNow,
            ProjectName = input.ProjectName,
            OverallStatus = overallStatus,
            TotalErrors = totalErrors,
            TotalWarnings = totalWarnings,
            TotalInfos = totalInfos,
            TotalChecksRun = checksRun,
            SectionsChecked = sections.Count,
            Sections = sections,
            SummaryNotes = notes,
        };
    }

    private static readonly string[] WireSizeOrder =
    {
        "14", "12", "10", "8", "6", "4", "3", "2", "1",
        "1/0", "2/0", "3/0", "4/0",
        "250", "300", "350", "400", "500", "600", "700", "750", "1000",
    };

    private static int GetWireSizeIndex(string size) => Array.IndexOf(WireSizeOrder, size);

    private static readonly Dictionary<string, string> ArticleTitles = new()
    {
        ["NEC 110"] = "Requirements for Electrical Installations",
        ["NEC 210"] = "Branch Circuits",
        ["NEC 215"] = "Feeders",
        ["NEC 220"] = "Branch-Circuit, Feeder, and Service Load Calculations",
        ["NEC 240"] = "Overcurrent Protection",
        ["NEC 250"] = "Grounding and Bonding",
        ["NEC 310"] = "Conductors for General Wiring",
        ["NEC 344"] = "Rigid Metal Conduit (RMC)",
        ["NEC 348"] = "Flexible Metal Conduit (FMC)",
        ["NEC 430"] = "Motors, Motor Circuits, and Controllers",
        ["NEC 450"] = "Transformers and Transformer Vaults",
        ["NEC 700"] = "Emergency Systems",
        ["NEC 701"] = "Legally Required Standby Systems",
    };

    private static List<ComplianceSection> OrganizeBySections(List<NecViolation> violations)
    {
        var grouped = violations
            .GroupBy(v => ExtractArticle(v.RuleId))
            .OrderBy(g => g.Key)
            .ToList();

        var sections = new List<ComplianceSection>();
        foreach (var group in grouped)
        {
            int errors = group.Count(v => v.Severity == ViolationSeverity.Error);
            int warnings = group.Count(v => v.Severity == ViolationSeverity.Warning);
            int infos = group.Count(v => v.Severity == ViolationSeverity.Info);

            var status = errors > 0 ? ComplianceStatus.Fail
                : warnings > 0 ? ComplianceStatus.PassWithWarnings
                : ComplianceStatus.Pass;

            sections.Add(new ComplianceSection
            {
                ArticleNumber = group.Key,
                ArticleTitle = ArticleTitles.GetValueOrDefault(group.Key, ""),
                Status = status,
                ErrorCount = errors,
                WarningCount = warnings,
                InfoCount = infos,
                Violations = group.ToList(),
            });
        }
        return sections;
    }

    private static string ExtractArticle(string ruleId)
    {
        // Extract "NEC XXX" from rules like "NEC 240.4(D)" or "NEC 344.22 / 348-362"
        if (string.IsNullOrEmpty(ruleId)) return "NEC";

        var parts = ruleId.Split(' ', '.', '(', '/');
        if (parts.Length >= 2 && parts[0] == "NEC")
            return $"NEC {parts[1]}";
        return ruleId.Length > 7 ? ruleId[..7] : ruleId;
    }
}
