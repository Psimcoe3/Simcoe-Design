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
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first to filter by its type.",
                "Select by Type", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var matches = _viewModel.SelectionFilter.SelectByType(
            _viewModel.Components, _viewModel.SelectedComponent.Type);
        ApplySelectionResult(matches, $"Selected {matches.Count} {_viewModel.SelectedComponent.Type} component(s)");
    }

    private void SelectSimilar_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.",
                "Select Similar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var matches = _viewModel.SelectionFilter.SelectSimilar(
            _viewModel.Components, _viewModel.SelectedComponent);
        ApplySelectionResult(matches, $"Selected {matches.Count} similar component(s)");
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
        if (!_viewModel.SelectedComponentIds.Any())
        {
            MessageBox.Show("Select components first (window/crossing selection or Select by Layer/Type).",
                "Bulk Edit", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selected = _viewModel.Components
            .Where(c => _viewModel.SelectedComponentIds.Contains(c.Id))
            .ToList();

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

        // Color
        var colorInput = PromptInput("Bulk Edit – Color",
            "New color hex (e.g. #FF0000, leave blank to skip):", "");
        if (!string.IsNullOrWhiteSpace(colorInput))
        {
            change.Color = colorInput;
            anyChange = true;
        }

        if (!anyChange) return;

        _viewModel.UndoRedo.Execute(
            new BulkPropertyChangeAction(_viewModel.SelectionFilter, selected, change));

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);

        var summary = new System.Text.StringBuilder();
        summary.Append($"Updated {selected.Count} component(s):");
        if (change.LayerId != null) summary.Append($"\n  Layer → {change.LayerId}");
        if (change.Elevation.HasValue) summary.Append($"\n  Elevation → {change.Elevation:F1} ft");
        if (change.Color != null) summary.Append($"\n  Color → {change.Color}");

        MessageBox.Show(summary.ToString(),
            "Bulk Edit Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (!_viewModel.Circuits.Any() && !_viewModel.PanelSchedules.Any())
        {
            MessageBox.Show("No circuits or panels defined.",
                "Export Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
            FileName = "electrical_report.txt",
            Title = "Export Electrical Report"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _viewModel.RebuildPanelSchedules();
            var reportService = new ElectricalReportService();
            var panels = _viewModel.Components.OfType<PanelComponent>().ToList();

            string content;
            if (dlg.FileName.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                if (_viewModel.Circuits.Any())
                    sb.Append(reportService.ExportVoltageDropCsv(
                        _viewModel.Circuits.ToList(), panels, _viewModel.ElectricalCalc));
                if (_viewModel.PanelSchedules.Any())
                {
                    sb.AppendLine();
                    sb.Append(reportService.ExportPanelLoadCsv(
                        _viewModel.PanelSchedules.ToList(), _viewModel.ElectricalCalc));
                }
                content = sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                if (_viewModel.Circuits.Any())
                {
                    sb.AppendLine(reportService.GenerateVoltageDropReport(
                        _viewModel.Circuits.ToList(), panels, _viewModel.ElectricalCalc));
                    sb.AppendLine();
                    sb.AppendLine(reportService.GenerateWireSizeReport(
                        _viewModel.Circuits.ToList(), _viewModel.ElectricalCalc));
                }
                if (_viewModel.PanelSchedules.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine(reportService.GeneratePanelLoadReport(
                        _viewModel.PanelSchedules.ToList(), _viewModel.ElectricalCalc));
                }
                content = sb.ToString();
            }

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
}
