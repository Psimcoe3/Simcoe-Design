using System.Globalization;
using System.IO;
using System.Text;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Generates professional exportable reports for electrical calculations.
/// Supports plain-text (.txt) formatted reports and CSV (.csv) data exports
/// suitable for import into Excel or other spreadsheet applications.
/// </summary>
public class ElectricalReportService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Project name printed in report headers.</summary>
    public string ProjectName { get; set; } = "Electrical Project";

    /// <summary>Engineer or preparer name printed in report headers.</summary>
    public string PreparedBy { get; set; } = string.Empty;

    private const int ReportWidth = 90;
    private static readonly string Separator = new('─', ReportWidth);
    private static readonly string DoubleSeparator = new('═', ReportWidth);

    // ── Text Reports ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a complete voltage drop analysis report as formatted text.
    /// Includes per-circuit calculations, NEC compliance flags, and a summary table.
    /// </summary>
    public string GenerateVoltageDropReport(
        IReadOnlyList<Circuit> circuits,
        IReadOnlyList<PanelComponent> panels,
        ElectricalCalculationService calcService)
    {
        var sb = new StringBuilder();

        WriteReportHeader(sb, "VOLTAGE DROP ANALYSIS", "Per NEC 210.19(A) Informational Note No. 4");

        if (circuits.Count == 0)
        {
            sb.AppendLine("  No circuits defined.");
            WriteReportFooter(sb);
            return sb.ToString();
        }

        // Column header
        sb.AppendLine(Separator);
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-8} {1,-24} {2,-12} {3,7} {4,7} {5,8} {6,7} {7,6}",
            "Circuit", "Description", "Panel", "Volts", "Amps", "VD (V)", "VD (%)", "Status"));
        sb.AppendLine(Separator);

        int passCount = 0;
        int warnCount = 0;
        int failCount = 0;
        int invalidCount = 0;

        foreach (var circuit in circuits)
        {
            var result = calcService.CalculateVoltageDrop(circuit);
            var panelName = FindPanelName(panels, circuit.PanelId);

            if (!result.IsValid)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-8} {1,-24} {2,-12} {3,7} {4,7} {5,8} {6,7} {7,6}",
                    TruncateField(circuit.CircuitNumber, 8),
                    TruncateField(circuit.Description, 24),
                    TruncateField(panelName, 12),
                    FormatVoltage(circuit.Voltage),
                    "---",
                    "---",
                    "---",
                    "N/A"));
                invalidCount++;
                continue;
            }

            string status;
            if (result.ExceedsTotalRecommendation)
            {
                status = "FAIL";
                failCount++;
            }
            else if (result.ExceedsNecRecommendation)
            {
                status = "WARN";
                warnCount++;
            }
            else
            {
                status = "OK";
                passCount++;
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-8} {1,-24} {2,-12} {3,7} {4,7:F1} {5,8:F2} {6,6:F1}% {7,6}",
                TruncateField(circuit.CircuitNumber, 8),
                TruncateField(circuit.Description, 24),
                TruncateField(panelName, 12),
                FormatVoltage(circuit.Voltage),
                result.CurrentAmps,
                result.VoltageDropVolts,
                result.VoltageDropPercent,
                status));
        }

        sb.AppendLine(Separator);

        // Detail section for each circuit
        sb.AppendLine();
        sb.AppendLine("  CIRCUIT DETAILS");
        sb.AppendLine(Separator);

        foreach (var circuit in circuits)
        {
            var result = calcService.CalculateVoltageDrop(circuit);
            var panelName = FindPanelName(panels, circuit.PanelId);

            sb.AppendLine();
            sb.AppendLine($"  Circuit {circuit.CircuitNumber}: {circuit.Description}");
            sb.AppendLine($"    Panel:        {panelName}");
            sb.AppendLine($"    Voltage:      {FormatVoltage(circuit.Voltage)}V  |  {circuit.Poles}P  |  Phase {circuit.Phase}");
            sb.AppendLine($"    Breaker:      {circuit.Breaker.TripAmps}A  {circuit.Breaker.Poles}P  {circuit.Breaker.BreakerType}");
            sb.AppendLine($"    Wire:         #{circuit.Wire.Size} {circuit.Wire.Material} {circuit.Wire.InsulationType}");
            sb.AppendLine($"    Length:       {circuit.WireLengthFeet:F0} ft");
            sb.AppendLine($"    Load:         {circuit.ConnectedLoadVA:N0} VA connected  |  {circuit.DemandLoadVA:N0} VA demand (DF={circuit.DemandFactor:F2})");

            if (result.IsValid)
            {
                sb.AppendLine($"    Current:      {result.CurrentAmps:F1} A");
                sb.AppendLine($"    Voltage Drop: {result.VoltageDropVolts:F2} V  ({result.VoltageDropPercent:F2}%)");
                sb.AppendLine($"    At Load:      {result.VoltageAtLoad:F1} V");

                if (result.ExceedsTotalRecommendation)
                    sb.AppendLine("    ** EXCEEDS NEC 5% TOTAL (FEEDER + BRANCH) RECOMMENDATION **");
                else if (result.ExceedsNecRecommendation)
                    sb.AppendLine("    ** EXCEEDS NEC 3% BRANCH CIRCUIT RECOMMENDATION **");
            }
            else
            {
                sb.AppendLine("    (Insufficient data for voltage drop calculation)");
            }
        }

        // Summary
        sb.AppendLine();
        sb.AppendLine(DoubleSeparator);
        sb.AppendLine("  SUMMARY");
        sb.AppendLine(Separator);
        sb.AppendLine($"    Total circuits analyzed:  {circuits.Count}");
        sb.AppendLine($"    Passing  (<=3%):          {passCount}");
        sb.AppendLine($"    Warning  (>3%, <=5%):     {warnCount}");
        sb.AppendLine($"    Failing  (>5%):           {failCount}");
        if (invalidCount > 0)
            sb.AppendLine($"    Insufficient data:        {invalidCount}");
        sb.AppendLine();
        sb.AppendLine("  NEC Reference: 210.19(A) Info Note No. 4 recommends max 3% branch, 5% total.");

        WriteReportFooter(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Generates a wire size recommendation report as formatted text.
    /// Compares current wire size against NEC Table 310.16 ampacity and voltage drop requirements.
    /// </summary>
    public string GenerateWireSizeReport(
        IReadOnlyList<Circuit> circuits,
        ElectricalCalculationService calcService)
    {
        var sb = new StringBuilder();

        WriteReportHeader(sb, "WIRE SIZE RECOMMENDATIONS", "Per NEC Table 310.16 (75C) + 3% Voltage Drop");

        if (circuits.Count == 0)
        {
            sb.AppendLine("  No circuits defined.");
            WriteReportFooter(sb);
            return sb.ToString();
        }

        // Summary table
        sb.AppendLine(Separator);
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-8} {1,-22} {2,6} {3,8} {4,10} {5,10} {6,10} {7,-10}",
            "Circuit", "Description", "Amps", "Current", "Ampacity", "VD Min", "Recomm.", "Governing"));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-8} {1,-22} {2,6} {3,8} {4,10} {5,10} {6,10} {7,-10}",
            "", "", "", "Wire", "Min", "Min", "Size", "Factor"));
        sb.AppendLine(Separator);

        int upsizeCount = 0;

        foreach (var circuit in circuits)
        {
            var rec = calcService.RecommendWireSize(circuit);
            string governing = rec.VoltageDropGoverning ? "VD" : "Ampacity";
            string vdMin = rec.MinSizeForVoltageDrop ?? "---";

            bool needsUpsize = !string.Equals(circuit.Wire.Size, rec.RecommendedSize, StringComparison.Ordinal)
                               && CompareWireSize(rec.RecommendedSize, circuit.Wire.Size) > 0;
            string flag = needsUpsize ? " *" : "";
            if (needsUpsize) upsizeCount++;

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-8} {1,-22} {2,6:F1} {3,8} {4,10} {5,10} {6,10} {7,-10}{8}",
                TruncateField(circuit.CircuitNumber, 8),
                TruncateField(circuit.Description, 22),
                rec.CurrentAmps,
                "#" + circuit.Wire.Size,
                "#" + rec.MinSizeForAmpacity,
                vdMin == "---" ? vdMin : "#" + vdMin,
                "#" + rec.RecommendedSize,
                governing,
                flag));
        }

        sb.AppendLine(Separator);

        // Detail section
        sb.AppendLine();
        sb.AppendLine("  CIRCUIT DETAILS");
        sb.AppendLine(Separator);

        foreach (var circuit in circuits)
        {
            var rec = calcService.RecommendWireSize(circuit);

            sb.AppendLine();
            sb.AppendLine($"  Circuit {circuit.CircuitNumber}: {circuit.Description}");
            sb.AppendLine($"    Material:         {circuit.Wire.Material}  |  Insulation: {circuit.Wire.InsulationType}");
            sb.AppendLine($"    Load Current:     {rec.CurrentAmps:F1} A");
            sb.AppendLine($"    Current Wire:     #{circuit.Wire.Size}");
            sb.AppendLine($"    Min for Ampacity: #{rec.MinSizeForAmpacity}");
            if (rec.MinSizeForVoltageDrop != null)
                sb.AppendLine($"    Min for VD (3%):  #{rec.MinSizeForVoltageDrop}");
            else
                sb.AppendLine($"    Min for VD (3%):  N/A (no wire length specified)");
            sb.AppendLine($"    Recommended:      #{rec.RecommendedSize}");
            sb.AppendLine($"    Governing Factor: {(rec.VoltageDropGoverning ? "Voltage Drop" : "Ampacity")}");

            bool needsUpsize = !string.Equals(circuit.Wire.Size, rec.RecommendedSize, StringComparison.Ordinal)
                               && CompareWireSize(rec.RecommendedSize, circuit.Wire.Size) > 0;
            if (needsUpsize)
                sb.AppendLine($"    ** WIRE UPSIZE REQUIRED: #{circuit.Wire.Size} -> #{rec.RecommendedSize} **");
        }

        // Summary
        sb.AppendLine();
        sb.AppendLine(DoubleSeparator);
        sb.AppendLine("  SUMMARY");
        sb.AppendLine(Separator);
        sb.AppendLine($"    Total circuits analyzed:  {circuits.Count}");
        sb.AppendLine($"    Requiring upsize:         {upsizeCount}");
        sb.AppendLine($"    Adequately sized:         {circuits.Count - upsizeCount}");
        sb.AppendLine();
        sb.AppendLine("  * = Wire upsize recommended.");
        sb.AppendLine("  NEC Reference: Table 310.16, 75C column; 3% max branch circuit voltage drop.");

        WriteReportFooter(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Generates a panel load summary report as formatted text.
    /// Includes load totals, phase balancing, and bus utilization for each panel schedule.
    /// </summary>
    public string GeneratePanelLoadReport(
        IReadOnlyList<PanelSchedule> schedules,
        ElectricalCalculationService calcService)
    {
        var sb = new StringBuilder();

        WriteReportHeader(sb, "PANEL LOAD SUMMARY", "NEC Article 220 Load Calculations");

        if (schedules.Count == 0)
        {
            sb.AppendLine("  No panel schedules defined.");
            WriteReportFooter(sb);
            return sb.ToString();
        }

        // Overview table
        sb.AppendLine(Separator);
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-16} {1,6} {2,10} {3,10} {4,8} {5,8} {6,10} {7,-8}",
            "Panel", "Bus A", "Conn. VA", "Demand VA", "Current", "Util %", "Imbal. VA", "Status"));
        sb.AppendLine(Separator);

        foreach (var schedule in schedules)
        {
            var summary = calcService.AnalyzePanelLoad(schedule);
            string status = summary.IsOverloaded ? "OVERLOAD" : "OK";

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-16} {1,6} {2,10:N0} {3,10:N0} {4,7:F1}A {5,7:F1}% {6,10:N0} {7,-8}",
                TruncateField(schedule.PanelName, 16),
                schedule.BusAmps,
                summary.TotalConnectedVA,
                summary.TotalDemandVA,
                summary.TotalCurrentAmps,
                summary.BusUtilizationPercent,
                summary.MaxPhaseImbalanceVA,
                status));
        }

        sb.AppendLine(Separator);

        // Detailed per-panel sections
        foreach (var schedule in schedules)
        {
            var summary = calcService.AnalyzePanelLoad(schedule);

            sb.AppendLine();
            sb.AppendLine(DoubleSeparator);
            sb.AppendLine($"  PANEL: {schedule.PanelName}");
            sb.AppendLine(Separator);
            sb.AppendLine($"    Configuration:    {FormatVoltageConfig(schedule.VoltageConfig)}");
            sb.AppendLine($"    Main:             {(schedule.IsMainLugsOnly ? "Main Lugs Only (MLO)" : $"{schedule.MainBreakerAmps}A Main Breaker")}");
            sb.AppendLine($"    Bus Rating:       {schedule.BusAmps}A");
            sb.AppendLine($"    Avail. Fault:     {schedule.AvailableFaultCurrentKA:F1} kA");
            sb.AppendLine();
            sb.AppendLine($"    Circuits:         {summary.CircuitCount}");
            sb.AppendLine($"    Available Spaces: {Math.Max(0, summary.AvailableSpaces)}");
            sb.AppendLine();
            sb.AppendLine("    LOAD TOTALS");
            sb.AppendLine($"    {"Connected Load:",-22} {summary.TotalConnectedVA,12:N0} VA");
            sb.AppendLine($"    {"Demand Load:",-22} {summary.TotalDemandVA,12:N0} VA");
            sb.AppendLine($"    {"Total Current:",-22} {summary.TotalCurrentAmps,12:F1} A");
            sb.AppendLine($"    {"Bus Utilization:",-22} {summary.BusUtilizationPercent,12:F1} %");
            sb.AppendLine();
            sb.AppendLine("    PHASE BALANCE");
            sb.AppendLine($"    {"Phase A:",-22} {summary.PhaseALoadVA,12:N0} VA");
            sb.AppendLine($"    {"Phase B:",-22} {summary.PhaseBLoadVA,12:N0} VA");
            sb.AppendLine($"    {"Phase C:",-22} {summary.PhaseCLoadVA,12:N0} VA");
            sb.AppendLine($"    {"Max Imbalance:",-22} {summary.MaxPhaseImbalanceVA,12:N0} VA");

            if (summary.IsOverloaded)
            {
                sb.AppendLine();
                sb.AppendLine("    ** PANEL IS OVERLOADED -- TOTAL CURRENT EXCEEDS BUS RATING **");
            }

            // Circuit listing for this panel
            if (schedule.Circuits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("    CIRCUIT LISTING");
                sb.AppendLine($"    {new string('-', 78)}");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    {0,-6} {1,-20} {2,4} {3,5} {4,6} {5,10} {6,10} {7,6} {8,6}",
                    "Ckt#", "Description", "Ph", "Volts", "Poles",
                    "Conn. VA", "Demand VA", "BkrA", "Wire"));
                sb.AppendLine($"    {new string('-', 78)}");

                foreach (var ckt in schedule.Circuits)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "    {0,-6} {1,-20} {2,4} {3,5} {4,6} {5,10:N0} {6,10:N0} {7,6} {8,6}",
                        TruncateField(ckt.CircuitNumber, 6),
                        TruncateField(ckt.Description, 20),
                        ckt.Phase,
                        FormatVoltage(ckt.Voltage),
                        ckt.Breaker.Poles,
                        ckt.ConnectedLoadVA,
                        ckt.DemandLoadVA,
                        ckt.Breaker.TripAmps,
                        "#" + ckt.Wire.Size));
                }
            }
        }

        // Grand totals
        sb.AppendLine();
        sb.AppendLine(DoubleSeparator);
        sb.AppendLine("  GRAND TOTALS");
        sb.AppendLine(Separator);

        double grandConnected = 0;
        double grandDemand = 0;
        int grandCircuits = 0;
        int overloadedPanels = 0;

        foreach (var schedule in schedules)
        {
            var summary = calcService.AnalyzePanelLoad(schedule);
            grandConnected += summary.TotalConnectedVA;
            grandDemand += summary.TotalDemandVA;
            grandCircuits += summary.CircuitCount;
            if (summary.IsOverloaded) overloadedPanels++;
        }

        sb.AppendLine($"    {"Panels:",-22} {schedules.Count,12}");
        sb.AppendLine($"    {"Total Circuits:",-22} {grandCircuits,12}");
        sb.AppendLine($"    {"Total Connected:",-22} {grandConnected,12:N0} VA");
        sb.AppendLine($"    {"Total Demand:",-22} {grandDemand,12:N0} VA");
        if (overloadedPanels > 0)
            sb.AppendLine($"    {"Overloaded Panels:",-22} {overloadedPanels,12}");

        WriteReportFooter(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Generates a formatted protection-program report from audit, mitigation, and upgrade findings.
    /// </summary>
    public string GenerateProtectionProgramReport(ProtectionProgramService.ProgramReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        WriteReportHeader(sb, "PROTECTION PROGRAM SUMMARY", "Protection study audit, mitigation, and upgrade planning");

        WriteProtectionOverview(sb, report);
        WriteProtectionActions(sb, report.Actions);
        WriteProtectionUpgrades(sb, report.RecommendedUpgrades);

        WriteReportFooter(sb);
        return sb.ToString();
    }

    // ── CSV Exports ───────────────────────────────────────────────────────────

    /// <summary>
    /// Exports voltage drop data as a CSV string with headers.
    /// Each row represents one circuit with its voltage drop calculation results.
    /// </summary>
    public string ExportVoltageDropCsv(
        IReadOnlyList<Circuit> circuits,
        IReadOnlyList<PanelComponent> panels,
        ElectricalCalculationService calcService)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(",",
            "Circuit Number",
            "Description",
            "Panel",
            "Phase",
            "Voltage (V)",
            "Poles",
            "Breaker (A)",
            "Wire Size",
            "Wire Material",
            "Wire Length (ft)",
            "Connected Load (VA)",
            "Demand Factor",
            "Demand Load (VA)",
            "Current (A)",
            "Voltage Drop (V)",
            "Voltage Drop (%)",
            "Voltage at Load (V)",
            "Exceeds 3% Branch",
            "Exceeds 5% Total",
            "Status"));

        foreach (var circuit in circuits)
        {
            var result = calcService.CalculateVoltageDrop(circuit);
            var panelName = FindPanelName(panels, circuit.PanelId);

            string status;
            if (!result.IsValid)
                status = "Insufficient Data";
            else if (result.ExceedsTotalRecommendation)
                status = "FAIL";
            else if (result.ExceedsNecRecommendation)
                status = "WARNING";
            else
                status = "PASS";

            sb.AppendLine(string.Join(",",
                CsvEscape(circuit.CircuitNumber),
                CsvEscape(circuit.Description),
                CsvEscape(panelName),
                CsvEscape(circuit.Phase),
                circuit.Voltage.ToString("F0", CultureInfo.InvariantCulture),
                circuit.Poles.ToString(CultureInfo.InvariantCulture),
                circuit.Breaker.TripAmps.ToString(CultureInfo.InvariantCulture),
                CsvEscape(circuit.Wire.Size),
                circuit.Wire.Material.ToString(),
                circuit.WireLengthFeet.ToString("F0", CultureInfo.InvariantCulture),
                circuit.ConnectedLoadVA.ToString("F0", CultureInfo.InvariantCulture),
                circuit.DemandFactor.ToString("F2", CultureInfo.InvariantCulture),
                circuit.DemandLoadVA.ToString("F0", CultureInfo.InvariantCulture),
                result.IsValid ? result.CurrentAmps.ToString("F2", CultureInfo.InvariantCulture) : "",
                result.IsValid ? result.VoltageDropVolts.ToString("F3", CultureInfo.InvariantCulture) : "",
                result.IsValid ? result.VoltageDropPercent.ToString("F2", CultureInfo.InvariantCulture) : "",
                result.IsValid ? result.VoltageAtLoad.ToString("F1", CultureInfo.InvariantCulture) : "",
                result.IsValid ? result.ExceedsNecRecommendation.ToString() : "",
                result.IsValid ? result.ExceedsTotalRecommendation.ToString() : "",
                status));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports panel load data as a CSV string with headers.
    /// Produces two logical sections: a panel summary row per panel,
    /// followed by individual circuit rows grouped by panel.
    /// </summary>
    public string ExportPanelLoadCsv(
        IReadOnlyList<PanelSchedule> schedules,
        ElectricalCalculationService calcService)
    {
        var sb = new StringBuilder();

        // Panel summary section
        sb.AppendLine(string.Join(",",
            "Panel Name",
            "Panel ID",
            "Voltage Configuration",
            "Main Type",
            "Main Breaker (A)",
            "Bus Rating (A)",
            "Circuit Count",
            "Available Spaces",
            "Total Connected (VA)",
            "Total Demand (VA)",
            "Total Current (A)",
            "Bus Utilization (%)",
            "Phase A (VA)",
            "Phase B (VA)",
            "Phase C (VA)",
            "Max Imbalance (VA)",
            "Overloaded"));

        foreach (var schedule in schedules)
        {
            var summary = calcService.AnalyzePanelLoad(schedule);

            sb.AppendLine(string.Join(",",
                CsvEscape(schedule.PanelName),
                CsvEscape(schedule.PanelId),
                CsvEscape(FormatVoltageConfig(schedule.VoltageConfig)),
                schedule.IsMainLugsOnly ? "MLO" : "Main Breaker",
                schedule.IsMainLugsOnly ? "" : schedule.MainBreakerAmps.ToString(CultureInfo.InvariantCulture),
                schedule.BusAmps.ToString(CultureInfo.InvariantCulture),
                summary.CircuitCount.ToString(CultureInfo.InvariantCulture),
                Math.Max(0, summary.AvailableSpaces).ToString(CultureInfo.InvariantCulture),
                summary.TotalConnectedVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.TotalDemandVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.TotalCurrentAmps.ToString("F2", CultureInfo.InvariantCulture),
                summary.BusUtilizationPercent.ToString("F1", CultureInfo.InvariantCulture),
                summary.PhaseALoadVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.PhaseBLoadVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.PhaseCLoadVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.MaxPhaseImbalanceVA.ToString("F0", CultureInfo.InvariantCulture),
                summary.IsOverloaded.ToString()));
        }

        // Blank separator row and circuit detail section
        sb.AppendLine();
        sb.AppendLine(string.Join(",",
            "Panel Name",
            "Circuit Number",
            "Description",
            "Phase",
            "Voltage (V)",
            "Poles",
            "Breaker (A)",
            "Wire Size",
            "Wire Material",
            "Wire Length (ft)",
            "Connected Load (VA)",
            "Demand Factor",
            "Demand Load (VA)"));

        foreach (var schedule in schedules)
        {
            foreach (var ckt in schedule.Circuits)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(schedule.PanelName),
                    CsvEscape(ckt.CircuitNumber),
                    CsvEscape(ckt.Description),
                    CsvEscape(ckt.Phase),
                    ckt.Voltage.ToString("F0", CultureInfo.InvariantCulture),
                    ckt.Breaker.Poles.ToString(CultureInfo.InvariantCulture),
                    ckt.Breaker.TripAmps.ToString(CultureInfo.InvariantCulture),
                    CsvEscape(ckt.Wire.Size),
                    ckt.Wire.Material.ToString(),
                    ckt.WireLengthFeet.ToString("F0", CultureInfo.InvariantCulture),
                    ckt.ConnectedLoadVA.ToString("F0", CultureInfo.InvariantCulture),
                    ckt.DemandFactor.ToString("F2", CultureInfo.InvariantCulture),
                    ckt.DemandLoadVA.ToString("F0", CultureInfo.InvariantCulture)));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports protection-program summary, actions, and upgrade recommendations as CSV.
    /// </summary>
    public string ExportProtectionProgramCsv(ProtectionProgramService.ProgramReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Readiness Score,{report.ReadinessScore.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Relay Critical Findings,{report.RelayCriticalCount.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Coordination Violations,{report.CoordinationViolationCount.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Duty Violations,{report.DutyViolationCount.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Average Arc Flash Reduction (%),{report.AverageArcFlashReductionPercent.ToString("F2", CultureInfo.InvariantCulture)}");

        sb.AppendLine();
        sb.AppendLine("Action Priority,Category,Description");
        foreach (var action in OrderActions(report.Actions))
        {
            sb.AppendLine(string.Join(",",
                action.Priority,
                CsvEscape(action.Category),
                CsvEscape(action.Description)));
        }

        sb.AppendLine();
        sb.AppendLine("Upgrade Name,Type,Priority Score,Benefit-Cost Ratio,Reason");
        foreach (var upgrade in report.RecommendedUpgrades.OrderByDescending(item => item.PriorityScore))
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(upgrade.Name),
                upgrade.Type,
                upgrade.PriorityScore.ToString("F2", CultureInfo.InvariantCulture),
                upgrade.BenefitCostRatio.ToString("F4", CultureInfo.InvariantCulture),
                CsvEscape(upgrade.Reason)));
        }

        return sb.ToString();
    }

    // ── File Output ───────────────────────────────────────────────────────────

    /// <summary>
    /// Saves any report string to a file at the specified path.
    /// Creates the directory if it does not exist.
    /// </summary>
    public void SaveReport(string content, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private void WriteReportHeader(StringBuilder sb, string title, string subtitle)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        sb.AppendLine(DoubleSeparator);
        sb.AppendLine($"  {ProjectName}");
        sb.AppendLine();
        sb.AppendLine($"  {title}");
        if (!string.IsNullOrWhiteSpace(subtitle))
            sb.AppendLine($"  {subtitle}");
        sb.AppendLine();
        sb.AppendLine($"  Date:        {timestamp}");
        if (!string.IsNullOrWhiteSpace(PreparedBy))
            sb.AppendLine($"  Prepared by: {PreparedBy}");
        sb.AppendLine(DoubleSeparator);
        sb.AppendLine();
    }

    private static void WriteReportFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine(Separator);
        sb.AppendLine($"  Report generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"  Simcoe Design -- Electrical Report Service");
        sb.AppendLine(Separator);
    }

    private static void WriteProtectionOverview(StringBuilder sb, ProtectionProgramService.ProgramReport report)
    {
        sb.AppendLine("  OVERVIEW");
        sb.AppendLine(Separator);
        sb.AppendLine($"    {"Readiness Score:",-32} {report.ReadinessScore,8}");
        sb.AppendLine($"    {"Relay Critical Findings:",-32} {report.RelayCriticalCount,8}");
        sb.AppendLine($"    {"Coordination Violations:",-32} {report.CoordinationViolationCount,8}");
        sb.AppendLine($"    {"Duty Violations:",-32} {report.DutyViolationCount,8}");
        sb.AppendLine($"    {"Avg Arc Flash Reduction:",-32} {report.AverageArcFlashReductionPercent,7:F1} %");
    }

    private static void WriteProtectionActions(StringBuilder sb, IReadOnlyList<ProtectionProgramService.ProgramAction> actions)
    {
        sb.AppendLine();
        sb.AppendLine("  PRIORITY ACTIONS");
        sb.AppendLine(Separator);

        if (actions.Count == 0)
        {
            sb.AppendLine("    No protection-program actions supplied.");
            return;
        }

        foreach (var action in OrderActions(actions))
            sb.AppendLine($"    [{action.Priority}] {action.Category}: {action.Description}");
    }

    private static void WriteProtectionUpgrades(StringBuilder sb, IReadOnlyList<ProtectionUpgradePlannerService.UpgradeRecommendation> upgrades)
    {
        sb.AppendLine();
        sb.AppendLine("  RECOMMENDED UPGRADES");
        sb.AppendLine(Separator);

        if (upgrades.Count == 0)
        {
            sb.AppendLine("    No upgrade recommendations supplied.");
            return;
        }

        foreach (var upgrade in upgrades.OrderByDescending(item => item.PriorityScore))
        {
            sb.AppendLine($"    {upgrade.Name} ({upgrade.Type})");
            sb.AppendLine($"      Score: {upgrade.PriorityScore:F1}  |  Benefit/Cost: {upgrade.BenefitCostRatio:F4}");
            sb.AppendLine($"      Reason: {upgrade.Reason}");
        }
    }

    private static IEnumerable<ProtectionProgramService.ProgramAction> OrderActions(IReadOnlyList<ProtectionProgramService.ProgramAction> actions)
    {
        return actions.OrderByDescending(action => action.Priority)
            .ThenBy(action => action.Category, StringComparer.Ordinal);
    }

    private static string FindPanelName(IReadOnlyList<PanelComponent> panels, string panelId)
    {
        foreach (var p in panels)
        {
            if (string.Equals(p.Id, panelId, StringComparison.Ordinal))
                return p.Name;
        }
        return "(unassigned)";
    }

    private static string FormatVoltage(double voltage)
    {
        return voltage == (int)voltage
            ? ((int)voltage).ToString(CultureInfo.InvariantCulture)
            : voltage.ToString("F0", CultureInfo.InvariantCulture);
    }

    private static string FormatVoltageConfig(PanelVoltageConfig config)
    {
        return config switch
        {
            PanelVoltageConfig.V120_240_1Ph => "120/240V 1-Phase",
            PanelVoltageConfig.V120_208_3Ph => "120/208V 3-Phase",
            PanelVoltageConfig.V277_480_3Ph => "277/480V 3-Phase",
            PanelVoltageConfig.V240_3Ph     => "240V 3-Phase",
            _                               => config.ToString()
        };
    }

    private static string TruncateField(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength - 2) + "..";
    }

    /// <summary>
    /// Escapes a value for CSV output. Wraps in double quotes if the value
    /// contains commas, quotes, or newlines per RFC 4180.
    /// </summary>
    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool needsQuoting = value.Contains(',') || value.Contains('"') ||
                            value.Contains('\n') || value.Contains('\r');

        if (needsQuoting)
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    /// <summary>
    /// Compares two AWG/kcmil wire sizes.  Returns positive if <paramref name="a"/> is
    /// larger than <paramref name="b"/>, negative if smaller, and zero if equal.
    /// </summary>
    private static int CompareWireSize(string a, string b)
    {
        int idxA = GetWireSizeIndex(a);
        int idxB = GetWireSizeIndex(b);
        return idxA.CompareTo(idxB);
    }

    private static int GetWireSizeIndex(string size)
    {
        // Ordered smallest to largest.  Index = relative size.
        string[] ordered =
        {
            "14", "12", "10", "8", "6", "4", "3", "2", "1",
            "1/0", "2/0", "3/0", "4/0",
            "250", "300", "350", "400", "500"
        };

        for (int i = 0; i < ordered.Length; i++)
        {
            if (string.Equals(ordered[i], size, StringComparison.Ordinal))
                return i;
        }

        return -1; // Unknown size sorts before all known sizes
    }
}
