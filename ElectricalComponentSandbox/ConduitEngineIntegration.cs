using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;
using ElectricalComponentSandbox.Conduit.Persistence;
using ElectricalComponentSandbox.Conduit.UI;
using ElectricalComponentSandbox.Conduit.ViewModels;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Microsoft.Win32;

namespace ElectricalComponentSandbox;

/// <summary>
/// Partial class extension wiring the conduit authoring engine into MainWindow.
/// Adds freehand drawing, auto-route, detail levels, and export actions.
/// </summary>
public partial class MainWindow
{
    private ConduitViewModel? _conduitVm;
    private bool _isFreehandDrawing;
    private readonly List<Polyline> _conduitRunPolylines = new();
    private readonly List<ModelVisual3D> _conduitRunVisuals = new();
    private readonly List<Ellipse> _freehandPreviewDots = new();
    private Polyline? _freehandPreviewLine;

    private ConduitViewModel ConduitEngine => _conduitVm ??= new ConduitViewModel();

    private static bool IsShiftHeld() =>
        Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

    // ----- Freehand conduit tool -----

    private void FreehandConduit_Click(object sender, RoutedEventArgs e)
    {
        CancelPendingPlacement();
        ExitSketchModes();

        if (_isFreehandDrawing)
        {
            FinishFreehandConduit();
            return;
        }

        if (_isDrawingConduit)
            FinishDrawingConduit();

        if (_isEditingConduitPath)
            ToggleEditConduitPath_Click(sender, e);

        _isFreehandDrawing = true;
        ConduitEngine.SetTool(ConduitToolState.DrawFreehand);
        FreehandConduitButton.Background = new SolidColorBrush(EditModeButtonColor);
        FreehandConduitButton.Content = "Finish Freehand";
        UpdatePlanCanvasCursor();
        ActionLogService.Instance.Log(LogCategory.Edit, "Freehand conduit tool activated");
    }

    private bool HandleFreehandMouseDown(Point canvasPoint)
    {
        if (!_isFreehandDrawing)
            return false;

        var snappedCanvas = ApplyDrawingSnap(canvasPoint);
        ConduitEngine.DrawingTool.IsShiftHeld = IsShiftHeld();
        ConduitEngine.OnClick(CanvasToConduitWorld(snappedCanvas));
        DrawFreehandPreview();
        return true;
    }

    private bool HandleFreehandMouseMove(Point canvasPoint)
    {
        if (!_isFreehandDrawing || !ConduitEngine.DrawingTool.IsDrawing)
            return false;

        var snappedCanvas = ApplyDrawingSnap(canvasPoint);
        ConduitEngine.DrawingTool.IsShiftHeld = IsShiftHeld();
        ConduitEngine.OnMouseMove(CanvasToConduitWorld(snappedCanvas));
        DrawFreehandPreview();
        return true;
    }

    private bool HandleFreehandDoubleClick()
    {
        if (!_isFreehandDrawing)
            return false;

        FinishFreehandConduit();
        return true;
    }

    private void FinishFreehandConduit()
    {
        var run = ConduitEngine.FinishDrawing();
        _isFreehandDrawing = false;
        FreehandConduitButton.Background = System.Windows.SystemColors.ControlBrush;
        FreehandConduitButton.Content = "Freehand Conduit";
        UpdatePlanCanvasCursor();
        ClearFreehandPreview();

        if (run != null)
        {
            ActionLogService.Instance.Log(
                LogCategory.Component,
                "Conduit run created",
                $"RunId: {run.RunId}, Segments: {run.SegmentIds.Count}, Fittings: {run.FittingIds.Count}");
            UpdateViewport();
            Update2DCanvas();
        }
        else
        {
            ActionLogService.Instance.Log(LogCategory.Edit, "Freehand conduit cancelled", "Not enough valid segments");
        }
    }

    // ----- 2D rendering -----

    private void DrawConduitRuns2D()
    {
        foreach (var line in _conduitRunPolylines)
            PlanCanvas.Children.Remove(line);

        _conduitRunPolylines.Clear();

        if (_conduitVm == null)
            return;

        Brush defaultBrush = Brushes.DodgerBlue;
        var overrideBrushes = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        foreach (var run in ConduitEngine.Store.GetAllRuns())
        {
            foreach (var seg in run.GetSegments(ConduitEngine.Store))
            {
                var data = ConduitEngine.Renderer2D.GenerateRenderData(seg);
                if (data.Overrides?.IsHidden == true)
                    continue;

                var colorOverride = data.Overrides?.ColorOverride;
                Brush brush = defaultBrush;
                if (!string.IsNullOrWhiteSpace(colorOverride))
                {
                    if (!overrideBrushes.TryGetValue(colorOverride, out brush!))
                    {
                        try
                        {
                            var parsedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorOverride));
                            parsedBrush.Freeze();
                            brush = parsedBrush;
                        }
                        catch
                        {
                            brush = defaultBrush;
                        }

                        overrideBrushes[colorOverride] = brush;
                    }
                }

                var start = ConduitWorldToCanvas(data.Start);
                var end = ConduitWorldToCanvas(data.End);

                if (data.DetailLevel == ConduitDetailLevel.Fine)
                {
                    var dx = end.X - start.X;
                    var dy = end.Y - start.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6)
                        continue;

                    var halfWidth = Math.Max(2.0, data.Width * 10.0);
                    var px = -dy / len * halfWidth;
                    var py = dx / len * halfWidth;

                    AddConduitLine(new Point(start.X + px, start.Y + py), new Point(end.X + px, end.Y + py), brush, 1.5);
                    AddConduitLine(new Point(start.X - px, start.Y - py), new Point(end.X - px, end.Y - py), brush, 1.5);
                }
                else
                {
                    var thickness = data.DetailLevel == ConduitDetailLevel.Medium ? 2.5 : 1.5;
                    AddConduitLine(start, end, brush, thickness);
                }
            }
        }
    }

    private void AddConduitLine(Point start, Point end, Brush brush, double thickness)
    {
        var polyline = new Polyline
        {
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        polyline.Points.Add(start);
        polyline.Points.Add(end);
        PlanCanvas.Children.Add(polyline);
        _conduitRunPolylines.Add(polyline);
    }

    private static Point CanvasToConduitWorld(Point canvas) =>
        new((canvas.X - 1000.0) / 20.0, (1000.0 - canvas.Y) / 20.0);

    private static Point ConduitWorldToCanvas(Point world) =>
        new(1000.0 + world.X * 20.0, 1000.0 - world.Y * 20.0);

    // ----- 3D rendering -----

    private void UpdateConduitRuns3D()
    {
        foreach (var visual in _conduitRunVisuals)
            Viewport.Children.Remove(visual);

        _conduitRunVisuals.Clear();

        if (_conduitVm == null)
            return;

        foreach (var model in ConduitEngine.View3D.GenerateAllModels())
        {
            var visual = new ModelVisual3D { Content = model };
            Viewport.Children.Add(visual);
            _conduitRunVisuals.Add(visual);
        }
    }

    // ----- Freehand preview -----

    private void DrawFreehandPreview()
    {
        ClearFreehandPreview();

        if (_conduitVm == null)
            return;

        var vertices = ConduitEngine.DrawingTool.PendingVertices;
        if (vertices.Count == 0)
            return;

        if (vertices.Count >= 2)
        {
            _freehandPreviewLine = new Polyline
            {
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            };

            foreach (var worldPoint in vertices)
                _freehandPreviewLine.Points.Add(ConduitWorldToCanvas(worldPoint));

            PlanCanvas.Children.Add(_freehandPreviewLine);
        }

        foreach (var worldPoint in vertices)
        {
            var canvasPoint = ConduitWorldToCanvas(worldPoint);
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.OrangeRed,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, canvasPoint.X - 3);
            Canvas.SetTop(dot, canvasPoint.Y - 3);
            PlanCanvas.Children.Add(dot);
            _freehandPreviewDots.Add(dot);
        }
    }

    private void ClearFreehandPreview()
    {
        if (_freehandPreviewLine != null)
        {
            PlanCanvas.Children.Remove(_freehandPreviewLine);
            _freehandPreviewLine = null;
        }

        foreach (var dot in _freehandPreviewDots)
            PlanCanvas.Children.Remove(dot);

        _freehandPreviewDots.Clear();
    }

    // ----- Toolbar handlers -----

    private void AutoRoute_Click(object sender, RoutedEventArgs e)
    {
        var elevation = ConduitEngine.DrawingTool.Elevation;
        var path = new List<XYZ>();

        if (_viewModel.SelectedComponent is ConduitComponent selectedConduit)
        {
            foreach (var point in selectedConduit.GetPathPoints())
            {
                path.Add(new XYZ(
                    selectedConduit.Position.X + point.X,
                    selectedConduit.Position.Z + point.Z,
                    elevation));
            }
        }

        if (path.Count < 2)
        {
            path = new List<XYZ>
            {
                new XYZ(0, 0, elevation),
                new XYZ(12, 0, elevation),
                new XYZ(12, 8, elevation),
                new XYZ(12, 8, elevation + 3)
            };
        }

        var run = ConduitEngine.AutoRouteAlongPath(path, new RoutingOptions
        {
            ConduitTypeId = ConduitEngine.Store.Settings.DefaultConduitTypeId,
            TradeSize = ConduitEngine.DrawingTool.TradeSize,
            Elevation = elevation,
            Material = ConduitEngine.DrawingTool.Material
        });

        if (run == null)
        {
            MessageBox.Show("Auto-route did not generate a run.", "Auto-Route", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateViewport();
        Update2DCanvas();

        MessageBox.Show(
            $"Run {run.RunId}\nSegments: {run.SegmentIds.Count}\nFittings: {run.FittingIds.Count}\nLength: {run.ComputeTotalLength(ConduitEngine.Store):F2} ft",
            "Auto-Route",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ExportRunSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (!ConduitEngine.Store.GetAllRuns().Any())
        {
            MessageBox.Show("No conduit runs to export.", "Export Runs CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "RunSchedule.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            await ConduitEngine.ScheduleService.ExportScheduleCsvAsync(dialog.FileName);
            MessageBox.Show("Run schedule exported.", "Export Runs CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ExportConduitJson_Click(object sender, RoutedEventArgs e)
    {
        if (!ConduitEngine.Store.GetAllRuns().Any())
        {
            MessageBox.Show("No conduit runs to export.", "Export Conduit JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "ConduitModel.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await ConduitPersistence.SaveToFileAsync(ConduitEngine.Store, dialog.FileName);
            MessageBox.Show("Conduit model exported.", "Export Conduit JSON", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DetailLevel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem selected)
            return;

        ConduitEngine.DetailLevel = selected.Content?.ToString() switch
        {
            "Medium" => ConduitDetailLevel.Medium,
            "Fine" => ConduitDetailLevel.Fine,
            _ => ConduitDetailLevel.Coarse
        };

        // Only update canvas if PlanCanvas is initialized
        if (PlanCanvas != null)
            Update2DCanvas();
    }

    private void TradeSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem selected)
        {
            var size = selected.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(size))
                ConduitEngine.DrawingTool.TradeSize = size;
        }
    }
}
