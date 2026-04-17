using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    // ── Voltage Drop Calculator ──────────────────────────────────────────────

    private void VoltageDropCalc_Click(object sender, RoutedEventArgs e)
    {
        var panels = _viewModel.Components.OfType<PanelComponent>().ToList();
        if (!_viewModel.Circuits.Any())
        {
            MessageBox.Show(
                "No circuits defined. Add a circuit first via Electrical → Add Circuit.",
                "Voltage Drop Calculator", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("VOLTAGE DROP ANALYSIS");
        sb.AppendLine(new string('─', 60));

        foreach (var circuit in _viewModel.Circuits)
        {
            var result = _viewModel.ElectricalCalc.CalculateVoltageDrop(circuit);
            var panelName = panels.FirstOrDefault(p =>
                string.Equals(p.Id, circuit.PanelId, StringComparison.Ordinal))?.Name ?? "(unassigned)";

            sb.AppendLine($"\nCircuit {circuit.CircuitNumber}: {circuit.Description}");
            sb.AppendLine($"  Panel: {panelName}  |  {circuit.Voltage}V {circuit.Poles}P  |  Phase {circuit.Phase}");
            sb.AppendLine($"  Wire: #{circuit.Wire.Size} {circuit.Wire.Material} {circuit.Wire.InsulationType}");
            sb.AppendLine($"  Length: {circuit.WireLengthFeet:F0} ft  |  Load: {circuit.DemandLoadVA:F0} VA");

            if (result.IsValid)
            {
                sb.AppendLine($"  Current: {result.CurrentAmps:F1} A");
                sb.AppendLine($"  Voltage Drop: {result.VoltageDropVolts:F2} V ({result.VoltageDropPercent:F1}%)");
                sb.AppendLine($"  Voltage at Load: {result.VoltageAtLoad:F1} V");
                if (result.ExceedsNecRecommendation)
                    sb.AppendLine("  ⚠ EXCEEDS NEC 3% BRANCH CIRCUIT RECOMMENDATION");
                if (result.ExceedsTotalRecommendation)
                    sb.AppendLine("  ⚠ EXCEEDS NEC 5% TOTAL RECOMMENDATION");
            }
            else
            {
                sb.AppendLine("  (insufficient data for calculation)");
            }
        }

        MessageBox.Show(sb.ToString(), "Voltage Drop Analysis",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Wire Size Recommendation ─────────────────────────────────────────────

    private void WireSizeCalc_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Circuits.Any())
        {
            MessageBox.Show(
                "No circuits defined. Add a circuit first via Electrical → Add Circuit.",
                "Wire Size Calculator", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("WIRE SIZE RECOMMENDATIONS (NEC Table 310.16 + 3% VD)");
        sb.AppendLine(new string('─', 60));

        foreach (var circuit in _viewModel.Circuits)
        {
            var rec = _viewModel.ElectricalCalc.RecommendWireSize(circuit);

            sb.AppendLine($"\nCircuit {circuit.CircuitNumber}: {circuit.Description}");
            sb.AppendLine($"  Current: {rec.CurrentAmps:F1} A");
            sb.AppendLine($"  Current wire: #{circuit.Wire.Size}");
            sb.AppendLine($"  Recommended: #{rec.RecommendedSize}");
            sb.AppendLine($"  Min for ampacity: #{rec.MinSizeForAmpacity}");
            if (rec.MinSizeForVoltageDrop != null)
                sb.AppendLine($"  Min for voltage drop: #{rec.MinSizeForVoltageDrop}");
            sb.AppendLine($"  Governing factor: {(rec.VoltageDropGoverning ? "Voltage Drop" : "Ampacity")}");
        }

        MessageBox.Show(sb.ToString(), "Wire Size Recommendations",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Panel Load Summary ───────────────────────────────────────────────────

    private void PanelLoadSummary_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RebuildPanelSchedules();

        if (!_viewModel.PanelSchedules.Any())
        {
            MessageBox.Show(
                "No panels in the project. Add a panel component first.",
                "Panel Load Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("PANEL LOAD SUMMARY");
        sb.AppendLine(new string('═', 60));

        foreach (var schedule in _viewModel.PanelSchedules)
        {
            var summary = _viewModel.ElectricalCalc.AnalyzePanelLoad(schedule);

            sb.AppendLine($"\n{schedule.PanelName}");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine($"  Main: {(schedule.IsMainLugsOnly ? "MLO" : $"{schedule.MainBreakerAmps}A")}  |  Bus: {schedule.BusAmps}A");
            sb.AppendLine($"  Circuits: {summary.CircuitCount}  |  Available spaces: {Math.Max(0, summary.AvailableSpaces)}");
            sb.AppendLine($"  Connected Load: {summary.TotalConnectedVA:N0} VA");
            sb.AppendLine($"  Demand Load:    {summary.TotalDemandVA:N0} VA");
            sb.AppendLine($"  Total Current:  {summary.TotalCurrentAmps:F1} A");
            sb.AppendLine($"  Bus Utilization: {summary.BusUtilizationPercent:F1}%");
            sb.AppendLine($"  Phase A: {summary.PhaseALoadVA:N0} VA");
            sb.AppendLine($"  Phase B: {summary.PhaseBLoadVA:N0} VA");
            sb.AppendLine($"  Phase C: {summary.PhaseCLoadVA:N0} VA");
            sb.AppendLine($"  Max Imbalance: {summary.MaxPhaseImbalanceVA:N0} VA");

            if (summary.IsOverloaded)
                sb.AppendLine("  ⚠ PANEL IS OVERLOADED");
        }

        MessageBox.Show(sb.ToString(), "Panel Load Summary",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RebuildPanelSchedules_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RebuildPanelSchedules();
        ActionLogService.Instance.Log(LogCategory.Component, "Panel schedules rebuilt",
            $"Count: {_viewModel.PanelSchedules.Count}");
        MessageBox.Show($"Rebuilt {_viewModel.PanelSchedules.Count} panel schedule(s).",
            "Panel Schedules", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProtectionProgramSummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var protectionReport = BuildProtectionProgramReport();
            if (!ProjectProtectionProgramService.HasMeaningfulContent(protectionReport))
            {
                MessageBox.Show(
                    "No protection-study content is available yet. Add source and downstream components, or store relay settings on components first.",
                    "Protection Program Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var reportService = new ElectricalReportService();
            MessageBox.Show(
                reportService.GenerateProtectionProgramReport(protectionReport),
                "Protection Program Summary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Component, "Failed to build protection program summary", ex);
            MessageBox.Show($"Protection summary failed: {ex.Message}",
                "Protection Program Summary", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Add Circuit ──────────────────────────────────────────────────────────

    private void AddCircuit_Click(object sender, RoutedEventArgs e)
    {
        var panels = _viewModel.Components.OfType<PanelComponent>().ToList();
        if (!panels.Any())
        {
            MessageBox.Show("Add a panel component to the project first.",
                "Add Circuit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var panel = panels.First();
        int nextNum = _viewModel.Circuits.Count + 1;

        var circuit = new Circuit
        {
            CircuitNumber = nextNum.ToString(),
            Description = $"Circuit {nextNum}",
            PanelId = panel.Id,
            Phase = nextNum % 3 == 1 ? "A" : nextNum % 3 == 2 ? "B" : "C",
            Voltage = 120,
            ConnectedLoadVA = 1800,
            DemandFactor = 1.0,
            WireLengthFeet = 75,
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
        };

        _viewModel.UndoRedo.Execute(
            new AddItemAction<Circuit>(_viewModel.Circuits, circuit, $"Circuit {circuit.CircuitNumber}"));
        ActionLogService.Instance.Log(LogCategory.Component, "Circuit added",
            $"Number: {circuit.CircuitNumber}, Panel: {panel.Name}");

        MessageBox.Show(
            $"Circuit #{circuit.CircuitNumber} added to {panel.Name}.\n\n" +
            $"Phase: {circuit.Phase}  |  120V 1P  |  20A breaker\n" +
            $"Wire: #12 Cu THHN  |  75 ft\n" +
            $"Load: 1,800 VA\n\n" +
            "Edit circuit properties in the Electrical menu.",
            "Circuit Added", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Selection Tools ──────────────────────────────────────────────────────

    private void SelectByLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveLayer == null)
        {
            MessageBox.Show("No active layer selected.", "Select by Layer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var matches = _viewModel.SelectionFilter.SelectByLayer(
            _viewModel.Components, _viewModel.ActiveLayer.Id);
        ApplySelectionResult(matches, $"Selected {matches.Count} component(s) on layer '{_viewModel.ActiveLayer.Name}'");
    }

    private void SelectByType_Click(object sender, RoutedEventArgs e)
    {
        var matches = SelectByTypeForTesting();
        if (matches.Count == 0)
        {
            MessageBox.Show("Select a component first to filter by its type.",
                "Select by Type", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedTypes = GetSelectedComponents().Select(component => component.Type).Distinct().ToList();
        var typeSummary = selectedTypes.Count == 1
            ? selectedTypes[0].ToString()
            : string.Join(", ", selectedTypes);
        ApplySelectionResult(matches, $"Selected {matches.Count} component(s) matching type set: {typeSummary}");
    }

    private void SelectSimilar_Click(object sender, RoutedEventArgs e)
    {
        var matches = SelectSimilarForTesting();
        if (matches.Count == 0)
        {
            MessageBox.Show("Select a component first.",
                "Select Similar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplySelectionResult(matches, $"Selected {matches.Count} similar component(s)");
    }

    internal IReadOnlyList<ElectricalComponent> SelectByTypeForTesting()
    {
        var selected = GetSelectedComponents();
        if (selected.Count == 0)
            return Array.Empty<ElectricalComponent>();

        var selectedTypes = selected.Select(component => component.Type).Distinct().ToHashSet();
        return _viewModel.Components
            .Where(component => selectedTypes.Contains(component.Type))
            .ToList();
    }

    internal IReadOnlyList<ElectricalComponent> SelectSimilarForTesting()
    {
        var selected = GetSelectedComponents();
        if (selected.Count == 0)
            return Array.Empty<ElectricalComponent>();

        var signatures = selected
            .Select(component => (component.Type, component.LayerId))
            .Distinct()
            .ToHashSet();
        var selectedIds = selected.Select(component => component.Id).ToHashSet(StringComparer.Ordinal);

        return _viewModel.Components
            .Where(component => signatures.Contains((component.Type, component.LayerId)) || selectedIds.Contains(component.Id))
            .DistinctBy(component => component.Id)
            .ToList();
    }

    private void QuickSelect_Click(object sender, RoutedEventArgs e)
    {
        var criteria = new SelectionCriteria();

        var input = PromptInput("Quick Select", "Filter by name (leave blank to skip):", "");
        if (input != null && !string.IsNullOrWhiteSpace(input))
            criteria.NameContains = input;

        var matches = _viewModel.SelectionFilter.QuickSelect(_viewModel.Components, criteria);
        ApplySelectionResult(matches, $"Quick Select: {matches.Count} match(es)");
    }

    private void BulkEditProperties_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedComponents();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select components first (window/crossing selection or Select by Layer/Type).",
                "Bulk Edit", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var change = new BulkPropertyChange();
        bool anyChange = false;

        // Layer
        var layerInput = PromptInput("Bulk Edit – Layer",
            $"New layer ID for {selected.Count} component(s) (leave blank to skip):",
            _viewModel.ActiveLayer?.Id ?? "default");
        if (!string.IsNullOrWhiteSpace(layerInput))
        {
            change.LayerId = layerInput;
            anyChange = true;
        }

        // Elevation
        var elevInput = PromptInput("Bulk Edit – Elevation",
            "New elevation in feet (leave blank to skip):", "");
        if (!string.IsNullOrWhiteSpace(elevInput) && double.TryParse(elevInput, out double elev))
        {
            change.Elevation = elev;
            anyChange = true;
        }

        // Material
        var materialInput = PromptInput("Bulk Edit – Material",
            "New material (leave blank to skip):", "");
        if (!string.IsNullOrWhiteSpace(materialInput))
        {
            change.Material = materialInput;
            anyChange = true;
        }

        // Color
        var colorInput = PromptInput("Bulk Edit – Color",
            "New color hex (e.g. #FF0000, leave blank to skip):", "");
        if (!string.IsNullOrWhiteSpace(colorInput))
        {
            change.Color = colorInput;
            anyChange = true;
        }

        // Line weight
        var lineWeightInput = PromptInput("Bulk Edit – Line Weight",
            "New line weight in mm (leave blank to skip):", "");
        if (!string.IsNullOrWhiteSpace(lineWeightInput) && double.TryParse(lineWeightInput, out double lineWeight))
        {
            change.LineWeight = lineWeight;
            anyChange = true;
        }

        if (!anyChange) return;

        ApplyBulkPropertyChange(selected, change, showCompletionDialog: true);
    }

    internal bool ApplyBulkPropertyChangeForTesting(BulkPropertyChange change)
        => ApplyBulkPropertyChange(GetSelectedComponents(), change, showCompletionDialog: false);

    private bool ApplyBulkPropertyChange(IReadOnlyList<ElectricalComponent> selected, BulkPropertyChange change, bool showCompletionDialog)
    {
        if (selected.Count == 0 || !HasBulkPropertyChange(change))
            return false;

        _viewModel.UndoRedo.Execute(
            new BulkPropertyChangeAction(_viewModel.SelectionFilter, selected, change));

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);

        ActionLogService.Instance.Log(LogCategory.Edit, "Bulk edit applied",
            $"Count: {selected.Count}, Changes: {BuildBulkEditSummary(change)}");

        if (showCompletionDialog)
        {
            MessageBox.Show($"Updated {selected.Count} component(s):\n{BuildBulkEditSummary(change)}",
                "Bulk Edit Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return true;
    }

    private static bool HasBulkPropertyChange(BulkPropertyChange change)
        => change.LayerId != null ||
           change.Elevation.HasValue ||
           change.Material != null ||
           change.Color != null ||
           change.LineWeight.HasValue;

    private static string BuildBulkEditSummary(BulkPropertyChange change)
    {
        var summary = new StringBuilder();
        if (change.LayerId != null) summary.AppendLine($"  Layer -> {change.LayerId}");
        if (change.Elevation.HasValue) summary.AppendLine($"  Elevation -> {change.Elevation:F1} ft");
        if (change.Material != null) summary.AppendLine($"  Material -> {change.Material}");
        if (change.Color != null) summary.AppendLine($"  Color -> {change.Color}");
        if (change.LineWeight.HasValue) summary.AppendLine($"  Line Weight -> {change.LineWeight:F2} mm");

        return summary.ToString().TrimEnd();
    }

    private void ApplySelectionResult(IReadOnlyList<ElectricalComponent> matches, string message)
    {
        _viewModel.SetSelectedComponents(matches, matches.FirstOrDefault());

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);

        ActionLogService.Instance.Log(LogCategory.Selection, message);
        MessageBox.Show(message, "Selection", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── NEC Design Rule Check ───────────────────────────────────────────────

    private void RunNecCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Circuits.Any())
        {
            MessageBox.Show("No circuits defined. Add circuits first.",
                "NEC Design Check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.RebuildPanelSchedules();
        var checker = new NecDesignRuleService();
        var violations = checker.ValidateAll(
            _viewModel.Circuits.ToList(),
            _viewModel.PanelSchedules.ToList(),
            _viewModel.ElectricalCalc);

        if (violations.Count == 0)
        {
            MessageBox.Show("All circuits pass NEC design checks.",
                "NEC Design Check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("NEC DESIGN RULE CHECK RESULTS");
        sb.AppendLine(new string('═', 60));

        var errors = violations.Where(v => v.Severity == ViolationSeverity.Error).ToList();
        var warnings = violations.Where(v => v.Severity == ViolationSeverity.Warning).ToList();
        var infos = violations.Where(v => v.Severity == ViolationSeverity.Info).ToList();

        sb.AppendLine($"\n  Errors: {errors.Count}  |  Warnings: {warnings.Count}  |  Info: {infos.Count}\n");

        foreach (var v in violations)
        {
            string icon = v.Severity switch
            {
                ViolationSeverity.Error => "ERROR",
                ViolationSeverity.Warning => "WARN ",
                _ => "INFO "
            };
            sb.AppendLine($"[{icon}] {v.RuleId}");
            sb.AppendLine($"  {v.AffectedItemName}: {v.Description}");
            if (!string.IsNullOrEmpty(v.Suggestion))
                sb.AppendLine($"  Fix: {v.Suggestion}");
            sb.AppendLine();
        }

        MessageBox.Show(sb.ToString(), "NEC Design Check",
            MessageBoxButton.OK,
            errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    // ── Export Electrical Report ─────────────────────────────────────────────

    private void ExportElectricalReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var reportService = new ElectricalReportService();
            var panels = _viewModel.Components.OfType<PanelComponent>().ToList();
            var protectionReport = BuildProtectionProgramReport();

            if (!HasExportableElectricalData(protectionReport))
            {
                MessageBox.Show("No circuits, panels, or protection-study data defined.",
                    "Export Report", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = CreateElectricalReportDialog();
            if (dlg.ShowDialog() != true) return;

            string content = dlg.FileName.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)
                ? BuildElectricalReportCsv(reportService, panels, protectionReport)
                : BuildElectricalReportText(reportService, panels, protectionReport);

            reportService.SaveReport(content, dlg.FileName);
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Electrical report exported",
                $"File: {dlg.FileName}");
            MessageBox.Show("Report exported successfully!", "Export Report",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Report export failed", ex);
            MessageBox.Show($"Export failed:\n{ex.Message}", "Export Report",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool HasExportableElectricalData(ProtectionProgramService.ProgramReport protectionReport)
    {
        return _viewModel.Circuits.Any()
            || _viewModel.PanelSchedules.Any()
            || ProjectProtectionProgramService.HasMeaningfulContent(protectionReport);
    }

    private static SaveFileDialog CreateElectricalReportDialog() => new()
    {
        Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
        FileName = "electrical_report.txt",
        Title = "Export Electrical Report"
    };

    private ProtectionProgramService.ProgramReport BuildProtectionProgramReport()
    {
        _viewModel.RebuildPanelSchedules();

        var protectionProgramService = new ProjectProtectionProgramService();
        return protectionProgramService.BuildReport(
            _viewModel.Components.ToList(),
            _viewModel.PanelSchedules.ToList());
    }

    private string BuildElectricalReportCsv(
        ElectricalReportService reportService,
        IReadOnlyList<PanelComponent> panels,
        ProtectionProgramService.ProgramReport protectionReport)
    {
        var sb = new StringBuilder();

        if (_viewModel.Circuits.Any())
        {
            AppendReportSection(
                sb,
                reportService.ExportVoltageDropCsv(
                    _viewModel.Circuits.ToList(), panels, _viewModel.ElectricalCalc));
        }

        if (_viewModel.PanelSchedules.Any())
        {
            AppendReportSection(
                sb,
                reportService.ExportPanelLoadCsv(
                    _viewModel.PanelSchedules.ToList(), _viewModel.ElectricalCalc));
        }

        if (ProjectProtectionProgramService.HasMeaningfulContent(protectionReport))
            AppendReportSection(sb, reportService.ExportProtectionProgramCsv(protectionReport));

        return sb.ToString();
    }

    private string BuildElectricalReportText(
        ElectricalReportService reportService,
        IReadOnlyList<PanelComponent> panels,
        ProtectionProgramService.ProgramReport protectionReport)
    {
        var sb = new StringBuilder();

        if (_viewModel.Circuits.Any())
        {
            AppendReportSection(
                sb,
                reportService.GenerateVoltageDropReport(
                    _viewModel.Circuits.ToList(), panels, _viewModel.ElectricalCalc));
            AppendReportSection(
                sb,
                reportService.GenerateWireSizeReport(
                    _viewModel.Circuits.ToList(), _viewModel.ElectricalCalc));
        }

        if (_viewModel.PanelSchedules.Any())
        {
            AppendReportSection(
                sb,
                reportService.GeneratePanelLoadReport(
                    _viewModel.PanelSchedules.ToList(), _viewModel.ElectricalCalc));
        }

        if (ProjectProtectionProgramService.HasMeaningfulContent(protectionReport))
            AppendReportSection(sb, reportService.GenerateProtectionProgramReport(protectionReport));

        return sb.ToString();
    }

    private static void AppendReportSection(StringBuilder sb, string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        if (sb.Length > 0)
            sb.AppendLine();

        sb.Append(section.TrimEnd());
    }
}
