using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    // ── Move / Rotate / Mirror Commands ──────────────────────────────────────

    private void MoveComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Move",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var src = _viewModel.SelectedComponent;
        var input = PromptInput("Move Component",
            $"Current position: ({src.Position.X:F2}, {src.Position.Y:F2}, {src.Position.Z:F2})\n\n" +
            "Enter displacement (dX, dY, dZ) or new position as (X, Y, Z):",
            "2, 0, 0");
        if (input == null) return;

        var parts = input.Split(',');
        if (parts.Length < 2)
        {
            MessageBox.Show("Enter at least X, Y values separated by commas.", "Move",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(parts[0].Trim(), out double dx)) return;
        if (!double.TryParse(parts[1].Trim(), out double dy)) return;
        double dz = parts.Length >= 3 && double.TryParse(parts[2].Trim(), out double z) ? z : 0;

        // Determine if absolute or relative based on magnitude hint
        var modeInput = PromptInput("Move Component", "Mode:\n1. Relative (offset)\n2. Absolute (new position)", "1");
        bool isAbsolute = modeInput == "2";

        Point3D newPos;
        if (isAbsolute)
            newPos = new Point3D(dx, dy, dz);
        else
            newPos = new Point3D(src.Position.X + dx, src.Position.Y + dy, src.Position.Z + dz);

        _viewModel.UndoRedo.Execute(new MoveComponentAction(src, src.Position, newPos));
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Component moved",
            $"Name: {src.Name}, To: ({newPos.X:F2}, {newPos.Y:F2}, {newPos.Z:F2})");
    }

    private void RotateComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Rotate",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var src = _viewModel.SelectedComponent;
        var input = PromptInput("Rotate Component",
            $"Current rotation: ({src.Rotation.X:F1}°, {src.Rotation.Y:F1}°, {src.Rotation.Z:F1}°)\n\n" +
            "Enter rotation angle in degrees (Y-axis for plan view):",
            "90");
        if (!double.TryParse(input, out double angle)) return;

        var oldRot = src.Rotation;
        var newRot = new Vector3D(oldRot.X, oldRot.Y + angle, oldRot.Z);

        _viewModel.UndoRedo.Execute(new RotateComponentAction(src, oldRot, newRot));
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Component rotated",
            $"Name: {src.Name}, Angle: {angle}°");
    }

    private void MirrorComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Mirror",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var src = _viewModel.SelectedComponent;
        var input = PromptInput("Mirror Component",
            "Mirror axis:\n1. X axis (flip left/right)\n2. Z axis (flip top/bottom)\n3. About a point (X, Z)",
            "1");
        if (input == null) return;

        Point3D mirroredPos;
        Vector3D mirroredScale;

        if (input == "1")
        {
            // Mirror about X axis: negate X position, flip X scale
            mirroredPos = new Point3D(-src.Position.X, src.Position.Y, src.Position.Z);
            mirroredScale = new Vector3D(-src.Scale.X, src.Scale.Y, src.Scale.Z);
        }
        else if (input == "2")
        {
            // Mirror about Z axis: negate Z position, flip Z scale
            mirroredPos = new Point3D(src.Position.X, src.Position.Y, -src.Position.Z);
            mirroredScale = new Vector3D(src.Scale.X, src.Scale.Y, -src.Scale.Z);
        }
        else
        {
            // Mirror about a point
            var parts = input.Split(',');
            if (parts.Length < 2 ||
                !double.TryParse(parts[0].Trim(), out double mx) ||
                !double.TryParse(parts[1].Trim(), out double mz))
            {
                MessageBox.Show("Invalid mirror point. Use format: X, Z", "Mirror",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            mirroredPos = new Point3D(2 * mx - src.Position.X, src.Position.Y, 2 * mz - src.Position.Z);
            mirroredScale = src.Scale; // point mirror preserves scale
        }

        _viewModel.UndoRedo.Execute(new MirrorComponentAction(src, mirroredPos, mirroredScale));
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Component mirrored",
            $"Name: {src.Name}");
    }

    // ── Schedule Table Annotation ────────────────────────────────────────────

    private void InsertScheduleTable_Click(object sender, RoutedEventArgs e)
    {
        var types = new[] { "Equipment Schedule", "Conduit Schedule", "Circuit Summary" };
        var list = string.Join("\n", types.Select((t, i) => $"{i + 1}. {t}"));
        var input = PromptInput("Insert Schedule Table",
            $"Select schedule type:\n\n{list}", "1");
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > types.Length) return;

        var service = new ScheduleTableService();
        ScheduleTable table;

        switch (idx)
        {
            case 1:
                table = service.GenerateEquipmentSchedule(_viewModel.Components.ToList());
                break;
            case 2:
                table = service.GenerateConduitSchedule(_viewModel.Components.ToList());
                break;
            case 3:
                table = service.GenerateCircuitSummary(_viewModel.Circuits.ToList());
                break;
            default:
                return;
        }

        if (table.Rows.Count == 0)
        {
            MessageBox.Show("No data for this schedule type.", "Schedule Table",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryPromptForDocumentPoint(
                "Insert Schedule Table",
                "Top-left insertion point (X, Y document units):",
                "120, 120",
                out var origin))
        {
            return;
        }

        var markups = _drawingAnnotationMarkupService.CreateScheduleTableMarkups(table, origin);
        InsertGeneratedMarkups(
            markups,
            $"Insert {table.Title}",
            "Schedule table inserted",
            $"Type: {types[idx - 1]}, Rows: {table.Rows.Count}");
    }

    private void ExportScheduleDxf_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Components.Any())
        {
            MessageBox.Show("No components to export.", "Export DXF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DXF Files (*.dxf)|*.dxf",
            FileName = "electrical_drawing.dxf"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var dxfService = new Services.Export.DxfExportService();
            dxfService.Export(
                _viewModel.Components.ToList(),
                _viewModel.Layers.ToList(),
                dlg.FileName);

            MessageBox.Show($"DXF exported to:\n{dlg.FileName}", "Export DXF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ActionLogService.Instance.Log(LogCategory.FileOperation, "DXF exported", dlg.FileName);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"DXF export failed:\n{ex.Message}", "Export DXF",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Layer State Snapshots ───────────────────────────────────────────────

    private void SaveLayerState_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptInput("Save Layer State",
            "Enter a name for this layer state:", $"State {_viewModel.LayerManager.SavedStates.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;

        _viewModel.LayerManager.SaveState(name);
        ActionLogService.Instance.Log(LogCategory.View, "Layer state saved", $"Name: {name}");
        MessageBox.Show($"Layer state '{name}' saved.", "Layer States",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreLayerState_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.LayerManager.SavedStates.Any())
        {
            MessageBox.Show("No saved layer states.", "Restore Layer State",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n", _viewModel.LayerManager.SavedStates
            .Select((s, i) => $"{i + 1}. {s.Name} ({s.SavedUtc:g})"));
        var input = PromptInput("Restore Layer State",
            $"Enter the number of the state to restore:\n\n{names}", "1");
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > _viewModel.LayerManager.SavedStates.Count)
            return;

        var state = _viewModel.LayerManager.SavedStates[idx - 1];
        _viewModel.LayerManager.RestoreState(state.Name);
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.View, "Layer state restored", $"Name: {state.Name}");
    }

    private void DeleteLayerState_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.LayerManager.SavedStates.Any())
        {
            MessageBox.Show("No saved layer states.", "Delete Layer State",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n", _viewModel.LayerManager.SavedStates
            .Select((s, i) => $"{i + 1}. {s.Name}"));
        var input = PromptInput("Delete Layer State",
            $"Enter the number of the state to delete:\n\n{names}", "");
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > _viewModel.LayerManager.SavedStates.Count)
            return;

        var state = _viewModel.LayerManager.SavedStates[idx - 1];
        _viewModel.LayerManager.DeleteState(state.Name);
        MessageBox.Show($"Layer state '{state.Name}' deleted.", "Layer States",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Array Commands ─────────────────────────────────────────────────────

    private void RectangularArray_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Rectangular Array",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rowStr = PromptInput("Rectangular Array", "Number of rows:", "3");
        if (!int.TryParse(rowStr, out int rows) || rows < 1) return;

        var colStr = PromptInput("Rectangular Array", "Number of columns:", "3");
        if (!int.TryParse(colStr, out int cols) || cols < 1) return;

        var spacingStr = PromptInput("Rectangular Array", "Spacing (row, column):", "5, 5");
        if (spacingStr == null) return;
        var spacingParts = spacingStr.Split(',');
        if (spacingParts.Length < 2 ||
            !double.TryParse(spacingParts[0].Trim(), out double rowSpacing) ||
            !double.TryParse(spacingParts[1].Trim(), out double colSpacing))
            return;

        var arrayParams = new RectangularArrayParams
        {
            Rows = rows,
            Columns = cols,
            RowSpacing = rowSpacing,
            ColumnSpacing = colSpacing
        };

        var arrayService = new ArrayService();
        var placements = arrayService.ComputeRectangularArray(
            _viewModel.SelectedComponent.Position,
            _viewModel.SelectedComponent.Rotation,
            arrayParams);

        var src = _viewModel.SelectedComponent;
        var actions = new System.Collections.Generic.List<IUndoableAction>();

        foreach (var placement in placements)
        {
            var copy = ElectricalComponentCatalog.CreateDefaultComponent(src.Type);
            CopyComponentProperties(src, copy);
            copy.Name = src.Name + $" (Array)";
            copy.Position = placement.Position;
            copy.Rotation = placement.Rotation;
            actions.Add(new AddComponentAction(_viewModel.Components, copy));
        }

        if (actions.Count > 0)
        {
            _viewModel.UndoRedo.Execute(new CompositeAction(
                $"Rectangular array {rows}x{cols}", actions));
            QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Component, "Rectangular array created",
                $"Source: {src.Name}, Rows: {rows}, Cols: {cols}, Total: {placements.Count}");
        }
    }

    private void PolarArray_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Polar Array",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var centerStr = PromptInput("Polar Array", "Center point (X, Z):", "0, 0");
        if (centerStr == null) return;
        var centerParts = centerStr.Split(',');
        if (centerParts.Length < 2 ||
            !double.TryParse(centerParts[0].Trim(), out double cx) ||
            !double.TryParse(centerParts[1].Trim(), out double cz))
            return;

        var countStr = PromptInput("Polar Array", "Number of items (including source):", "6");
        if (!int.TryParse(countStr, out int count) || count < 2) return;

        var angleStr = PromptInput("Polar Array", "Total angle (degrees):", "360");
        if (!double.TryParse(angleStr, out double totalAngle)) return;

        var polarParams = new PolarArrayParams
        {
            Center = new Point3D(cx, _viewModel.SelectedComponent.Position.Y, cz),
            Count = count,
            TotalAngleDegrees = totalAngle,
            RotateItems = true
        };

        var arrayService = new ArrayService();
        var placements = arrayService.ComputePolarArray(
            _viewModel.SelectedComponent.Position,
            _viewModel.SelectedComponent.Rotation,
            polarParams);

        var src = _viewModel.SelectedComponent;
        var actions = new System.Collections.Generic.List<IUndoableAction>();

        foreach (var placement in placements)
        {
            var copy = ElectricalComponentCatalog.CreateDefaultComponent(src.Type);
            CopyComponentProperties(src, copy);
            copy.Name = src.Name + $" (Array)";
            copy.Position = placement.Position;
            copy.Rotation = placement.Rotation;
            actions.Add(new AddComponentAction(_viewModel.Components, copy));
        }

        if (actions.Count > 0)
        {
            _viewModel.UndoRedo.Execute(new CompositeAction(
                $"Polar array {count} items", actions));
            QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Component, "Polar array created",
                $"Source: {src.Name}, Count: {count}, Angle: {totalAngle}°");
        }
    }

    private static void CopyComponentProperties(ElectricalComponent src, ElectricalComponent copy)
    {
        copy.LayerId = src.LayerId;
        copy.Rotation = src.Rotation;
        copy.Scale = src.Scale;
        copy.Parameters.Width = src.Parameters.Width;
        copy.Parameters.Height = src.Parameters.Height;
        copy.Parameters.Depth = src.Parameters.Depth;
        copy.Parameters.Elevation = src.Parameters.Elevation;
        copy.Parameters.Material = src.Parameters.Material;
        copy.Parameters.Manufacturer = src.Parameters.Manufacturer;
        copy.Parameters.Color = src.Parameters.Color;
    }

    // ── Duplicate Component ─────────────────────────────────────────────────

    private void DuplicateComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Select a component first.", "Duplicate",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var src = _viewModel.SelectedComponent;
        var copy = ElectricalComponentCatalog.CreateDefaultComponent(src.Type);
        copy.Name = src.Name + " (Copy)";
        copy.LayerId = src.LayerId;
        copy.Position = new Point3D(src.Position.X + 2, src.Position.Y, src.Position.Z + 2);
        copy.Rotation = src.Rotation;
        copy.Scale = src.Scale;
        copy.Parameters.Width = src.Parameters.Width;
        copy.Parameters.Height = src.Parameters.Height;
        copy.Parameters.Depth = src.Parameters.Depth;
        copy.Parameters.Elevation = src.Parameters.Elevation;
        copy.Parameters.Material = src.Parameters.Material;
        copy.Parameters.Manufacturer = src.Parameters.Manufacturer;
        copy.Parameters.Color = src.Parameters.Color;

        _viewModel.UndoRedo.Execute(new AddComponentAction(_viewModel.Components, copy));
        _viewModel.SelectedComponent = copy;

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Component, "Component duplicated",
            $"Source: {src.Name}, Copy: {copy.Name}");
    }

    // ── Measurement Tools ───────────────────────────────────────────────────

    private void MeasureDistance_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Components.Count < 2)
        {
            MessageBox.Show("Need at least 2 components to measure between.",
                "Measure Distance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selected = _viewModel.Components
            .Where(c => _viewModel.SelectedComponentIds.Contains(c.Id))
            .ToList();

        if (selected.Count < 2)
        {
            MessageBox.Show("Select 2 components to measure the distance between them.\n" +
                "Use window/crossing selection to select multiple components.",
                "Measure Distance", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var p1 = selected[0].Position;
        var p2 = selected[1].Position;
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double dz = p2.Z - p1.Z;
        double dist3D = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        double dist2D = System.Math.Sqrt(dx * dx + dz * dz);

        var unit = _viewModel.UnitSystemName == "Metric" ? "m" : "ft";
        MessageBox.Show(
            $"Distance: {selected[0].Name} → {selected[1].Name}\n\n" +
            $"  2D (plan): {dist2D:F3} {unit}\n" +
            $"  3D:        {dist3D:F3} {unit}\n\n" +
            $"  ΔX: {dx:F3}  ΔY: {dy:F3}  ΔZ: {dz:F3}\n" +
            $"  Angle (plan): {System.Math.Atan2(dz, dx) * 180 / System.Math.PI:F1}°",
            "Measure Distance", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MeasureArea_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.Components
            .Where(c => _viewModel.SelectedComponentIds.Contains(c.Id))
            .ToList();

        if (selected.Count < 3)
        {
            MessageBox.Show("Select 3+ components to compute the enclosed area.\n" +
                "The area is calculated from the convex hull of component positions.",
                "Measure Area", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Shoelace formula on XZ plane (plan view)
        var pts = selected.Select(c => (X: c.Position.X, Z: c.Position.Z)).ToList();
        // Sort by angle from centroid for simple polygon
        double cx = pts.Average(p => p.X), cz = pts.Average(p => p.Z);
        pts = pts.OrderBy(p => System.Math.Atan2(p.Z - cz, p.X - cx)).ToList();

        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            int j = (i + 1) % pts.Count;
            area += pts[i].X * pts[j].Z;
            area -= pts[j].X * pts[i].Z;
        }
        area = System.Math.Abs(area) / 2.0;

        var unit = _viewModel.UnitSystemName == "Metric" ? "m²" : "sq ft";
        MessageBox.Show(
            $"Enclosed area ({selected.Count} points): {area:F2} {unit}",
            "Measure Area", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Circuit Edit / Delete ───────────────────────────────────────────────

    private void EditCircuit_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Circuits.Any())
        {
            MessageBox.Show("No circuits defined.", "Edit Circuit",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n", _viewModel.Circuits
            .Select((c, i) => $"{i + 1}. #{c.CircuitNumber} - {c.Description} ({c.Voltage}V {c.Breaker.Poles}P)"));
        var input = PromptInput("Edit Circuit", $"Select circuit number:\n\n{names}", "1");
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > _viewModel.Circuits.Count) return;

        var circuit = _viewModel.Circuits[idx - 1];

        // Description
        var desc = PromptInput("Edit Circuit – Description", "Description:", circuit.Description);
        if (desc != null && desc != circuit.Description)
        {
            var old = circuit.Description;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<string>(
                $"Edit circuit {circuit.CircuitNumber} description",
                v => circuit.Description = v, old, desc));
        }

        // Voltage
        var voltStr = PromptInput("Edit Circuit – Voltage", "Voltage (120, 208, 240, 277, 480):",
            circuit.Voltage.ToString("F0"));
        if (voltStr != null && double.TryParse(voltStr, out double volt) &&
            System.Math.Abs(volt - circuit.Voltage) > 0.1)
        {
            var old = circuit.Voltage;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<double>(
                $"Edit circuit {circuit.CircuitNumber} voltage",
                v => circuit.Voltage = v, old, volt));
        }

        // Load
        var loadStr = PromptInput("Edit Circuit – Load (VA)", "Connected load (VA):",
            circuit.ConnectedLoadVA.ToString("F0"));
        if (loadStr != null && double.TryParse(loadStr, out double load) &&
            System.Math.Abs(load - circuit.ConnectedLoadVA) > 0.1)
        {
            var old = circuit.ConnectedLoadVA;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<double>(
                $"Edit circuit {circuit.CircuitNumber} load",
                v => circuit.ConnectedLoadVA = v, old, load));
        }

        // Wire size
        var wireStr = PromptInput("Edit Circuit – Wire Size", "Wire size (14, 12, 10, 8, 6, 4, 2, 1/0, etc.):",
            circuit.Wire.Size);
        if (wireStr != null && wireStr != circuit.Wire.Size)
        {
            var old = circuit.Wire.Size;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<string>(
                $"Edit circuit {circuit.CircuitNumber} wire size",
                v => circuit.Wire.Size = v, old, wireStr));
        }

        // Breaker
        var brkStr = PromptInput("Edit Circuit – Breaker", "Breaker trip amps:",
            circuit.Breaker.TripAmps.ToString());
        if (brkStr != null && int.TryParse(brkStr, out int brk) && brk != circuit.Breaker.TripAmps)
        {
            var old = circuit.Breaker.TripAmps;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<int>(
                $"Edit circuit {circuit.CircuitNumber} breaker",
                v => circuit.Breaker.TripAmps = v, old, brk));
        }

        // Wire length
        var lenStr = PromptInput("Edit Circuit – Wire Length", "Wire length (feet):",
            circuit.WireLengthFeet.ToString("F0"));
        if (lenStr != null && double.TryParse(lenStr, out double len) &&
            System.Math.Abs(len - circuit.WireLengthFeet) > 0.1)
        {
            var old = circuit.WireLengthFeet;
            _viewModel.UndoRedo.Execute(new PropertyChangeAction<double>(
                $"Edit circuit {circuit.CircuitNumber} wire length",
                v => circuit.WireLengthFeet = v, old, len));
        }

        ActionLogService.Instance.Log(LogCategory.Component, "Circuit edited",
            $"Number: {circuit.CircuitNumber}");
    }

    private void DeleteCircuit_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Circuits.Any())
        {
            MessageBox.Show("No circuits to delete.", "Delete Circuit",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n", _viewModel.Circuits
            .Select((c, i) => $"{i + 1}. #{c.CircuitNumber} - {c.Description}"));
        var input = PromptInput("Delete Circuit", $"Select circuit to delete:\n\n{names}", "");
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > _viewModel.Circuits.Count) return;

        var circuit = _viewModel.Circuits[idx - 1];
        if (MessageBox.Show($"Delete circuit #{circuit.CircuitNumber} ({circuit.Description})?",
            "Delete Circuit", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _viewModel.UndoRedo.Execute(
                new RemoveItemAction<Circuit>(_viewModel.Circuits, circuit, $"Circuit {circuit.CircuitNumber}"));
            ActionLogService.Instance.Log(LogCategory.Component, "Circuit deleted",
                $"Number: {circuit.CircuitNumber}");
        }
    }

    // ── Conduit Fill Check ──────────────────────────────────────────────────

    private void ConduitFillCheck_Click(object sender, RoutedEventArgs e)
    {
        var conduitSize = PromptInput("Conduit Fill Check",
            "Conduit trade size (e.g. 1/2, 3/4, 1, 1-1/4, 1-1/2, 2, 3, 4):", "3/4");
        if (string.IsNullOrWhiteSpace(conduitSize)) return;

        var wireInput = PromptInput("Conduit Fill Check",
            "Wire sizes, comma-separated (e.g. 12,12,12,12):", "12,12,12");
        if (string.IsNullOrWhiteSpace(wireInput)) return;

        var wireSizes = wireInput.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        try
        {
            var fillService = new ConduitFillService();
            var result = fillService.CalculateFill(conduitSize,
                Conduit.Core.Model.ConduitMaterialType.EMT, wireSizes);

            var sb = new StringBuilder();
            sb.AppendLine("CONDUIT FILL ANALYSIS");
            sb.AppendLine(new string('─', 50));
            sb.AppendLine($"  Conduit: {conduitSize}\" EMT");
            sb.AppendLine($"  Conduit Area: {result.ConduitAreaSqIn:F4} sq in");
            sb.AppendLine($"  Conductors: {result.ConductorCount}");
            sb.AppendLine($"  Wire Area: {result.TotalWireAreaSqIn:F4} sq in");
            sb.AppendLine($"  Fill: {result.FillPercent:F1}%");
            sb.AppendLine($"  Max Allowed: {result.MaxAllowedFillPercent:F0}%");
            sb.AppendLine($"  Reference: {result.NecReference}");
            sb.AppendLine();

            if (result.ExceedsCode)
                sb.AppendLine("  ⚠ EXCEEDS NEC FILL LIMIT — upsize conduit");
            else
                sb.AppendLine("  ✓ Within code limits");

            var recommended = fillService.RecommendConduitSize(
                Conduit.Core.Model.ConduitMaterialType.EMT, wireSizes);
            sb.AppendLine($"\n  Recommended minimum size: {recommended}\"");

            MessageBox.Show(sb.ToString(), "Conduit Fill Check",
                MessageBoxButton.OK,
                result.ExceedsCode ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Conduit fill check failed:\n{ex.Message}",
                "Conduit Fill Check", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
