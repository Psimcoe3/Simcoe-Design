using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private static bool IsAdditiveSelectionGesture(ModifierKeys modifiers)
        => (modifiers & ModifierKeys.Control) == ModifierKeys.Control;

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var hits = GetSceneHits(position);

        if (_pendingPlacementComponent != null)
        {
            var hitPoint = hits?.FirstOrDefault()?.Position ?? new Point3D(0, 0, 0);
            if (TryPlacePendingComponentAtWorld(hitPoint))
            {
                e.Handled = true;
                return;
            }
        }

        if (_isAddingCustomDimension)
        {
            if (hits == null || hits.Count == 0 ||
                !TryGetSnappedCustomDimensionAnchor(position, hits, out var snappedAnchor, out var snappedMode))
            {
                _customDimensionPlacementState.LastPreviewSnap = null;
                ClearCustomDimensionPreview();
                UpdateCustomDimensionUiState();
                e.Handled = true;
                return;
            }

            if (_pendingCustomDimensionStartAnchor == null)
            {
                _pendingCustomDimensionStartAnchor = snappedAnchor;
                _customDimensionPlacementState.FirstReference = _customDimensionPlacementState.LastPreviewSnap;
                _customDimensionPlacementState.SecondReference = null;
                ClearCustomDimensionPreview();
                var componentName = string.IsNullOrEmpty(snappedAnchor.ComponentId)
                    ? "(world)"
                    : _viewModel.Components.FirstOrDefault(c => string.Equals(c.Id, snappedAnchor.ComponentId, StringComparison.Ordinal))?.Name ?? "(unknown)";
                ActionLogService.Instance.Log(LogCategory.Edit, "Custom dimension first point",
                    $"Snap: {snappedMode}, Component: {componentName}, Point: ({snappedAnchor.WorldPoint.X:F2}, {snappedAnchor.WorldPoint.Y:F2}, {snappedAnchor.WorldPoint.Z:F2})");
                UpdateCustomDimensionUiState();
                e.Handled = true;
                return;
            }

            if (!TryResolveCustomDimensionAnchorWorldPoint(_pendingCustomDimensionStartAnchor, out var startWorld))
            {
                _pendingCustomDimensionStartAnchor = null;
                _customDimensionPlacementState.Reset();
                ClearCustomDimensionPreview();
                UpdateCustomDimensionUiState();
                e.Handled = true;
                return;
            }

            if (!CanDimensionPair(_pendingCustomDimensionStartAnchor, snappedAnchor))
            {
                ActionLogService.Instance.Log(LogCategory.Edit, "Custom dimension pair rejected",
                    $"Reason: duplicate/unstable pair, Snap: {snappedMode}");
                e.Handled = true;
                return;
            }

            if (TryResolveCustomDimensionAnchorWorldPoint(snappedAnchor, out var endWorld))
            {
                var worldDelta = endWorld - startWorld;
                if (worldDelta.Length >= InViewDimensionMinSpan)
                {
                    var axis = GetDominantAxis(worldDelta);
                    _customDimensionAnnotations.Add(new CustomDimensionAnnotation
                    {
                        Start = _pendingCustomDimensionStartAnchor,
                        End = snappedAnchor,
                        Axis = axis
                    });
                    _customDimensionPlacementState.SecondReference = _customDimensionPlacementState.LastPreviewSnap;
                    ActionLogService.Instance.Log(LogCategory.Edit, "Custom dimension added",
                        $"Snap: {snappedMode}, Axis: {axis}, Length: {worldDelta.Length:F3}");
                    UpdateViewport();
                }
            }

            _pendingCustomDimensionStartAnchor = null;
            _customDimensionPlacementState.Reset();
            ClearCustomDimensionPreview();
            UpdateCustomDimensionUiState();
            e.Handled = true;
            return;
        }

        if (!_isEditingConduitPath && _viewModel.SelectedComponent != null)
        {
            var draggedAxisVisual = hits?
                .Select(hit => hit.Visual)
                .OfType<Visual3D>()
                .FirstOrDefault(v => _dimensionVisualAxisMap.ContainsKey(v));
            if (draggedAxisVisual != null)
            {
                _isDraggingDimensionAnnotation = true;
                _draggingDimensionAxis = _dimensionVisualAxisMap[draggedAxisVisual];
                _dimensionDragStartMousePosition = position;
                _dimensionDragStartOffsetFeet = GetDimensionAxisOffset(_viewModel.SelectedComponent, _draggingDimensionAxis);
                Mouse.Capture(Viewport);
                Viewport.MouseMove += Viewport_MouseMove;
                Viewport.MouseLeftButtonUp += Viewport_MouseLeftButtonUp;
                e.Handled = true;
                return;
            }
        }

        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent conduit)
        {
            EnsureConduitHasEditableEndPoint(conduit);

            var handleHit = hits?
                .Select(hit => hit.Visual)
                .OfType<ModelVisual3D>()
                .FirstOrDefault(v => _bendPointHandles.Contains(v));

            if (handleHit != null)
            {
                _draggedHandle = handleHit;
                _lastMousePosition = position;
                Mouse.Capture(Viewport);
                Viewport.MouseMove += Viewport_MouseMove;
                Viewport.MouseLeftButtonUp += Viewport_MouseLeftButtonUp;
                e.Handled = true;
                return;
            }

            var rayHit = hits?.FirstOrDefault();
            if (rayHit != null)
            {
                var hitPoint = ClampWorldToPlanBounds(rayHit.Position);
                var offset = hitPoint - _viewModel.SelectedComponent.Position;
                var localPoint = new Point3D(offset.X, offset.Y, offset.Z);

                conduit.BendPoints.Add(localPoint);
                if (!TryValidateConduitMinimumSegmentSpacing(conduit, out var minimumSpacingFeet, out var shortestSegmentFeet, out var shortestSegmentIndex))
                {
                    conduit.BendPoints.RemoveAt(conduit.BendPoints.Count - 1);
                    ActionLogService.Instance.Log(LogCategory.Edit, "Bend point rejected (minimum spacing)",
                        $"Conduit: {conduit.Name}, SegmentIndex: {shortestSegmentIndex}, Shortest: {shortestSegmentFeet:F3} ft, Minimum: {minimumSpacingFeet:F3} ft");
                    e.Handled = true;
                    return;
                }

                ConstrainConduitPathToPlanBounds(conduit);
                ActionLogService.Instance.Log(LogCategory.Edit, "Bend point added",
                    $"Conduit: {conduit.Name}, Point: ({localPoint.X:F2}, {localPoint.Y:F2}, {localPoint.Z:F2}), Total: {conduit.BendPoints.Count}");
                UpdateViewport();
                Update2DCanvas();
                ShowBendPointHandles();
                e.Handled = true;
                return;
            }
        }

        var matchedComponent = hits?
            .Select(hit => hit.Visual)
            .OfType<ModelVisual3D>()
            .Where(visual => _visualToComponentMap.ContainsKey(visual))
            .Select(visual => _visualToComponentMap[visual])
            .FirstOrDefault();

        var isAdditiveSelection = IsAdditiveSelectionGesture(Keyboard.Modifiers);

        if (matchedComponent != null)
        {
            ClearMarkupSelection();

            if (isAdditiveSelection)
                _viewModel.ToggleComponentSelection(matchedComponent);
            else
                _viewModel.SelectSingleComponent(matchedComponent);
        }
        else if (!isAdditiveSelection)
        {
            _viewModel.ClearComponentSelection();
        }

        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent selectedConduit)
        {
            EnsureConduitHasEditableEndPoint(selectedConduit);
            ShowBendPointHandles();
            Update2DCanvas();
        }
        else if (_isMobileView && _viewModel.SelectedComponent != null)
        {
            SetMobilePane(MobilePane.Properties);
        }
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingDimensionAnnotation)
        {
            var selected = _viewModel.SelectedComponent;
            if (selected != null && TryGetSelectedComponentWorldBounds(out var selectedBounds))
            {
                var currentPosition = e.GetPosition(Viewport);
                var delta = currentPosition - _dimensionDragStartMousePosition;
                var referencePoint = new Point3D(
                    selectedBounds.X + selectedBounds.SizeX / 2.0,
                    selectedBounds.Y + selectedBounds.SizeY / 2.0,
                    selectedBounds.Z + selectedBounds.SizeZ / 2.0);
                var worldPerPixel = EstimateWorldUnitsPerPixel(referencePoint);
                var updatedOffset = _dimensionDragStartOffsetFeet + (-delta.Y * worldPerPixel);
                SetDimensionAxisOffset(selected, _draggingDimensionAxis, updatedOffset);
                UpdateViewport();
                e.Handled = true;
            }

            return;
        }

        if (_isAddingCustomDimension)
        {
            UpdateCustomDimensionPreview(e.GetPosition(Viewport));
            return;
        }

        if (_draggedHandle == null || _viewModel.SelectedComponent is not ConduitComponent conduit)
            return;

        var position = e.GetPosition(Viewport);
        int handleIndex = _bendPointHandles.IndexOf(_draggedHandle);
        if (handleIndex >= 0 && handleIndex < conduit.BendPoints.Count)
        {
            var filteredHits = GetSceneHits(position);
            if (filteredHits != null && filteredHits.Any())
            {
                var hitPoint = ClampWorldToPlanBounds(filteredHits.First().Position);
                var offset = hitPoint - _viewModel.SelectedComponent.Position;
                var newPoint = new Point3D(offset.X, offset.Y, offset.Z);

                if (_viewModel.SnapToGrid)
                {
                    newPoint.X = Math.Round(newPoint.X / _viewModel.GridSize) * _viewModel.GridSize;
                    newPoint.Y = Math.Round(newPoint.Y / _viewModel.GridSize) * _viewModel.GridSize;
                    newPoint.Z = Math.Round(newPoint.Z / _viewModel.GridSize) * _viewModel.GridSize;
                }

                var originalPoint = conduit.BendPoints[handleIndex];
                conduit.BendPoints[handleIndex] = newPoint;

                if (!TryValidateConduitMinimumSegmentSpacing(conduit, out _, out _, out _))
                {
                    conduit.BendPoints[handleIndex] = originalPoint;
                    return;
                }

                ConstrainConduitPathToPlanBounds(conduit);
                UpdateViewport();
                Update2DCanvas();
                UpdatePropertiesPanel();
                ShowBendPointHandles();
            }
        }

        _lastMousePosition = position;
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingDimensionAnnotation = false;
        _draggingDimensionAxis = '\0';
        _dimensionDragStartOffsetFeet = 0.0;
        _draggedHandle = null;
        Mouse.Capture(null);
        Viewport.MouseMove -= Viewport_MouseMove;
        Viewport.MouseLeftButtonUp -= Viewport_MouseLeftButtonUp;
    }

    private void ClearBendPoints_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit)
        {
            if (MessageBox.Show("Clear all bend points from this conduit?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ActionLogService.Instance.Log(LogCategory.Edit, "Bend points cleared",
                    $"Conduit: {conduit.Name}, Points removed: {conduit.BendPoints.Count}");
                conduit.BendPoints.Clear();
                if (conduit.Length <= 0)
                    conduit.Length = 10.0;
                ApplyImperialDefaultsToConduit(conduit);
                UpdateViewport();
                UpdatePropertiesPanel();

                if (_isEditingConduitPath)
                    ShowBendPointHandles();
            }
        }
    }

    private void DeleteLastBendPoint_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit && conduit.BendPoints.Count > 0)
        {
            var removed = conduit.BendPoints[conduit.BendPoints.Count - 1];
            ActionLogService.Instance.Log(LogCategory.Edit, "Last bend point deleted",
                $"Conduit: {conduit.Name}, Removed: ({removed.X:F2}, {removed.Y:F2}, {removed.Z:F2}), Remaining: {conduit.BendPoints.Count - 1}");
            conduit.BendPoints.RemoveAt(conduit.BendPoints.Count - 1);
            UpdateConduitLengthFromPath(conduit);
            UpdateViewport();
            UpdatePropertiesPanel();

            if (_isEditingConduitPath)
                ShowBendPointHandles();
        }
        else
        {
            MessageBox.Show("No bend points to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ToggleEditConduitPath_Click(object sender, RoutedEventArgs e)
    {
        CancelPendingPlacement();
        ExitSketchModes();
        _isEditingConduitPath = !_isEditingConduitPath;
        ActionLogService.Instance.Log(LogCategory.Edit, "Edit conduit path toggled",
            $"Active: {_isEditingConduitPath}");

        if (_isEditingConduitPath)
        {
            EditConduitPathButton.Background = new SolidColorBrush(EditModeButtonColor);
            EditConduitPathButton.Content = "Exit Edit Mode";

            if (_viewModel.SelectedComponent is ConduitComponent conduit)
            {
                EnsureConduitHasEditableEndPoint(conduit);
                UpdateConduitLengthFromPath(conduit);
                ShowBendPointHandles();
                Update2DCanvas();
                MessageBox.Show("Edit Mode Active:\n" +
                    "• Click on conduit to add bend points\n" +
                    "• Drag orange handles to move bend points\n" +
                    "• In 2D: click conduit segments to add bend points\n" +
                    "• Use 'Clear All Bend Points' to reset\n" +
                    "• Click 'Exit Edit Mode' when done",
                    "Edit Conduit Path", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _isEditingConduitPath = false;
                EditConduitPathButton.Background = SystemColors.ControlBrush;
                EditConduitPathButton.Content = "Edit Conduit Path";
                MessageBox.Show("Please select a conduit component first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            EditConduitPathButton.Background = SystemColors.ControlBrush;
            EditConduitPathButton.Content = "Edit Conduit Path";
            HideBendPointHandles();
            _isDraggingConduitBend2D = false;
            _draggingConduit2D = null;
            _draggingConduitBendIndex2D = -1;
            _conduitBendHandleToIndexMap.Clear();
            PlanCanvas.ReleaseMouseCapture();
            Update2DCanvas();

            if (_draggedHandle != null)
            {
                _draggedHandle = null;
                Mouse.Capture(null);
                Viewport.MouseMove -= Viewport_MouseMove;
                Viewport.MouseLeftButtonUp -= Viewport_MouseLeftButtonUp;
            }
        }
    }

    private void ShowBendPointHandles()
    {
        HideBendPointHandles();

        if (_viewModel.SelectedComponent is not ConduitComponent conduit)
            return;

        EnsureConduitHasEditableEndPoint(conduit);
        UpdateConduitLengthFromPath(conduit);

        var pathPoints = conduit.GetPathPoints();
        for (int index = 1; index < pathPoints.Count; index++)
        {
            var handle = CreateBendPointHandle(pathPoints[index]);
            _bendPointHandles.Add(handle);
            Viewport.Children.Add(handle);
        }
    }

    private void HideBendPointHandles()
    {
        foreach (var handle in _bendPointHandles)
            Viewport.Children.Remove(handle);

        _bendPointHandles.Clear();
    }

    private ModelVisual3D CreateBendPointHandle(Point3D position)
    {
        var visual = new ModelVisual3D();
        var builder = new MeshBuilder();
        builder.AddSphere(new Point3D(0, 0, 0), BendPointHandleRadius, 12, 12);

        var material = new DiffuseMaterial(new SolidColorBrush(BendPointHandleColor));
        var emissive = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(100, 255, 165, 0)));
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(material);
        materialGroup.Children.Add(emissive);

        var model = new GeometryModel3D(builder.ToMesh(), materialGroup);

        if (_viewModel.SelectedComponent != null)
        {
            var transformGroup = new Transform3DGroup();
            var globalPos = _viewModel.SelectedComponent.Position + new Vector3D(position.X, position.Y, position.Z);
            transformGroup.Children.Add(new TranslateTransform3D(globalPos.X, globalPos.Y, globalPos.Z));
            model.Transform = transformGroup;
        }

        visual.Content = model;
        return visual;
    }
}