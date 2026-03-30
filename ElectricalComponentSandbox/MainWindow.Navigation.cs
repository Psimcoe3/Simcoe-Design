using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    // ── Zoom / Navigation Commands ────────────────────────────────────────────

    private void ZoomExtents_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Components.Any())
        {
            MessageBox.Show("No components to zoom to.", "Zoom Extents",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ZoomToComponents(_viewModel.Components.ToList());
        ActionLogService.Instance.Log(LogCategory.View, "Zoom extents");
    }

    private void ZoomSelection_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedComponents();

        if (selected.Count == 0)
        {
            MessageBox.Show("Select one or more components first.", "Zoom Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ZoomToComponents(selected);
        ActionLogService.Instance.Log(LogCategory.View, "Zoom to selection",
            $"Components: {selected.Count}");
    }

    private void HomeView_Click(object sender, RoutedEventArgs e)
    {
        PlanCanvasScale.ScaleX = 1.0;
        PlanCanvasScale.ScaleY = 1.0;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            PlanScrollViewer.ScrollToHorizontalOffset(0);
            PlanScrollViewer.ScrollToVerticalOffset(0);
        });

        UpdateStatusBar();
        ActionLogService.Instance.Log(LogCategory.View, "Home view");
    }

    private void ZoomToComponents(IReadOnlyList<Models.ElectricalComponent> components)
    {
        if (components.Count == 0) return;

        double minX = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxZ = double.MinValue;

        foreach (var c in components)
        {
            double hw = c.Parameters.Width / 2;
            double hd = c.Parameters.Depth / 2;
            minX = Math.Min(minX, c.Position.X - hw);
            maxX = Math.Max(maxX, c.Position.X + hw);
            minZ = Math.Min(minZ, c.Position.Z - hd);
            maxZ = Math.Max(maxZ, c.Position.Z + hd);
        }

        double extentW = maxX - minX;
        double extentH = maxZ - minZ;
        if (extentW < 1) extentW = 10;
        if (extentH < 1) extentH = 10;

        // Add 10% margin
        double margin = 0.1;
        minX -= extentW * margin;
        minZ -= extentH * margin;
        extentW *= 1 + 2 * margin;
        extentH *= 1 + 2 * margin;

        double viewW = PlanScrollViewer.ActualWidth;
        double viewH = PlanScrollViewer.ActualHeight;
        if (viewW < 1 || viewH < 1) return;

        // Scale factor: how many pixels per unit to fit the extents in the viewport
        // The 2D canvas maps Position.X to screen X and Position.Z to screen Y
        // Canvas pixel = (worldCoord * 20) * scale (20 px per unit is the base grid)
        double pixelsPerUnit = 20.0;
        double scaleX = viewW / (extentW * pixelsPerUnit);
        double scaleY = viewH / (extentH * pixelsPerUnit);
        double zoom = Math.Min(scaleX, scaleY);
        zoom = Math.Max(0.1, Math.Min(10.0, zoom));

        PlanCanvasScale.ScaleX = zoom;
        PlanCanvasScale.ScaleY = zoom;

        // Center the extents in the viewport
        double centerX = (minX + extentW / 2) * pixelsPerUnit * zoom;
        double centerY = (minZ + extentH / 2) * pixelsPerUnit * zoom;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            PlanScrollViewer.ScrollToHorizontalOffset(centerX - viewW / 2);
            PlanScrollViewer.ScrollToVerticalOffset(centerY - viewH / 2);
        });

        UpdateStatusBar();
    }

    // ── Status Bar Updates ────────────────────────────────────────────────────

    internal void UpdateStatusBar()
    {
        var selectedComponents = GetSelectedComponents();

        // Zoom level
        double zoomPercent = PlanCanvasScale.ScaleX * 100;
        ZoomLevelText.Text = $"Zoom: {zoomPercent:F0}%";

        // Component count
        ComponentCountText.Text = $"Components: {_viewModel.Components.Count}";

        // Selection count
        SelectionCountText.Text = $"Selected: {selectedComponents.Count}";

        // Active layer
        var layerSummary = GetSelectedLayerSummary(selectedComponents);
        ActiveLayerText.Text = $"Layer: {layerSummary}";

        // Ortho / Polar status
        if (_canvasInteractionController != null)
        {
            string mode = _canvasInteractionController.IsOrthoActive ? "ORTHO" :
                          _canvasInteractionController.IsPolarActive ? $"POLAR {_canvasInteractionController.PolarIncrementDeg:F0}°" :
                          "";
            OrthoStatusText.Text = mode;
        }

        UpdateWorkspaceOverview();
    }

    private string GetSelectedLayerSummary(IReadOnlyList<Models.ElectricalComponent> selectedComponents)
    {
        if (selectedComponents.Count == 0)
            return "Default";

        var distinctLayerIds = selectedComponents
            .Select(component => component.LayerId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctLayerIds.Count != 1)
            return "Mixed";

        return _viewModel.Layers.FirstOrDefault(layer => layer.Id == distinctLayerIds[0])?.Name ?? "Default";
    }

    internal void UpdateCoordinateDisplay(double docX, double docY)
    {
        var unit = _viewModel.UnitSystemName == "Metric" ? "m" : "ft";
        CoordinateText.Text = $"X: {docX:F3}  Y: {docY:F3} {unit}";
    }
}
