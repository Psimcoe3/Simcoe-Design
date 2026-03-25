using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private Dictionary<string, bool> BuildLayerVisibilityLookup()
    {
        var lookup = new Dictionary<string, bool>(_viewModel.Layers.Count, StringComparer.Ordinal);
        foreach (var layer in _viewModel.Layers)
            lookup[layer.Id] = layer.IsVisible && !layer.IsFrozen;

        return lookup;
    }

    private Dictionary<string, bool> BuildLayerPlottableLookup()
    {
        var lookup = new Dictionary<string, bool>(_viewModel.Layers.Count, StringComparer.Ordinal);
        foreach (var layer in _viewModel.Layers)
            lookup[layer.Id] = layer.IsVisible && !layer.IsFrozen && layer.IsPlotted;

        return lookup;
    }

    private static bool IsLayerVisible(IReadOnlyDictionary<string, bool> layerVisibilityById, string layerId)
    {
        return !layerVisibilityById.TryGetValue(layerId, out var visible) || visible;
    }

    private static bool IsLayerPlottable(IReadOnlyDictionary<string, bool> layerPlottableById, string layerId)
    {
        return !layerPlottableById.TryGetValue(layerId, out var plottable) || plottable;
    }

    private void InitializeDimensionDisplayDefaults()
    {
        _dimensionDisplayMode = GetSelectedDimensionDisplayMode();
        _dimensionInchFractionDenominator = GetSelectedDimensionIncrementDenominator();
    }

    private DimensionDisplayMode GetSelectedDimensionDisplayMode()
    {
        if (DimensionDisplayFormatCombo?.SelectedItem is ComboBoxItem item)
        {
            var selected = item.Content?.ToString();
            if (string.Equals(selected, "Decimal Feet", StringComparison.OrdinalIgnoreCase))
                return DimensionDisplayMode.DecimalFeet;
        }

        return DimensionDisplayMode.FeetInches;
    }

    private int GetSelectedDimensionIncrementDenominator()
    {
        if (DimensionIncrementCombo?.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return denominator;
        }

        return DefaultDimensionInchFractionDenominator;
    }

    private CustomDimensionSnapMode GetSelectedCustomDimensionSnapMode()
    {
        if (CustomDimensionSnapModeCombo?.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<CustomDimensionSnapMode>(item.Tag?.ToString(), out var mode))
        {
            return mode;
        }

        return CustomDimensionSnapMode.Auto;
    }

    private static char GetDominantAxis(Vector3D delta)
    {
        var absX = Math.Abs(delta.X);
        var absY = Math.Abs(delta.Y);
        var absZ = Math.Abs(delta.Z);

        if (absX >= absY && absX >= absZ)
            return 'X';
        if (absY >= absX && absY >= absZ)
            return 'Y';

        return 'Z';
    }

    private void CancelCustomDimensionMode()
    {
        _isAddingCustomDimension = false;
        _pendingCustomDimensionStartAnchor = null;
        _customDimensionPlacementState.Reset();
        ClearCustomDimensionPreview();
    }

    private void PruneCustomDimensions()
    {
        if (_customDimensionAnnotations.Count == 0)
            return;

        var validComponentIds = _viewModel.Components
            .Select(component => component.Id)
            .ToHashSet(StringComparer.Ordinal);

        _customDimensionAnnotations.RemoveAll(annotation =>
            (!string.IsNullOrEmpty(annotation.Start.ComponentId) && !validComponentIds.Contains(annotation.Start.ComponentId)) ||
            (!string.IsNullOrEmpty(annotation.End.ComponentId) && !validComponentIds.Contains(annotation.End.ComponentId)));

        if (_pendingCustomDimensionStartAnchor != null &&
            !string.IsNullOrEmpty(_pendingCustomDimensionStartAnchor.ComponentId) &&
            !validComponentIds.Contains(_pendingCustomDimensionStartAnchor.ComponentId))
        {
            _pendingCustomDimensionStartAnchor = null;
            _customDimensionPlacementState.Reset();
        }
    }

    private void UpdateCustomDimensionUiState()
    {
        if (CustomDimensionModeButton == null ||
            ClearCustomDimensionsButton == null ||
            CustomDimensionModeTextBlock == null)
        {
            return;
        }

        CustomDimensionModeButton.Content = _isAddingCustomDimension
            ? "Cancel Custom Dimension"
            : "Add Custom Dimension";

        var selected = _viewModel?.SelectedComponent;
        var selectedCount = selected == null
            ? 0
            : _customDimensionAnnotations.Count(annotation =>
                string.Equals(annotation.Start.ComponentId, selected.Id, StringComparison.Ordinal) ||
                string.Equals(annotation.End.ComponentId, selected.Id, StringComparison.Ordinal));

        ClearCustomDimensionsButton.IsEnabled = selected == null
            ? _customDimensionAnnotations.Count > 0
            : selectedCount > 0;

        if (_isAddingCustomDimension)
        {
            CustomDimensionModeTextBlock.Text = _pendingCustomDimensionStartAnchor == null
                ? $"Custom dim mode ({_customDimensionSnapMode} snap): click first point."
                : $"Custom dim mode ({_customDimensionSnapMode} snap): click second point.";
            return;
        }

        CustomDimensionModeTextBlock.Text = selected == null
            ? $"Custom dimensions total: {_customDimensionAnnotations.Count}"
            : $"Custom dimensions on selected: {selectedCount}";
    }

    private bool ShouldIgnoreHitVisual(Viewport3DHelper.HitResult hit)
    {
        if (ReferenceEquals(hit.Visual, _pdfUnderlayVisual3D))
            return true;

        return hit.Visual is Visual3D visual && _customDimensionPreviewVisualSet.Contains(visual);
    }

    private List<Viewport3DHelper.HitResult>? GetSceneHits(Point screenPoint)
    {
        return Viewport3DHelper.FindHits(Viewport.Viewport, screenPoint)?
            .Where(hit => !ShouldIgnoreHitVisual(hit))
            .ToList();
    }

    private static Color GetCustomDimensionSnapPreviewColor(CustomDimensionSnapMode mode)
    {
        return mode switch
        {
            CustomDimensionSnapMode.Point => Colors.DarkOrange,
            CustomDimensionSnapMode.Edge => Colors.ForestGreen,
            CustomDimensionSnapMode.Face => Colors.SteelBlue,
            CustomDimensionSnapMode.Center => Colors.MediumPurple,
            CustomDimensionSnapMode.Intersection => Colors.Crimson,
            _ => Colors.DimGray
        };
    }

    private void TrackCustomDimensionPreviewVisual(Visual3D visual)
    {
        _customDimensionPreviewVisuals.Add(visual);
        _customDimensionPreviewVisualSet.Add(visual);
    }

    private void ClearCustomDimensionPreview()
    {
        if (_customDimensionPreviewVisuals.Count == 0)
            return;

        foreach (var visual in _customDimensionPreviewVisuals)
            Viewport.Children.Remove(visual);
        _customDimensionPreviewVisuals.Clear();
        _customDimensionPreviewVisualSet.Clear();
    }

    private void ShowCustomDimensionPreview(CustomDimensionAnchor anchor, CustomDimensionSnapMode mode, bool secondPointPending)
    {
        ClearCustomDimensionPreview();

        var previewColor = GetCustomDimensionSnapPreviewColor(mode);
        var markerRadius = Math.Max(0.01, EstimateWorldUnitsPerPixel(anchor.WorldPoint) * 5.0);
        var marker = new SphereVisual3D
        {
            Center = anchor.WorldPoint,
            Radius = markerRadius,
            Fill = new SolidColorBrush(previewColor),
            ThetaDiv = 18,
            PhiDiv = 12
        };
        Viewport.Children.Add(marker);
        TrackCustomDimensionPreviewVisual(marker);

        var label = new BillboardTextVisual3D
        {
            Position = new Point3D(anchor.WorldPoint.X, anchor.WorldPoint.Y + markerRadius * 2.0, anchor.WorldPoint.Z),
            Text = $"{mode} snap",
            Foreground = Brushes.Black,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.6),
            FontSize = 12,
            Padding = new Thickness(3, 1, 3, 1)
        };
        Viewport.Children.Add(label);
        TrackCustomDimensionPreviewVisual(label);

        if (!secondPointPending ||
            _pendingCustomDimensionStartAnchor == null ||
            !TryResolveCustomDimensionAnchorWorldPoint(_pendingCustomDimensionStartAnchor, out var startWorld))
        {
            return;
        }

        var guideLine = new LinesVisual3D
        {
            Color = previewColor,
            Thickness = 1.6
        };
        guideLine.Points.Add(startWorld);
        guideLine.Points.Add(anchor.WorldPoint);
        Viewport.Children.Add(guideLine);
        TrackCustomDimensionPreviewVisual(guideLine);

        var startMarker = new SphereVisual3D
        {
            Center = startWorld,
            Radius = markerRadius * 0.8,
            Fill = Brushes.White,
            ThetaDiv = 16,
            PhiDiv = 10
        };
        Viewport.Children.Add(startMarker);
        TrackCustomDimensionPreviewVisual(startMarker);
    }

    private void UpdateCustomDimensionPreview(Point screenPoint)
    {
        if (!_isAddingCustomDimension)
        {
            _customDimensionPlacementState.LastPreviewSnap = null;
            ClearCustomDimensionPreview();
            return;
        }

        var hits = GetSceneHits(screenPoint);
        if (hits == null || hits.Count == 0 ||
            !TryGetSnappedCustomDimensionAnchor(screenPoint, hits, out var snappedAnchor, out var snappedMode))
        {
            _customDimensionPlacementState.LastPreviewSnap = null;
            ClearCustomDimensionPreview();
            return;
        }

        ShowCustomDimensionPreview(snappedAnchor, snappedMode, _pendingCustomDimensionStartAnchor != null);
    }

    private static Point3D ClosestPointOnSegment(Point3D a, Point3D b, Point3D point)
    {
        var segment = b - a;
        var lengthSquared = segment.LengthSquared;
        if (lengthSquared < 1e-8)
            return a;

        var t = Vector3D.DotProduct(point - a, segment) / lengthSquared;
        t = Math.Max(0.0, Math.Min(1.0, t));
        return a + segment * t;
    }

    private double ComputeScreenSnapDistance(Point screenPoint, Point3D worldPoint)
    {
        var projected = Viewport3DHelper.Point3DtoPoint2D(Viewport.Viewport, worldPoint);
        if (double.IsNaN(projected.X) || double.IsNaN(projected.Y) ||
            double.IsInfinity(projected.X) || double.IsInfinity(projected.Y))
        {
            return double.MaxValue;
        }

        return (projected - screenPoint).Length;
    }
}
