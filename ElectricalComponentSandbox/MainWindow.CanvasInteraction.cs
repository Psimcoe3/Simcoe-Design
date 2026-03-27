using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly ShadowGeometryTree _canvasInteractionShadowTree = new();
    private CanvasInteractionController? _canvasInteractionController;

    private void InitializeCanvasInteractionController()
    {
        _canvasInteractionController = new CanvasInteractionController(
            SkiaBackground.DrawingContext,
            _viewModel.SnapService,
            _canvasInteractionShadowTree);

        SyncCanvasInteractionControllerFromViewModel();

        _canvasInteractionController.SelectionRectCompleted += OnCanvasInteractionSelectionRectCompleted;
        _canvasInteractionController.CursorMoved += () =>
        {
            SkiaBackground.RequestRedraw();
            if (_canvasInteractionController != null)
            {
                var docPt = _canvasInteractionController.CursorDocPoint;
                UpdateCoordinateDisplay(docPt.X, docPt.Y);
            }
        };
    }

    private void SyncCanvasInteractionControllerFromViewModel()
    {
        if (_canvasInteractionController == null)
            return;

        _canvasInteractionController.IsOrthoActive = _viewModel.IsOrthoActive;
        _canvasInteractionController.IsPolarActive = _viewModel.IsPolarActive;
        _canvasInteractionController.PolarIncrementDeg = _viewModel.PolarIncrementDeg;
    }

    private void SyncViewModelInteractionStateFromCanvasInteraction()
    {
        if (_canvasInteractionController == null)
            return;

        _viewModel.IsOrthoActive = _canvasInteractionController.IsOrthoActive;
        _viewModel.IsPolarActive = _canvasInteractionController.IsPolarActive;
        _viewModel.PolarIncrementDeg = _canvasInteractionController.PolarIncrementDeg;
    }

    private void SyncCanvasInteractionContextFromViewport()
    {
        var drawingContext = SkiaBackground.DrawingContext;
        drawingContext.Zoom = PlanCanvasScale.ScaleX;
        drawingContext.PanX = -PlanScrollViewer.HorizontalOffset;
        drawingContext.PanY = -PlanScrollViewer.VerticalOffset;
        drawingContext.SyncCoordTransform();
        SyncPdfCalibrationState();
    }

    private void ApplyCanvasInteractionContextToViewport()
    {
        var drawingContext = SkiaBackground.DrawingContext;
        var scale = Math.Clamp(drawingContext.Zoom, 0.1, 10.0);
        PlanCanvasScale.ScaleX = scale;
        PlanCanvasScale.ScaleY = scale;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            PlanScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, -drawingContext.PanX));
            PlanScrollViewer.ScrollToVerticalOffset(Math.Max(0.0, -drawingContext.PanY));
            SyncCanvasInteractionContextFromViewport();
            SkiaBackground.RequestRedraw();
        }));
    }

    private void RebuildCanvasInteractionShadowTree(IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        _canvasInteractionShadowTree.Clear();

        foreach (var component in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibilityById, component.LayerId))
                continue;

            var center = WorldToCanvas(component.Position);
            var componentRect = new Rect(center.X - 10, center.Y - 10, 20, 20);
            _canvasInteractionShadowTree.AddOrUpdateNode(
                component.Id,
                ShadowGeometryTree.ShadowNodeKind.Component,
                componentRect,
                source: component);
        }

        foreach (var markup in _viewModel.Markups)
        {
            if (!IsLayerVisible(layerVisibilityById, markup.LayerId))
                continue;

            _canvasInteractionShadowTree.AddOrUpdate(markup);
        }
    }

    private void OnCanvasInteractionSelectionRectCompleted(IReadOnlyList<string> ids, bool crossing)
    {
        _viewModel.SelectedComponentIds.Clear();
        ElectricalComponent? firstSelected = null;
        MarkupRecord? firstMarkup = null;

        foreach (var id in ids)
        {
            var component = _viewModel.Components.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
            if (component != null)
            {
                _viewModel.SelectedComponentIds.Add(id);
                firstSelected ??= component;
                continue;
            }

            firstMarkup ??= _viewModel.Markups.FirstOrDefault(markup => string.Equals(markup.Id, id, StringComparison.Ordinal));
        }

        if (firstSelected != null)
        {
            ClearMarkupSelection();
            _viewModel.SelectedComponent = firstSelected;
        }
        else if (firstMarkup != null)
        {
            _viewModel.SelectedComponent = null;
            _viewModel.MarkupTool.SelectedMarkup = firstMarkup;
        }
        else
        {
            _viewModel.SelectedComponent = null;
            ClearMarkupSelection();
        }

        ActionLogService.Instance.Log(LogCategory.Selection, "2D selection rectangle",
            $"Mode: {(crossing ? "Crossing" : "Window")}, Hits: {ids.Count}");

        Update2DCanvas();
    }

    private void PlanScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncCanvasInteractionContextFromViewport();
        SkiaBackground.RequestRedraw();
    }

    private void ClearSketchSelection()
    {
        _selectedSketchPrimitive = null;
        Update2DCanvas();
    }

    private void DrawConduit_Click(object sender, RoutedEventArgs e)
    {
        CancelPendingPlacement();
        ExitSketchModes();

        if (_isFreehandDrawing)
            FinishFreehandConduit();

        if (_isDrawingConduit)
        {
            FinishDrawingConduit();
            return;
        }

        if (_isEditingConduitPath)
            ToggleEditConduitPath_Click(sender, e);

        _isDrawingConduit = true;
        _drawingCanvasPoints.Clear();
        _drawingConduit = null;

        DrawConduitButton.Background = new SolidColorBrush(EditModeButtonColor);
        DrawConduitButton.Content = "Finish Conduit";
        UpdatePlanCanvasCursor();

        ActionLogService.Instance.Log(LogCategory.Edit, "Draw conduit tool activated");
    }

    private void FinishDrawingConduit()
    {
        if (_drawingCanvasPoints.Count >= 2)
        {
            var conduit = new ConduitComponent
            {
                VisualProfile = ElectricalComponentCatalog.Profiles.ConduitEmt
            };

            if (_viewModel.ActiveLayer != null)
                conduit.LayerId = _viewModel.ActiveLayer.Id;

            var firstPt = _drawingCanvasPoints[0];
            conduit.Position = ClampWorldToPlanBounds(CanvasToWorld(firstPt));

            for (int i = 1; i < _drawingCanvasPoints.Count; i++)
            {
                var worldPt = ClampWorldToPlanBounds(CanvasToWorld(_drawingCanvasPoints[i]));
                var relative = new Point3D(
                    worldPt.X - conduit.Position.X,
                    worldPt.Y - conduit.Position.Y,
                    worldPt.Z - conduit.Position.Z);
                conduit.BendPoints.Add(relative);
            }

            ConstrainConduitPathToPlanBounds(conduit);
            var totalLen = conduit.Length;

            _viewModel.Components.Add(conduit);
            _viewModel.SelectedComponent = conduit;

            ActionLogService.Instance.Log(LogCategory.Component, "Conduit drawn",
                $"Vertices: {_drawingCanvasPoints.Count}, Length: {totalLen:F2}, Id: {conduit.Id}");
        }
        else if (_drawingCanvasPoints.Count > 0)
        {
            ActionLogService.Instance.Log(LogCategory.Edit, "Draw conduit cancelled", "Not enough vertices (need ≥ 2)");
        }

        _isDrawingConduit = false;
        _drawingCanvasPoints.Clear();
        _drawingConduit = null;
        RemoveRubberBand();
        RemoveSnapIndicator();

        DrawConduitButton.Background = System.Windows.SystemColors.ControlBrush;
        DrawConduitButton.Content = "Draw Conduit";
        UpdatePlanCanvasCursor();

        UpdateViewport();
        Update2DCanvas();
    }

    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);

        if (_isPdfCalibrationMode)
        {
            e.Handled = HandlePdfCalibrationCanvasClick(pos);
            return;
        }

        if (TryPlacePendingComponentOnCanvas(pos))
        {
            e.Handled = true;
            return;
        }

        if (_isFreehandDrawing && e.ClickCount == 2 && HandleFreehandDoubleClick())
        {
            e.Handled = true;
            return;
        }

        if (HandleFreehandMouseDown(pos))
        {
            e.Handled = true;
            return;
        }

        if (_isSketchLineMode)
        {
            var snapped = ApplyDrawingSnap(pos);
            if (_sketchDraftLinePoints.Count > 0 && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                snapped = ConstrainToAngle(_sketchDraftLinePoints[^1], snapped);

            if (e.ClickCount == 2)
            {
                if (_sketchDraftLinePoints.Count >= 1)
                {
                    if ((_sketchDraftLinePoints[^1] - snapped).Length > 1)
                        _sketchDraftLinePoints.Add(snapped);

                    if (_sketchDraftLinePoints.Count >= 2)
                    {
                        _sketchPrimitives.Add(new SketchLinePrimitive(Guid.NewGuid().ToString(), _sketchDraftLinePoints.ToList()));
                        _selectedSketchPrimitive = _sketchPrimitives[^1];
                    }
                }

                _sketchDraftLinePoints.Clear();
                RemoveSketchLineRubberBand();
                Update2DCanvas();
                e.Handled = true;
                return;
            }

            _sketchDraftLinePoints.Add(snapped);
            Update2DCanvas();
            e.Handled = true;
            return;
        }

        if (_isSketchRectangleMode)
        {
            _isSketchRectangleDragging = true;
            _sketchRectangleStartPoint = ApplyDrawingSnap(pos);
            _lastMousePosition = _sketchRectangleStartPoint;
            PlanCanvas.CaptureMouse();
            Update2DCanvas();
            e.Handled = true;
            return;
        }

        if (_isDrawingConduit)
        {
            if (e.ClickCount == 2)
            {
                FinishDrawingConduit();
                e.Handled = true;
                return;
            }

            var snapped = ApplyDrawingSnap(pos);

            if (_drawingCanvasPoints.Count > 0 &&
                (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                snapped = ConstrainToAngle(_drawingCanvasPoints[^1], snapped);
            }

            snapped = ClampCanvasToPlanBounds(snapped);
            if (_drawingCanvasPoints.Count > 0 && (_drawingCanvasPoints[^1] - snapped).Length < 0.5)
            {
                e.Handled = true;
                return;
            }

            _drawingCanvasPoints.Add(snapped);
            ActionLogService.Instance.Log(LogCategory.Edit, "Conduit vertex placed",
                $"Vertex #{_drawingCanvasPoints.Count}, Canvas: ({snapped.X:F0}, {snapped.Y:F0})");

            Update2DCanvas();
            DrawConduitPreview();
            e.Handled = true;
            return;
        }

        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent selectedConduit)
        {
            EnsureConduitHasEditableEndPoint(selectedConduit);

            if (TryStartDraggingConduitBendHandle2D(pos))
            {
                e.Handled = true;
                return;
            }

            if (TryInsertConduitBendPoint2D(selectedConduit, pos))
            {
                e.Handled = true;
                return;
            }
        }

        var hit = PlanCanvas.InputHitTest(pos) as FrameworkElement;
        if (hit != null && _canvasToComponentMap.ContainsKey(hit))
        {
            ClearMarkupSelection();
            if (_isEditingConduitPath &&
                _viewModel.SelectedComponent is ConduitComponent &&
                ReferenceEquals(_canvasToComponentMap[hit], _viewModel.SelectedComponent))
            {
                e.Handled = true;
                return;
            }

            _selectedSketchPrimitive = null;
            _viewModel.SelectedComponent = _canvasToComponentMap[hit];
            _isDragging2D = true;
            _draggedElement2D = hit;
            _lastMousePosition = pos;
            _dragStartCanvasPosition = pos;
            _mobileSelectionCandidate = _isMobileView;
            PlanCanvas.CaptureMouse();
            return;
        }

        if (hit != null && _canvasToSketchMap.TryGetValue(hit, out var hitSketch))
        {
            ClearMarkupSelection();
            _selectedSketchPrimitive = hitSketch;
            Update2DCanvas();
            e.Handled = true;
            return;
        }

        if (TryHitConduitPath2D(pos, out var hitConduit) && hitConduit != null)
        {
            ClearMarkupSelection();
            _selectedSketchPrimitive = null;
            _viewModel.SelectedComponent = hitConduit;

            if (_isEditingConduitPath)
            {
                EnsureConduitHasEditableEndPoint(hitConduit);
                Update2DCanvas();
                e.Handled = true;
                return;
            }

            _isDragging2D = true;
            _draggedElement2D = PlanCanvas;
            _lastMousePosition = pos;
            _dragStartCanvasPosition = pos;
            _mobileSelectionCandidate = _isMobileView;
            PlanCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (TryStartMarkupArcAngleDrag(pos))
        {
            e.Handled = true;
            return;
        }

        if (TryStartMarkupRadiusDrag(pos))
        {
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2 && TryEditStructuredMarkupText(pos))
        {
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2 && TryInsertMarkupVertex(pos))
        {
            e.Handled = true;
            return;
        }

        if (TryStartMarkupVertexDrag(pos))
        {
            e.Handled = true;
            return;
        }

        if (TryStartMarkupResizeDrag(pos))
        {
            e.Handled = true;
            return;
        }

        if (TryStartMarkupSelectionDrag(pos))
        {
            e.Handled = true;
            return;
        }

        PlanCanvas.CaptureMouse();
        SyncCanvasInteractionContextFromViewport();
        _canvasInteractionController?.OnMouseDown(e.GetPosition(PlanScrollViewer), MouseButton.Left, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);

        if (_isPdfCalibrationMode)
        {
            UpdatePdfCalibrationPreview(pos);
            e.Handled = true;
            return;
        }

        if (HandleFreehandMouseMove(pos))
        {
            e.Handled = true;
            return;
        }

        if (_pendingPlacementComponent != null)
        {
            _canvasInteractionController?.ClearPreview();
            var snapped = ClampCanvasToPlanBounds(ApplyDrawingSnap(pos));
            UpdateSnapIndicator(snapped, pos);
            e.Handled = true;
            return;
        }

        if (_isSketchLineMode && _sketchDraftLinePoints.Count > 0)
        {
            _canvasInteractionController?.ClearPreview();
            var snapped = ApplyDrawingSnap(pos);
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                snapped = ConstrainToAngle(_sketchDraftLinePoints[^1], snapped);

            UpdateSketchLineRubberBand(_sketchDraftLinePoints[^1], snapped);
            UpdateSnapIndicator(snapped, pos);
            return;
        }

        if (_isSketchRectangleMode && _isSketchRectangleDragging)
        {
            _canvasInteractionController?.ClearPreview();
            _lastMousePosition = ApplyDrawingSnap(pos);
            Update2DCanvas();
            return;
        }

        if (_isDrawingConduit && _drawingCanvasPoints.Count > 0)
        {
            _canvasInteractionController?.ClearPreview();
            var snapped = ClampCanvasToPlanBounds(ApplyDrawingSnap(pos));
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                snapped = ConstrainToAngle(_drawingCanvasPoints[^1], snapped);
            }
            snapped = ClampCanvasToPlanBounds(snapped);
            UpdateRubberBand(_drawingCanvasPoints[^1], snapped);
            UpdateSnapIndicator(snapped, pos);
            return;
        }

        if (_isDraggingConduitBend2D && _draggingConduit2D != null)
        {
            _canvasInteractionController?.ClearPreview();
            BeginFastInteractionMode();
            var snapped = ClampCanvasToPlanBounds(ApplyDrawingSnap(pos));
            var worldPoint = ClampWorldToPlanBounds(CanvasToWorld(snapped));
            var relativePoint = new Point3D(
                worldPoint.X - _draggingConduit2D.Position.X,
                0,
                worldPoint.Z - _draggingConduit2D.Position.Z);

            if (_viewModel.SnapToGrid)
            {
                relativePoint.X = Math.Round(relativePoint.X / _viewModel.GridSize) * _viewModel.GridSize;
                relativePoint.Z = Math.Round(relativePoint.Z / _viewModel.GridSize) * _viewModel.GridSize;
            }

            if (_draggingConduitBendIndex2D >= 0 && _draggingConduitBendIndex2D < _draggingConduit2D.BendPoints.Count)
            {
                var originalPoint = _draggingConduit2D.BendPoints[_draggingConduitBendIndex2D];
                _draggingConduit2D.BendPoints[_draggingConduitBendIndex2D] = relativePoint;

                if (!TryValidateConduitMinimumSegmentSpacing(_draggingConduit2D, out _, out _, out _))
                {
                    _draggingConduit2D.BendPoints[_draggingConduitBendIndex2D] = originalPoint;
                    return;
                }

                ConstrainConduitPathToPlanBounds(_draggingConduit2D);
                QueueSceneRefresh(update2D: true, update3D: true);
            }

            return;
        }

        if (_isDragging2D && _draggedElement2D != null && _viewModel.SelectedComponent != null)
        {
            _canvasInteractionController?.ClearPreview();
            BeginFastInteractionMode();
            var delta = pos - _lastMousePosition;
            if (_mobileSelectionCandidate && (Math.Abs(pos.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(pos.Y - _dragStartCanvasPosition.Y) > 4))
            {
                _mobileSelectionCandidate = false;
            }
            var worldDelta = new Vector3D(delta.X / 20.0, 0, -delta.Y / 20.0);

            var comp = _viewModel.SelectedComponent;
            var newPosition = comp.Position + worldDelta;
            if (_viewModel.SnapToGrid)
            {
                newPosition.X = Math.Round(newPosition.X / _viewModel.GridSize) * _viewModel.GridSize;
                newPosition.Y = Math.Round(newPosition.Y / _viewModel.GridSize) * _viewModel.GridSize;
                newPosition.Z = Math.Round(newPosition.Z / _viewModel.GridSize) * _viewModel.GridSize;
            }

            newPosition = comp is ConduitComponent draggedConduit
                ? ClampConduitPositionToPlanBounds(draggedConduit, newPosition)
                : ClampWorldToPlanBounds(newPosition);

            comp.Position = newPosition;

            _lastMousePosition = pos;
            QueueSceneRefresh(update2D: true);
        }
        else if (_isDraggingMarkupArcAngle)
        {
            _canvasInteractionController?.ClearPreview();
            UpdateDraggedMarkupArcAnglePreview(pos);
            e.Handled = true;
            return;
        }
        else if (_isDraggingMarkupRadius)
        {
            _canvasInteractionController?.ClearPreview();
            UpdateDraggedMarkupRadiusPreview(pos);
            e.Handled = true;
            return;
        }
        else if (_isDraggingMarkupVertex)
        {
            _canvasInteractionController?.ClearPreview();
            UpdateDraggedMarkupVertexPreview(pos);
            e.Handled = true;
            return;
        }
        else if (_isResizingMarkup)
        {
            _canvasInteractionController?.ClearPreview();
            UpdateMarkupResizePreview(pos);
            e.Handled = true;
            return;
        }
        else if (_isDraggingMarkup)
        {
            _canvasInteractionController?.ClearPreview();
            UpdateDraggedMarkupPreview(pos);
            e.Handled = true;
            return;
        }

        SyncCanvasInteractionContextFromViewport();
        _canvasInteractionController?.OnMouseMove(e.GetPosition(PlanScrollViewer), _snapEndpointsCache, _snapSegmentsCache);
        e.Handled = _canvasInteractionController?.IsRubberBanding == true;
    }

    private void PlanCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSketchRectangleMode && _isSketchRectangleDragging)
        {
            _isSketchRectangleDragging = false;
            var end = _lastMousePosition;
            var width = Math.Abs(end.X - _sketchRectangleStartPoint.X);
            var height = Math.Abs(end.Y - _sketchRectangleStartPoint.Y);
            if (width > 4 && height > 4)
            {
                _sketchPrimitives.Add(new SketchRectanglePrimitive(
                    Guid.NewGuid().ToString(),
                    _sketchRectangleStartPoint,
                    end));
                _selectedSketchPrimitive = _sketchPrimitives[^1];
            }
            PlanCanvas.ReleaseMouseCapture();
            Update2DCanvas();
            e.Handled = true;
            return;
        }

        if (_isDraggingConduitBend2D)
        {
            _isDraggingConduitBend2D = false;
            _draggingConduit2D = null;
            _draggingConduitBendIndex2D = -1;
            UpdatePropertiesPanel();
            PlanCanvas.ReleaseMouseCapture();
            return;
        }

        if (_isDragging2D)
        {
            UpdateViewport();
            if (_isMobileView && _mobileSelectionCandidate && _viewModel.SelectedComponent != null)
            {
                SetMobilePane(MobilePane.Properties);
            }
        }

        if (_isDraggingMarkup)
        {
            FinishMarkupSelectionDrag();
            _mobileSelectionCandidate = false;
            PlanCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_isDraggingMarkupArcAngle)
        {
            FinishMarkupArcAngleDrag();
            _mobileSelectionCandidate = false;
            PlanCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_isDraggingMarkupRadius)
        {
            FinishMarkupRadiusDrag();
            _mobileSelectionCandidate = false;
            PlanCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_isDraggingMarkupVertex)
        {
            FinishMarkupVertexDrag();
            _mobileSelectionCandidate = false;
            PlanCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_isResizingMarkup)
        {
            FinishMarkupResizeDrag();
            _mobileSelectionCandidate = false;
            PlanCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        _isDragging2D = false;
        _draggedElement2D = null;
        _mobileSelectionCandidate = false;
        PlanCanvas.ReleaseMouseCapture();

        if (_canvasInteractionController?.IsRubberBanding == true)
        {
            SyncCanvasInteractionContextFromViewport();
            _canvasInteractionController.OnMouseUp(e.GetPosition(PlanScrollViewer), MouseButton.Left);
            e.Handled = true;
        }
    }

    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        BeginFastInteractionMode();

        SyncCanvasInteractionContextFromViewport();
        _canvasInteractionController?.OnMouseWheel(e.GetPosition(PlanScrollViewer), e.Delta);
        ApplyCanvasInteractionContextToViewport();
        e.Handled = true;
    }

    private void PlanCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        if (!_isMobileView) return;

        e.ManipulationContainer = PlanScrollViewer;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
    }

    private void PlanCanvas_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (!_isMobileView) return;
        BeginFastInteractionMode();

        var deltaScale = e.DeltaManipulation.Scale;
        var scaleFactor = Math.Max(deltaScale.X, deltaScale.Y);
        if (scaleFactor > 0)
        {
            var newScale = PlanCanvasScale.ScaleX * scaleFactor;
            newScale = Math.Max(MobileMinCanvasScale, Math.Min(MobileMaxCanvasScale, newScale));
            PlanCanvasScale.ScaleX = newScale;
            PlanCanvasScale.ScaleY = newScale;
        }

        var translation = e.DeltaManipulation.Translation;
        PlanScrollViewer.ScrollToHorizontalOffset(PlanScrollViewer.HorizontalOffset - translation.X);
        PlanScrollViewer.ScrollToVerticalOffset(PlanScrollViewer.VerticalOffset - translation.Y);
        e.Handled = true;
    }

    /// <summary>
    /// Draws the in-progress conduit polyline (committed segments) on the canvas.
    /// Called after Update2DCanvas so it renders on top.
    /// </summary>
    private void DrawConduitPreview()
    {
        if (_drawingCanvasPoints.Count < 2) return;

        var polyline = new Polyline
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        foreach (var pt in _drawingCanvasPoints)
            polyline.Points.Add(pt);

        PlanCanvas.Children.Add(polyline);

        foreach (var pt in _drawingCanvasPoints)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.DodgerBlue,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, pt.X - 4);
            Canvas.SetTop(dot, pt.Y - 4);
            PlanCanvas.Children.Add(dot);
        }
    }

    private void UpdateRubberBand(Point from, Point to)
    {
        if (_rubberBandLine == null)
        {
            _rubberBandLine = new Line
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            PlanCanvas.Children.Add(_rubberBandLine);
        }

        _rubberBandLine.X1 = from.X;
        _rubberBandLine.Y1 = from.Y;
        _rubberBandLine.X2 = to.X;
        _rubberBandLine.Y2 = to.Y;
    }

    private void RemoveRubberBand()
    {
        if (_rubberBandLine != null)
        {
            PlanCanvas.Children.Remove(_rubberBandLine);
            _rubberBandLine = null;
        }
    }

    private void UpdateSnapIndicator(Point snappedPos, Point rawPos)
    {
        bool didSnap = Math.Abs(snappedPos.X - rawPos.X) > 0.5 || Math.Abs(snappedPos.Y - rawPos.Y) > 0.5;

        if (!didSnap)
        {
            RemoveSnapIndicator();
            return;
        }

        if (_snapIndicator == null)
        {
            _snapIndicator = new Ellipse
            {
                Width = 12,
                Height = 12,
                Stroke = Brushes.Lime,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)),
                IsHitTestVisible = false
            };
            PlanCanvas.Children.Add(_snapIndicator);
        }

        Canvas.SetLeft(_snapIndicator, snappedPos.X - 6);
        Canvas.SetTop(_snapIndicator, snappedPos.Y - 6);
    }

    private void RemoveSnapIndicator()
    {
        if (_snapIndicator != null)
        {
            PlanCanvas.Children.Remove(_snapIndicator);
            _snapIndicator = null;
        }
    }

    private Point ApplyDrawingSnap(Point canvasPos)
    {
        var endpoints = _drawingCanvasPoints.Count == 0
            ? _snapEndpointsCache
            : _snapEndpointsCache.Concat(_drawingCanvasPoints);

        var snapResult = _viewModel.SnapService.FindSnapPoint(canvasPos, endpoints, _snapSegmentsCache);
        if (snapResult.Snapped)
            return snapResult.SnappedPoint;

        if (_viewModel.SnapToGrid)
        {
            double gridPx = _viewModel.GridSize * 20;
            double snappedX = Math.Round(canvasPos.X / gridPx) * gridPx;
            double snappedY = Math.Round(canvasPos.Y / gridPx) * gridPx;
            return new Point(snappedX, snappedY);
        }

        return canvasPos;
    }

    private static Point ConstrainToAngle(Point anchor, Point target)
    {
        double dx = target.X - anchor.X;
        double dy = target.Y - anchor.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return target;

        double angle = Math.Atan2(dy, dx);
        double snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);

        return new Point(
            anchor.X + dist * Math.Cos(snapped),
            anchor.Y + dist * Math.Sin(snapped));
    }

    private static Point3D CanvasToWorld(Point canvasPos)
    {
        double worldX = (canvasPos.X - CanvasWorldOrigin) / CanvasWorldScale;
        double worldZ = (CanvasWorldOrigin - canvasPos.Y) / CanvasWorldScale;
        return new Point3D(worldX, 0, worldZ);
    }

    private static Point WorldToCanvas(Point3D worldPos)
    {
        return new Point(
            CanvasWorldOrigin + worldPos.X * CanvasWorldScale,
            CanvasWorldOrigin - worldPos.Z * CanvasWorldScale);
    }

    private void UpdateSketchLineRubberBand(Point from, Point to)
    {
        if (_sketchRubberBandLine == null)
        {
            _sketchRubberBandLine = new Line
            {
                Stroke = Brushes.MediumPurple,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            PlanCanvas.Children.Add(_sketchRubberBandLine);
        }

        _sketchRubberBandLine.X1 = from.X;
        _sketchRubberBandLine.Y1 = from.Y;
        _sketchRubberBandLine.X2 = to.X;
        _sketchRubberBandLine.Y2 = to.Y;
    }

    private void RemoveSketchLineRubberBand()
    {
        if (_sketchRubberBandLine != null)
        {
            PlanCanvas.Children.Remove(_sketchRubberBandLine);
            _sketchRubberBandLine = null;
        }
    }

    private void UpdatePlanCanvasCursor()
    {
        if (_isPdfCalibrationMode)
        {
            PlanCanvas.Cursor = Cursors.Cross;
            return;
        }

        if (_isFreehandDrawing)
        {
            PlanCanvas.Cursor = Cursors.Pen;
            return;
        }

        PlanCanvas.Cursor = (_isSketchLineMode || _isSketchRectangleMode || _isDrawingConduit || _pendingPlacementComponent != null)
            ? Cursors.Cross
            : Cursors.Arrow;
    }

    private void ExitConflictingAuthoringModes()
    {
        if (_isPdfCalibrationMode)
            CancelPdfCalibrationMode(logCancellation: false);

        if (_isSketchLineMode || _isSketchRectangleMode)
            ExitSketchModes();

        if (_isFreehandDrawing)
            FinishFreehandConduit();

        if (_isDrawingConduit)
            FinishDrawingConduit();

        if (_isEditingConduitPath)
            ToggleEditConduitPath_Click(this, new RoutedEventArgs());
    }

    private void BeginComponentPlacement(ElectricalComponent component, string source)
    {
        ExitConflictingAuthoringModes();
        CancelPendingPlacement(logCancellation: false);
        _pendingPlacementComponent = component;
        _pendingPlacementSource = source;
        RemoveSnapIndicator();
        UpdatePlanCanvasCursor();
        PostAddComponentMobileUX();

        ActionLogService.Instance.Log(LogCategory.Component, "Component placement armed",
            $"Name: {component.Name}, Type: {component.Type}, Source: {source}");
    }

    private void CancelPendingPlacement(bool logCancellation = true)
    {
        if (_pendingPlacementComponent == null)
            return;

        if (logCancellation)
        {
            ActionLogService.Instance.Log(LogCategory.Component, "Component placement cancelled",
                $"Name: {_pendingPlacementComponent.Name}, Type: {_pendingPlacementComponent.Type}");
        }

        _pendingPlacementComponent = null;
        _pendingPlacementSource = null;
        RemoveSnapIndicator();
        UpdatePlanCanvasCursor();
    }

    private bool TryPlacePendingComponentAtWorld(Point3D worldPosition)
    {
        if (_pendingPlacementComponent == null)
            return false;

        if (_viewModel.SnapToGrid)
        {
            worldPosition.X = Math.Round(worldPosition.X / _viewModel.GridSize) * _viewModel.GridSize;
            worldPosition.Z = Math.Round(worldPosition.Z / _viewModel.GridSize) * _viewModel.GridSize;
        }

        worldPosition.Y = 0;
        worldPosition = ClampWorldToPlanBounds(worldPosition);
        var component = _pendingPlacementComponent;
        component.Position = worldPosition;
        AddComponentWithUndo(component);

        ActionLogService.Instance.Log(LogCategory.Component, "Component placed",
            $"Name: {component.Name}, Type: {component.Type}, Source: {_pendingPlacementSource ?? "unknown"}, " +
            $"World: ({worldPosition.X:F2}, {worldPosition.Y:F2}, {worldPosition.Z:F2})");

        _pendingPlacementComponent = null;
        _pendingPlacementSource = null;
        RemoveSnapIndicator();
        UpdatePlanCanvasCursor();
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);

        if (_isMobileView)
            SetMobilePane(MobilePane.Properties);

        return true;
    }

    private bool TryPlacePendingComponentOnCanvas(Point canvasPosition)
    {
        if (_pendingPlacementComponent == null)
            return false;

        var snapped = ApplyDrawingSnap(canvasPosition);
        var world = CanvasToWorld(snapped);
        return TryPlacePendingComponentAtWorld(world);
    }

    private PlanWorldBounds GetPlanWorldBounds()
    {
        if (TryGetUnderlayCanvasFrame(out var frame))
        {
            var worldCorners = frame.Corners.Select(CanvasToWorld).ToList();
            return new PlanWorldBounds(
                worldCorners.Min(p => p.X),
                worldCorners.Max(p => p.X),
                worldCorners.Min(p => p.Z),
                worldCorners.Max(p => p.Z));
        }

        var canvasWidth = PlanCanvas?.Width > 0 ? PlanCanvas.Width : DefaultPlanCanvasSize;
        var canvasHeight = PlanCanvas?.Height > 0 ? PlanCanvas.Height : DefaultPlanCanvasSize;

        var topLeft = CanvasToWorld(new Point(0, 0));
        var bottomRight = CanvasToWorld(new Point(canvasWidth, canvasHeight));

        return new PlanWorldBounds(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Max(topLeft.X, bottomRight.X),
            Math.Min(bottomRight.Z, topLeft.Z),
            Math.Max(bottomRight.Z, topLeft.Z));
    }

    private Point ClampCanvasToPlanBounds(Point canvasPoint)
    {
        if (TryGetUnderlayCanvasFrame(out var frame))
        {
            var local = canvasPoint;
            local.Offset(-frame.Underlay.OffsetX, -frame.Underlay.OffsetY);
            local = RotateCanvasPoint(local, -frame.Underlay.RotationDegrees);

            var clampedLocal = new Point(
                Math.Clamp(local.X, 0, frame.ScaledWidth),
                Math.Clamp(local.Y, 0, frame.ScaledHeight));

            var constrainedCanvas = RotateCanvasPoint(clampedLocal, frame.Underlay.RotationDegrees);
            constrainedCanvas.Offset(frame.Underlay.OffsetX, frame.Underlay.OffsetY);
            return constrainedCanvas;
        }

        var canvasWidth = PlanCanvas?.Width > 0 ? PlanCanvas.Width : DefaultPlanCanvasSize;
        var canvasHeight = PlanCanvas?.Height > 0 ? PlanCanvas.Height : DefaultPlanCanvasSize;

        return new Point(
            Math.Clamp(canvasPoint.X, 0, canvasWidth),
            Math.Clamp(canvasPoint.Y, 0, canvasHeight));
    }

    private Point3D ClampWorldToPlanBounds(Point3D worldPosition)
    {
        var canvasPoint = WorldToCanvas(worldPosition);
        var constrainedCanvasPoint = ClampCanvasToPlanBounds(canvasPoint);
        var constrainedWorld = CanvasToWorld(constrainedCanvasPoint);
        constrainedWorld.Y = worldPosition.Y;
        return constrainedWorld;
    }

    private Point3D ClampConduitPositionToPlanBounds(ConduitComponent conduit, Point3D desiredPosition)
    {
        var bounds = GetPlanWorldBounds();
        var path = conduit.GetPathPoints();
        if (path.Count == 0)
            return ClampWorldToPlanBounds(desiredPosition);

        var minRelX = path.Min(p => p.X);
        var maxRelX = path.Max(p => p.X);
        var minRelZ = path.Min(p => p.Z);
        var maxRelZ = path.Max(p => p.Z);

        return new Point3D(
            Math.Clamp(desiredPosition.X, bounds.MinX - minRelX, bounds.MaxX - maxRelX),
            desiredPosition.Y,
            Math.Clamp(desiredPosition.Z, bounds.MinZ - minRelZ, bounds.MaxZ - maxRelZ));
    }

    private void ConstrainConduitPathToPlanBounds(ConduitComponent conduit)
    {
        if (conduit.BendPoints.Count == 0)
        {
            conduit.Position = ClampWorldToPlanBounds(conduit.Position);
            UpdateConduitLengthFromPath(conduit);
            return;
        }

        var absolutePath = conduit.GetPathPoints()
            .Select(p => new Point3D(
                conduit.Position.X + p.X,
                conduit.Position.Y + p.Y,
                conduit.Position.Z + p.Z))
            .Select(ClampWorldToPlanBounds)
            .ToList();

        if (absolutePath.Count == 0)
            return;

        var origin = absolutePath[0];
        conduit.Position = origin;
        conduit.BendPoints.Clear();

        for (int i = 1; i < absolutePath.Count; i++)
        {
            conduit.BendPoints.Add(new Point3D(
                absolutePath[i].X - origin.X,
                absolutePath[i].Y - origin.Y,
                absolutePath[i].Z - origin.Z));
        }

        UpdateConduitLengthFromPath(conduit);
    }

    private static List<Point> GetConduitCanvasPathPoints(ConduitComponent conduit)
    {
        var origin = WorldToCanvas(conduit.Position);
        return conduit.GetPathPoints()
            .Select(p => new Point(origin.X + p.X * CanvasWorldScale, origin.Y - p.Z * CanvasWorldScale))
            .ToList();
    }

    private static void EnsureConduitHasEditableEndPoint(ConduitComponent conduit)
    {
        if (conduit.BendPoints.Count == 0)
        {
            conduit.BendPoints.Add(new Point3D(0, 0, conduit.Length));
        }
    }

    private void UpdateConduitLengthFromPath(ConduitComponent conduit)
    {
        var points = conduit.GetPathPoints();
        double total = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            total += (points[i + 1] - points[i]).Length;
        }

        conduit.Length = total;
        ApplyImperialDefaultsToConduit(conduit);
    }

    private static double DistanceToSegment(Point point, Point a, Point b, out Point closestPoint, out double t)
    {
        var ab = b - a;
        var lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
        if (lengthSquared < 1e-9)
        {
            closestPoint = a;
            t = 0;
            return (point - a).Length;
        }

        t = ((point.X - a.X) * ab.X + (point.Y - a.Y) * ab.Y) / lengthSquared;
        t = Math.Max(0, Math.Min(1, t));
        closestPoint = new Point(a.X + t * ab.X, a.Y + t * ab.Y);
        return (point - closestPoint).Length;
    }

    private bool TryStartDraggingConduitBendHandle2D(Point canvasPos)
    {
        var hit = PlanCanvas.InputHitTest(canvasPos) as FrameworkElement;
        if (hit == null)
            return false;

        if (_viewModel.SelectedComponent is ConduitComponent conduit &&
            _conduitBendHandleToIndexMap.TryGetValue(hit, out var bendIndex))
        {
            _isDraggingConduitBend2D = true;
            _draggingConduit2D = conduit;
            _draggingConduitBendIndex2D = bendIndex;
            _lastMousePosition = canvasPos;
            PlanCanvas.CaptureMouse();
            return true;
        }

        return false;
    }

    private bool TryInsertConduitBendPoint2D(ConduitComponent conduit, Point canvasPos)
    {
        var pathPoints = GetConduitCanvasPathPoints(conduit);
        if (pathPoints.Count < 2)
            return false;

        int bestSegment = -1;
        double bestDistance = double.MaxValue;
        Point bestProjection = default;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            double distance = DistanceToSegment(canvasPos, pathPoints[i], pathPoints[i + 1], out var projection, out _);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSegment = i;
                bestProjection = projection;
            }
        }

        if (bestSegment < 0 || bestDistance > Conduit2DInsertThreshold)
            return false;

        EnsureConduitHasEditableEndPoint(conduit);

        var snappedCanvas = ClampCanvasToPlanBounds(ApplyDrawingSnap(bestProjection));
        var worldPoint = ClampWorldToPlanBounds(CanvasToWorld(snappedCanvas));
        var relativePoint = new Point3D(
            worldPoint.X - conduit.Position.X,
            0,
            worldPoint.Z - conduit.Position.Z);

        if (_viewModel.SnapToGrid)
        {
            relativePoint.X = Math.Round(relativePoint.X / _viewModel.GridSize) * _viewModel.GridSize;
            relativePoint.Z = Math.Round(relativePoint.Z / _viewModel.GridSize) * _viewModel.GridSize;
        }

        int insertIndex = Math.Clamp(bestSegment, 0, conduit.BendPoints.Count);
        conduit.BendPoints.Insert(insertIndex, relativePoint);

        if (!TryValidateConduitMinimumSegmentSpacing(conduit, out var minimumSpacingFeet, out var shortestSegmentFeet, out var shortestSegmentIndex))
        {
            conduit.BendPoints.RemoveAt(insertIndex);
            ActionLogService.Instance.Log(LogCategory.Edit, "2D bend point rejected (minimum spacing)",
                $"Conduit: {conduit.Name}, SegmentIndex: {shortestSegmentIndex}, Shortest: {shortestSegmentFeet:F3} ft, Minimum: {minimumSpacingFeet:F3} ft");
            return false;
        }

        ConstrainConduitPathToPlanBounds(conduit);

        ActionLogService.Instance.Log(LogCategory.Edit, "2D bend point inserted",
            $"Conduit: {conduit.Name}, Index: {insertIndex}, Point: ({relativePoint.X:F2}, {relativePoint.Z:F2})");

        _isDraggingConduitBend2D = true;
        _draggingConduit2D = conduit;
        _draggingConduitBendIndex2D = insertIndex;
        _lastMousePosition = canvasPos;
        PlanCanvas.CaptureMouse();

        UpdateViewport();
        Update2DCanvas();
        UpdatePropertiesPanel();
        return true;
    }

    private bool TryHitConduitPath2D(Point canvasPos, out ConduitComponent? hitConduit)
    {
        hitConduit = null;
        double bestDistance = Conduit2DHitThreshold;
        var layerVisibilityById = BuildLayerVisibilityLookup();

        foreach (var component in _viewModel.Components)
        {
            if (component is not ConduitComponent conduit)
                continue;

            if (!IsLayerVisible(layerVisibilityById, conduit.LayerId))
                continue;

            var points = GetConduitCanvasPathPoints(conduit);
            if (points.Count < 2)
                continue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                var distance = DistanceToSegment(canvasPos, points[i], points[i + 1], out _, out _);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    hitConduit = conduit;
                }
            }
        }

        return hitConduit != null;
    }

    private void DrawConduitEditHandles2D(ConduitComponent conduit)
    {
        _conduitBendHandleToIndexMap.Clear();

        var pathPoints = GetConduitCanvasPathPoints(conduit);
        if (pathPoints.Count < 2)
            return;

        // Mid-segment markers hint where a tap can insert a bend point.
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            var midpoint = new Point(
                (pathPoints[i].X + pathPoints[i + 1].X) / 2,
                (pathPoints[i].Y + pathPoints[i + 1].Y) / 2);

            var marker = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = Brushes.Orange,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(marker, midpoint.X - 2.5);
            Canvas.SetTop(marker, midpoint.Y - 2.5);
            PlanCanvas.Children.Add(marker);
        }

        for (int i = 1; i < pathPoints.Count; i++)
        {
            var handle = new Ellipse
            {
                Width = Conduit2DHandleRadius * 2,
                Height = Conduit2DHandleRadius * 2,
                Fill = Brushes.White,
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2
            };
            Canvas.SetLeft(handle, pathPoints[i].X - Conduit2DHandleRadius);
            Canvas.SetTop(handle, pathPoints[i].Y - Conduit2DHandleRadius);
            PlanCanvas.Children.Add(handle);

            // Path index 1 maps to bend point index 0.
            _conduitBendHandleToIndexMap[handle] = i - 1;
        }
    }

    private void SketchLine_Click(object sender, RoutedEventArgs e)
    {
        if (_isSketchLineMode)
        {
            FinalizeSketchLineDraft();
            _isSketchLineMode = false;
        }
        else
        {
            CancelPendingPlacement();
            ExitSketchModes();
            if (_isDrawingConduit) FinishDrawingConduit();
            if (_isFreehandDrawing) FinishFreehandConduit();
            if (_isEditingConduitPath) ToggleEditConduitPath_Click(sender, e);
            _isSketchLineMode = true;
        }

        UpdateSketchToolButtons();
        Update2DCanvas();
    }

    private void SketchRectangle_Click(object sender, RoutedEventArgs e)
    {
        if (_isSketchRectangleMode)
        {
            _isSketchRectangleMode = false;
            _isSketchRectangleDragging = false;
        }
        else
        {
            CancelPendingPlacement();
            ExitSketchModes();
            if (_isDrawingConduit) FinishDrawingConduit();
            if (_isFreehandDrawing) FinishFreehandConduit();
            if (_isEditingConduitPath) ToggleEditConduitPath_Click(sender, e);
            _isSketchRectangleMode = true;
        }

        UpdateSketchToolButtons();
        Update2DCanvas();
    }

    private void ConvertSketch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSketchPrimitive == null)
        {
            MessageBox.Show("Select a sketch line or rectangle first.", "Convert Sketch", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        switch (_selectedSketchPrimitive)
        {
            case SketchLinePrimitive line:
                ConvertSketchLine(line);
                break;
            case SketchRectanglePrimitive rectangle:
                ConvertSketchRectangle(rectangle);
                break;
        }
    }

    private void ConvertSketchLine(SketchLinePrimitive line)
    {
        if (line.Points.Count < 2)
            return;

        if (line.Points.Count > 2)
        {
            ConvertSketchLineToConduit(line);
            return;
        }

        var choice = MessageBox.Show(
            "Convert straight line to conduit?\n\nYes = Conduit\nNo = Unistrut",
            "Convert Line",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choice == MessageBoxResult.Yes)
            ConvertSketchLineToConduit(line);
        else if (choice == MessageBoxResult.No)
            ConvertSketchLineToUnistrut(line);
    }

    private void ConvertSketchLineToConduit(SketchLinePrimitive line)
    {
        var conduit = new ConduitComponent
        {
            Name = line.Points.Count > 2 ? "Conduit Run" : "Conduit",
            VisualProfile = ElectricalComponentCatalog.Profiles.ConduitEmt
        };

        var startWorld = ClampWorldToPlanBounds(CanvasToWorld(line.Points[0]));
        conduit.Position = startWorld;

        for (int i = 1; i < line.Points.Count; i++)
        {
            var world = ClampWorldToPlanBounds(CanvasToWorld(line.Points[i]));
            conduit.BendPoints.Add(new Point3D(
                world.X - startWorld.X,
                0,
                world.Z - startWorld.Z));
        }

        ConstrainConduitPathToPlanBounds(conduit);
        AddComponentWithUndo(conduit);
        RemoveConvertedSketch(line);
    }

    private void ConvertSketchLineToUnistrut(SketchLinePrimitive line)
    {
        if (line.Points.Count < 2)
            return;

        var worldA = CanvasToWorld(line.Points[0]);
        var worldB = CanvasToWorld(line.Points[1]);
        var dx = worldB.X - worldA.X;
        var dz = worldB.Z - worldA.Z;
        var length = Math.Sqrt(dx * dx + dz * dz);

        var unistrut = new SupportComponent
        {
            Name = "Unistrut",
            VisualProfile = ElectricalComponentCatalog.Profiles.SupportUnistrut,
            SupportType = "Unistrut",
            Position = new Point3D((worldA.X + worldB.X) / 2, 0, (worldA.Z + worldB.Z) / 2),
            Rotation = new Vector3D(0, Math.Atan2(dx, dz) * 180 / Math.PI, 0)
        };
        unistrut.Parameters.Width = 0.135;
        unistrut.Parameters.Height = 0.135;
        unistrut.Parameters.Depth = Math.Max(0.1, length);
        unistrut.Parameters.Material = "Unistrut";
        unistrut.Parameters.Color = "#666666";

        AddComponentWithUndo(unistrut);
        RemoveConvertedSketch(line);
    }

    private void ConvertSketchRectangle(SketchRectanglePrimitive rectangle)
    {
        var choice = MessageBox.Show(
            "Convert rectangle to junction box?\n\nYes = Junction Box\nNo = Electrical Panel",
            "Convert Rectangle",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choice == MessageBoxResult.Cancel)
            return;

        var centerCanvas = new Point(
            (rectangle.Start.X + rectangle.End.X) / 2,
            (rectangle.Start.Y + rectangle.End.Y) / 2);
        var centerWorld = CanvasToWorld(centerCanvas);
        var worldWidth = Math.Max(0.1, Math.Abs(rectangle.End.X - rectangle.Start.X) / 20.0);
        var worldDepth = Math.Max(0.1, Math.Abs(rectangle.End.Y - rectangle.Start.Y) / 20.0);

        if (choice == MessageBoxResult.Yes)
        {
            var box = new BoxComponent
            {
                Name = "Junction Box",
                VisualProfile = ElectricalComponentCatalog.Profiles.BoxJunction,
                BoxType = "Junction Box",
                Position = centerWorld
            };
            box.Parameters.Width = worldWidth;
            box.Parameters.Depth = worldDepth;
            AddComponentWithUndo(box);
        }
        else
        {
            var panel = new PanelComponent
            {
                Name = "Electrical Panel",
                VisualProfile = ElectricalComponentCatalog.Profiles.PanelDistribution,
                PanelType = "Distribution Panel",
                Position = centerWorld
            };
            panel.Parameters.Width = worldWidth;
            panel.Parameters.Depth = worldDepth;
            AddComponentWithUndo(panel);
        }

        RemoveConvertedSketch(rectangle);
    }

    private void RemoveConvertedSketch(SketchPrimitive primitive)
    {
        _sketchPrimitives.Remove(primitive);
        _selectedSketchPrimitive = null;
        UpdateViewport();
        Update2DCanvas();
        UpdatePropertiesPanel();
    }

    private void AddConduit_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Conduit), "toolbar");
    }

    private void AddBox_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box), "toolbar");
    }

    private void AddPanel_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Panel), "toolbar");
    }

    private void AddSupport_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Support), "toolbar");
    }

    private void AddCableTray_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.CableTray), "toolbar");
    }

    private void AddHanger_Click(object sender, RoutedEventArgs e)
    {
        BeginComponentPlacement(ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Hanger), "toolbar");
    }

    private void LibraryItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryListBox.SelectedItem is ElectricalComponent component)
        {
            BeginComponentPlacement(ElectricalComponentCatalog.CloneTemplate(component), "library-double-click");
            e.Handled = true;
        }
    }

    private void LibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LibraryListBox.SelectedItem is ElectricalComponent component)
        {
            BeginComponentPlacement(ElectricalComponentCatalog.CloneTemplate(component), "library-selection");
        }
    }

    private void PostAddComponentMobileUX()
    {
        if (!_isMobileView)
            return;

        SetMobilePane(MobilePane.Canvas);
        ViewTabs.SelectedIndex = 1;
        Update2DCanvas();
    }

    private void FinalizeSketchLineDraft()
    {
        if (_sketchDraftLinePoints.Count >= 2)
        {
            _sketchPrimitives.Add(new SketchLinePrimitive(Guid.NewGuid().ToString(), _sketchDraftLinePoints.ToList()));
            _selectedSketchPrimitive = _sketchPrimitives[^1];
        }

        _sketchDraftLinePoints.Clear();
        RemoveSketchLineRubberBand();
    }

    private void ExitSketchModes()
    {
        _isSketchLineMode = false;
        _isSketchRectangleMode = false;
        _isSketchRectangleDragging = false;
        _sketchDraftLinePoints.Clear();
        RemoveSketchLineRubberBand();
        RemoveSnapIndicator();
        UpdateSketchToolButtons();
    }

    private void UpdateSketchToolButtons()
    {
        SketchLineButton.Background = _isSketchLineMode ? new SolidColorBrush(EditModeButtonColor) : SystemColors.ControlBrush;
        SketchRectangleButton.Background = _isSketchRectangleMode ? new SolidColorBrush(EditModeButtonColor) : SystemColors.ControlBrush;
        SketchLineButton.Content = _isSketchLineMode ? "Finish Sketch Line" : "Sketch Line";
        SketchRectangleButton.Content = _isSketchRectangleMode ? "Finish Sketch Rect" : "Sketch Rectangle";
        UpdatePlanCanvasCursor();
    }
}
