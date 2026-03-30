using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using ElectricalComponentSandbox.Services.RevitIntrospection;
using ElectricalComponentSandbox.Rendering;
using HelixToolkit.Wpf;
using PDFtoImage;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // ===== 2D Plan Canvas =====
    
    private void Update2DCanvas()
    {
        PlanCanvas.Children.Clear();
        _canvasToComponentMap.Clear();
        _conduitBendHandleToIndexMap.Clear();
        _canvasToSketchMap.Clear();
        UpdatePlanCanvasBackground();
        var layerVisibilityById = BuildLayerVisibilityLookup();
        RebuildSnapGeometryCache(layerVisibilityById);
        
        // Draw PDF/Image underlay first (so it appears behind everything)
        DrawPdfUnderlay();
        EnsureConduitVisualHost();
        DrawConduitsWithVisualLayer(layerVisibilityById);
        
        // Draw components
        foreach (var component in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibilityById, component.LayerId))
                continue;
            
            Draw2DComponent(component);
        }

        DrawConduitRuns2D();

        if (_isDrawingConduit)
            DrawConduitPreview();

        if (_isFreehandDrawing)
            DrawFreehandPreview();

        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent selectedConduit)
        {
            EnsureConduitHasEditableEndPoint(selectedConduit);
            DrawConduitEditHandles2D(selectedConduit);
        }

        DrawSketchPrimitives2D();

        if (_isSketchLineMode)
            DrawSketchLineDraft();

        if (_isSketchRectangleMode && _isSketchRectangleDragging)
            DrawSketchRectangleDraft();

        DrawGripHandles();
        RebuildCanvasInteractionShadowTree(layerVisibilityById);
        UpdateCanvasGuidance();
    }

    private void RebuildSnapGeometryCache(IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        _snapEndpointsCache.Clear();
        _snapSegmentsCache.Clear();

        foreach (var comp in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibilityById, comp.LayerId))
                continue;

            double cx = 1000 + comp.Position.X * 20;
            double cy = 1000 - comp.Position.Z * 20;

            if (comp is ConduitComponent conduit)
            {
                var pathPts = conduit.GetPathPoints();
                for (int i = 0; i < pathPts.Count; i++)
                {
                    var cp = new Point(cx + pathPts[i].X * 20, cy - pathPts[i].Z * 20);
                    _snapEndpointsCache.Add(cp);
                    if (i > 0)
                    {
                        var prev = new Point(cx + pathPts[i - 1].X * 20, cy - pathPts[i - 1].Z * 20);
                        _snapSegmentsCache.Add((prev, cp));
                    }
                }
            }
            else
            {
                _snapEndpointsCache.Add(new Point(cx, cy));
            }
        }
    }

    // ── Grip handles ──────────────────────────────────────────────────────────

    private const double GripHandleSize = 7.0;

    /// <summary>
    /// Draws vertex grip handles on the selected conduit component.
    /// Handles are registered in _canvasToGripIndexMap for hit detection.
    /// </summary>
    private void DrawGripHandles()
    {
        _canvasToGripIndexMap.Clear();

        if (_viewModel.SelectedComponent is not ConduitComponent conduit) return;
        if (!_isEditingConduitPath) return;

        double cx = 1000 + conduit.Position.X * 20;
        double cy = 1000 - conduit.Position.Z * 20;

        var pathPts = conduit.GetPathPoints();
        for (int i = 0; i < pathPts.Count; i++)
        {
            double px = cx + pathPts[i].X * 20;
            double py = cy - pathPts[i].Z * 20;

            var grip = new Rectangle
            {
                Width = GripHandleSize,
                Height = GripHandleSize,
                Fill = new SolidColorBrush(Color.FromRgb(0, 80, 200)),
                Stroke = Brushes.White,
                StrokeThickness = 1.0,
                Cursor = System.Windows.Input.Cursors.Hand,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(grip, 300);
            Canvas.SetLeft(grip, px - GripHandleSize / 2);
            Canvas.SetTop(grip, py - GripHandleSize / 2);
            PlanCanvas.Children.Add(grip);
            _canvasToGripIndexMap[grip] = i;
        }
    }

    // ── OSNAP toolbar sync ────────────────────────────────────────────────────

    private void SyncOsnapToolbarState()
    {
        var snap = _viewModel.SnapService;
        OsnapAllToggle.IsChecked      = snap.IsEnabled;
        SnapEndpointToggle.IsChecked  = snap.SnapToEndpoints;
        SnapMidpointToggle.IsChecked  = snap.SnapToMidpoints;
        SnapCenterToggle.IsChecked    = snap.SnapToCenter;
        SnapIntersectToggle.IsChecked = snap.SnapToIntersections;
        SnapPerpendicularToggle.IsChecked = snap.SnapToPerpendicular;
        SnapNearestToggle.IsChecked   = snap.SnapToNearest;
        SnapQuadrantToggle.IsChecked  = snap.SnapToQuadrant;
        SnapTangentToggle.IsChecked   = snap.SnapToTangent;
        OrthoToggle.IsChecked         = _viewModel.IsOrthoActive;
        PolarToggle.IsChecked         = _viewModel.IsPolarActive;
    }

    private void OsnapAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SnapService.IsEnabled = OsnapAllToggle.IsChecked == true;
        SyncCanvasInteractionControllerFromViewModel();
        SkiaBackground.RequestRedraw();
    }

    private void SnapMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;
        bool on = btn.IsChecked == true;
        var snap = _viewModel.SnapService;
        switch (tag)
        {
            case "Endpoint":     snap.SnapToEndpoints     = on; break;
            case "Midpoint":     snap.SnapToMidpoints     = on; break;
            case "Center":       snap.SnapToCenter        = on; break;
            case "Intersection": snap.SnapToIntersections = on; break;
            case "Perpendicular":snap.SnapToPerpendicular = on; break;
            case "Nearest":      snap.SnapToNearest       = on; break;
            case "Quadrant":     snap.SnapToQuadrant      = on; break;
            case "Tangent":      snap.SnapToTangent       = on; break;
        }
    }

    private void OrthoToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsOrthoActive = OrthoToggle.IsChecked == true;
        if (_viewModel.IsOrthoActive) _viewModel.IsPolarActive = false;
        SyncCanvasInteractionControllerFromViewModel();
        SyncOsnapToolbarState();
        SkiaBackground.RequestRedraw();
    }

    private void PolarToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPolarActive = PolarToggle.IsChecked == true;
        if (_viewModel.IsPolarActive) _viewModel.IsOrthoActive = false;
        SyncCanvasInteractionControllerFromViewModel();
        SyncOsnapToolbarState();
        SkiaBackground.RequestRedraw();
    }

    private void PolarAngle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (PolarAngleCombo.SelectedItem is not ComboBoxItem item) return;
        var text = item.Content?.ToString()?.TrimEnd('°');
        if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double deg))
        {
            _viewModel.PolarIncrementDeg = deg;
            SyncCanvasInteractionControllerFromViewModel();
        }
    }

    // ── 2D drawing tool toolbar ───────────────────────────────────────────────

    private void DrawTool2D_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        if (!Enum.TryParse<MarkupToolMode>(tag, out var mode)) return;
        _viewModel.MarkupTool.ActiveMode = mode;

        // Exit conflicting drawing modes when returning to Select
        if (mode == MarkupToolMode.None)
        {
            if (_isSketchLineMode || _isSketchRectangleMode)
            {
                ExitSketchModes();
                Update2DCanvas();
            }
        }
    }

    private void UpdatePlanCanvasBackground()
    {
        // Grid is now drawn by SkiaCanvasHost; just request a redraw
        SkiaBackground.RequestRedraw();
    }

    private void OnSkiaBackgroundRenderFrame(ICanvas2DRenderer renderer)
    {
        SyncCanvasInteractionContextFromViewport();
        SyncCanvasInteractionControllerFromViewModel();

        // White background
        renderer.Clear("#FFFFFFFF");

        if (_viewModel.ShowGrid)
        {
            var gridSizePx = Math.Max(4.0, _viewModel.GridSize * 20.0);
            var scale = PlanCanvasScale.ScaleX;
            var scaledGrid = gridSizePx * scale;

            if (scaledGrid >= 3.0)
            {
                var width = SkiaBackground.ActualWidth;
                var height = SkiaBackground.ActualHeight;

                var offsetX = PlanScrollViewer.HorizontalOffset;
                var offsetY = PlanScrollViewer.VerticalOffset;

                var startX = -(offsetX % scaledGrid);
                var startY = -(offsetY % scaledGrid);

                var gridStyle = RenderStyle.Solid("#FFE8E8E8", 0.6);

                for (var x = startX; x < width; x += scaledGrid)
                    renderer.DrawLine(new Point(x, 0), new Point(x, height), gridStyle);

                for (var y = startY; y < height; y += scaledGrid)
                    renderer.DrawLine(new Point(0, y), new Point(width, y), gridStyle);
            }
        }

        _viewModel.MarkupRenderer.RenderAll(
            renderer,
            _viewModel.Markups,
            BuildLayerVisibilityLookup(),
            SkiaBackground.DrawingContext.CurrentDetailLevel);

        DrawSelectedMarkupOverlay(renderer);

        _canvasInteractionController?.DrawOverlays(renderer);
    }

    private void EnsureConduitVisualHost()
    {
        if (_conduitVisualHost == null)
        {
            _conduitVisualHost = new ConduitVisualHost();
            Canvas.SetLeft(_conduitVisualHost, 0);
            Canvas.SetTop(_conduitVisualHost, 0);
        }

        _conduitVisualHost.Width = PlanCanvas.Width;
        _conduitVisualHost.Height = PlanCanvas.Height;

        if (!PlanCanvas.Children.Contains(_conduitVisualHost))
        {
            PlanCanvas.Children.Add(_conduitVisualHost);
        }
    }

    private void DrawConduitsWithVisualLayer(IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        if (_conduitVisualHost == null)
            return;

        _conduitVisualHost.Render(dc =>
        {
            foreach (var component in _viewModel.Components)
            {
                if (component is not ConduitComponent conduit)
                    continue;

                if (!IsLayerVisible(layerVisibilityById, component.LayerId))
                    continue;

                var points = GetConduitCanvasPathPoints(conduit);
                if (points.Count < 2)
                    continue;

                var selected = component == _viewModel.SelectedComponent;
                var profile = ElectricalComponentCatalog.GetProfile(conduit);
                var strokeColor = selected
                    ? Colors.Orange
                    : ResolveComponentColor(component, Colors.SteelBlue);
                var brush = new SolidColorBrush(strokeColor);
                brush.Freeze();
                var thickness = Math.Max(2, conduit.Diameter * 10) + (selected ? 2 : 0);
                var dashPattern = Array.Empty<double>();
                switch (profile)
                {
                    case ElectricalComponentCatalog.Profiles.ConduitPvc:
                        thickness *= 1.1;
                        dashPattern = new[] { 9.0, 4.0 };
                        break;
                    case ElectricalComponentCatalog.Profiles.ConduitRigidMetal:
                        thickness *= 1.2;
                        break;
                    case ElectricalComponentCatalog.Profiles.ConduitFlexibleMetal:
                        thickness *= 0.95;
                        dashPattern = new[] { 2.5, 2.5 };
                        break;
                }

                var pen = new Pen(brush, thickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                if (dashPattern.Length > 0)
                {
                    pen.DashStyle = new DashStyle(dashPattern, 0);
                }
                pen.Freeze();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    dc.DrawLine(pen, points[i], points[i + 1]);
                }

                if (!selected && profile == ElectricalComponentCatalog.Profiles.ConduitRigidMetal)
                {
                    var jointBrush = new SolidColorBrush(Color.FromArgb(170, 220, 220, 220));
                    jointBrush.Freeze();
                    for (int i = 1; i < points.Count - 1; i++)
                    {
                        dc.DrawEllipse(jointBrush, null, points[i], thickness * 0.4, thickness * 0.4);
                    }
                }
            }
        });
    }
    
    private void Draw2DComponent(ElectricalComponent component)
    {
        var isSelected = _viewModel.IsComponentSelected(component);

        double canvasX = 1000 + component.Position.X * 20;
        double canvasY = 1000 - component.Position.Z * 20;

        switch (component.Type)
        {
            case ComponentType.Conduit:
                // Conduits are rendered by a dedicated DrawingVisual layer for better 2D performance.
                return;
        }

        var color = ResolveComponentColor(component, Colors.SteelBlue);
        var fill = new SolidColorBrush(color);
        var outline = isSelected ? Brushes.Orange : Brushes.Black;
        var profile = ElectricalComponentCatalog.GetProfile(component);

        FrameworkElement element = component switch
        {
            BoxComponent box => CreateBoxPlanSymbol(box, fill, outline, isSelected, profile),
            PanelComponent panel => CreatePanelPlanSymbol(panel, fill, outline, isSelected, profile),
            CableTrayComponent tray => CreateTrayPlanSymbol(tray, fill, outline, isSelected, profile),
            SupportComponent support => CreateSupportPlanSymbol(support, fill, outline, isSelected, profile),
            HangerComponent hanger => CreateHangerPlanSymbol(hanger, fill, outline, isSelected, profile),
            _ => CreateRectElement(component.Parameters.Width * 20, component.Parameters.Height * 20, fill, isSelected)
        };

        ApplyPlanRotation(element, component.Rotation.Y);
        Canvas.SetLeft(element, canvasX - Math.Max(5, element.Width) / 2);
        Canvas.SetTop(element, canvasY - Math.Max(5, element.Height) / 2);
        PlanCanvas.Children.Add(element);
        _canvasToComponentMap[element] = component;
    }
    
    private static Color ResolveComponentColor(ElectricalComponent component, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(component.Parameters.Color))
            return fallback;

        try
        {
            return (Color)ColorConverter.ConvertFromString(component.Parameters.Color);
        }
        catch
        {
            return fallback;
        }
    }

    private static void ApplyPlanRotation(FrameworkElement element, double yRotationDegrees)
    {
        if (Math.Abs(yRotationDegrees) < 0.001)
        {
            element.RenderTransform = Transform.Identity;
            return;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new RotateTransform(yRotationDegrees);
    }

    private Rectangle CreateRectElement(double width, double height, Brush fill, bool isSelected)
    {
        return new Rectangle
        {
            Width = Math.Max(5, width),
            Height = Math.Max(5, height),
            Fill = fill,
            Stroke = isSelected ? Brushes.Orange : Brushes.Black,
            StrokeThickness = isSelected ? 3 : 1
        };
    }

    private FrameworkElement CreateBoxPlanSymbol(BoxComponent box, Brush fill, Brush outline, bool isSelected, string profile)
    {
        var width = Math.Max(18, box.Parameters.Width * 20);
        var height = Math.Max(18, box.Parameters.Depth * 20);
        var canvas = CreateSymbolCanvas(width, height);
        var strokeThickness = isSelected ? 3 : 1.5;

        var shell = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = outline,
            StrokeThickness = strokeThickness
        };
        AddSymbolChild(canvas, shell);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.BoxPull:
                var insetPull = new Rectangle
                {
                    Width = width * 0.72,
                    Height = height * 0.64,
                    Fill = Brushes.Transparent,
                    Stroke = outline,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(insetPull, width * 0.14);
                Canvas.SetTop(insetPull, height * 0.18);
                AddSymbolChild(canvas, insetPull);
                AddCenteredCross(canvas, width, height, outline, 0.26);
                break;

            case ElectricalComponentCatalog.Profiles.BoxFloor:
                var centerRadius = Math.Min(width, height) * 0.22;
                var cover = new Ellipse
                {
                    Width = centerRadius * 2,
                    Height = centerRadius * 2,
                    Fill = Brushes.Transparent,
                    Stroke = outline,
                    StrokeThickness = 1.2
                };
                Canvas.SetLeft(cover, width / 2 - centerRadius);
                Canvas.SetTop(cover, height / 2 - centerRadius);
                AddSymbolChild(canvas, cover);
                AddCenteredCross(canvas, width, height, outline, 0.14);
                break;

            case ElectricalComponentCatalog.Profiles.BoxDisconnectSwitch:
                var handle = new Line
                {
                    X1 = width * 0.62,
                    Y1 = height * 0.3,
                    X2 = width * 0.8,
                    Y2 = height * 0.55,
                    Stroke = outline,
                    StrokeThickness = 2,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                AddSymbolChild(canvas, handle);
                var door = new Rectangle
                {
                    Width = width * 0.5,
                    Height = height * 0.7,
                    Fill = Brushes.Transparent,
                    Stroke = outline,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(door, width * 0.16);
                Canvas.SetTop(door, height * 0.14);
                AddSymbolChild(canvas, door);
                break;

            default:
                AddCenteredCross(canvas, width, height, outline, 0.34);
                break;
        }

        return canvas;
    }

    private FrameworkElement CreatePanelPlanSymbol(PanelComponent panel, Brush fill, Brush outline, bool isSelected, string profile)
    {
        var width = Math.Max(22, panel.Parameters.Width * 20);
        var height = Math.Max(14, panel.Parameters.Depth * 20);
        var canvas = CreateSymbolCanvas(width, height);
        var strokeThickness = isSelected ? 3 : 1.6;

        var shell = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = outline,
            StrokeThickness = strokeThickness
        };
        AddSymbolChild(canvas, shell);

        var sections = profile switch
        {
            ElectricalComponentCatalog.Profiles.PanelLighting => 2,
            ElectricalComponentCatalog.Profiles.PanelSwitchboard => 5,
            ElectricalComponentCatalog.Profiles.PanelMcc => 4,
            _ => 3
        };

        for (int i = 1; i < sections; i++)
        {
            var x = width * i / sections;
            AddSymbolChild(canvas, new Line
            {
                X1 = x,
                Y1 = 2,
                X2 = x,
                Y2 = height - 2,
                Stroke = outline,
                StrokeThickness = 1
            });
        }

        if (profile == ElectricalComponentCatalog.Profiles.PanelMcc)
        {
            for (int i = 1; i <= 3; i++)
            {
                var y = height * i / 4;
                AddSymbolChild(canvas, new Line
                {
                    X1 = 2,
                    Y1 = y,
                    X2 = width - 2,
                    Y2 = y,
                    Stroke = outline,
                    StrokeThickness = 0.8
                });
            }
        }

        var handle = new Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = outline
        };
        Canvas.SetLeft(handle, width * 0.86);
        Canvas.SetTop(handle, height * 0.45);
        AddSymbolChild(canvas, handle);
        return canvas;
    }

    private FrameworkElement CreateTrayPlanSymbol(CableTrayComponent tray, Brush fill, Brush outline, bool isSelected, string profile)
    {
        var width = Math.Max(28, tray.Parameters.Depth * 20);
        var height = Math.Max(10, tray.Parameters.Width * 2.4);
        var canvas = CreateSymbolCanvas(width, height);
        var strokeThickness = isSelected ? 2.8 : 1.4;
        var railTop = 2.0;
        var railBottom = height - 2.0;

        var background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = profile == ElectricalComponentCatalog.Profiles.TraySolidBottom
                ? fill
                : new SolidColorBrush(Color.FromArgb(30, 120, 120, 120)),
            Stroke = outline,
            StrokeThickness = strokeThickness
        };
        AddSymbolChild(canvas, background);

        AddSymbolChild(canvas, new Line
        {
            X1 = 1,
            Y1 = railTop,
            X2 = width - 1,
            Y2 = railTop,
            Stroke = outline,
            StrokeThickness = 1.2
        });
        AddSymbolChild(canvas, new Line
        {
            X1 = 1,
            Y1 = railBottom,
            X2 = width - 1,
            Y2 = railBottom,
            Stroke = outline,
            StrokeThickness = 1.2
        });

        var spacing = profile switch
        {
            ElectricalComponentCatalog.Profiles.TrayWireMesh => 8.0,
            ElectricalComponentCatalog.Profiles.TraySolidBottom => 16.0,
            _ => 12.0
        };

        for (double x = spacing; x < width - spacing / 2; x += spacing)
        {
            AddSymbolChild(canvas, new Line
            {
                X1 = x,
                Y1 = 2,
                X2 = x,
                Y2 = height - 2,
                Stroke = outline,
                StrokeThickness = profile == ElectricalComponentCatalog.Profiles.TrayWireMesh ? 0.9 : 1.1
            });
        }

        if (profile == ElectricalComponentCatalog.Profiles.TrayWireMesh)
        {
            for (double y = 4; y < height - 4; y += 4)
            {
                AddSymbolChild(canvas, new Line
                {
                    X1 = 2,
                    Y1 = y,
                    X2 = width - 2,
                    Y2 = y,
                    Stroke = outline,
                    StrokeThickness = 0.6
                });
            }
        }

        return canvas;
    }

    private FrameworkElement CreateSupportPlanSymbol(SupportComponent support, Brush fill, Brush outline, bool isSelected, string profile)
    {
        var width = Math.Max(22, support.Parameters.Depth * 20);
        var height = Math.Max(12, support.Parameters.Height * 20);
        var canvas = CreateSymbolCanvas(width, height);
        var strokeThickness = isSelected ? 3 : 1.6;
        var midY = height / 2;

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.SupportWallBracket:
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.18,
                    Y1 = height * 0.15,
                    X2 = width * 0.18,
                    Y2 = height * 0.82,
                    Stroke = outline,
                    StrokeThickness = strokeThickness
                });
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.18,
                    Y1 = height * 0.82,
                    X2 = width * 0.82,
                    Y2 = height * 0.82,
                    Stroke = outline,
                    StrokeThickness = strokeThickness
                });
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.24,
                    Y1 = height * 0.74,
                    X2 = width * 0.68,
                    Y2 = height * 0.36,
                    Stroke = outline,
                    StrokeThickness = 1.2
                });
                break;

            case ElectricalComponentCatalog.Profiles.SupportTrapeze:
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.12,
                    Y1 = midY,
                    X2 = width * 0.88,
                    Y2 = midY,
                    Stroke = outline,
                    StrokeThickness = strokeThickness
                });
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.24,
                    Y1 = height * 0.12,
                    X2 = width * 0.24,
                    Y2 = midY,
                    Stroke = outline,
                    StrokeThickness = 1.4
                });
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.76,
                    Y1 = height * 0.12,
                    X2 = width * 0.76,
                    Y2 = midY,
                    Stroke = outline,
                    StrokeThickness = 1.4
                });
                break;

            default:
                var body = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = fill,
                    Stroke = outline,
                    StrokeThickness = strokeThickness
                };
                AddSymbolChild(canvas, body);
                AddSymbolChild(canvas, new Line
                {
                    X1 = width * 0.1,
                    Y1 = midY,
                    X2 = width * 0.9,
                    Y2 = midY,
                    Stroke = outline,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                });
                break;
        }

        return canvas;
    }

    private FrameworkElement CreateHangerPlanSymbol(HangerComponent hanger, Brush fill, Brush outline, bool isSelected, string profile)
    {
        var size = Math.Max(12, hanger.RodDiameter * 24 + 8);
        var canvas = CreateSymbolCanvas(size, size);
        var strokeThickness = isSelected ? 3 : 1.4;

        if (profile == ElectricalComponentCatalog.Profiles.HangerSeismicBrace)
        {
            var anchor = new Rectangle
            {
                Width = size * 0.28,
                Height = size * 0.28,
                Fill = fill,
                Stroke = outline,
                StrokeThickness = 1
            };
            Canvas.SetLeft(anchor, size * 0.08);
            Canvas.SetTop(anchor, size * 0.64);
            AddSymbolChild(canvas, anchor);

            AddSymbolChild(canvas, new Line
            {
                X1 = size * 0.24,
                Y1 = size * 0.76,
                X2 = size * 0.84,
                Y2 = size * 0.2,
                Stroke = outline,
                StrokeThickness = strokeThickness
            });

            var tip = new Ellipse
            {
                Width = size * 0.22,
                Height = size * 0.22,
                Fill = fill,
                Stroke = outline,
                StrokeThickness = 1
            };
            Canvas.SetLeft(tip, size * 0.74);
            Canvas.SetTop(tip, size * 0.1);
            AddSymbolChild(canvas, tip);
        }
        else
        {
            var rod = new Ellipse
            {
                Width = size * 0.58,
                Height = size * 0.58,
                Fill = fill,
                Stroke = outline,
                StrokeThickness = strokeThickness
            };
            Canvas.SetLeft(rod, size * 0.21);
            Canvas.SetTop(rod, size * 0.21);
            AddSymbolChild(canvas, rod);

            AddCenteredCross(canvas, size, size, outline, 0.22);
        }

        return canvas;
    }

    private static Canvas CreateSymbolCanvas(double width, double height)
    {
        return new Canvas
        {
            Width = Math.Max(6, width),
            Height = Math.Max(6, height),
            Background = Brushes.Transparent
        };
    }

    private static void AddSymbolChild(Canvas canvas, UIElement child)
    {
        child.IsHitTestVisible = false;
        canvas.Children.Add(child);
    }

    private static void AddCenteredCross(Canvas canvas, double width, double height, Brush stroke, double insetScale)
    {
        var insetX = width * insetScale;
        var insetY = height * insetScale;
        AddSymbolChild(canvas, new Line
        {
            X1 = insetX,
            Y1 = insetY,
            X2 = width - insetX,
            Y2 = height - insetY,
            Stroke = stroke,
            StrokeThickness = 1
        });
        AddSymbolChild(canvas, new Line
        {
            X1 = insetX,
            Y1 = height - insetY,
            X2 = width - insetX,
            Y2 = insetY,
            Stroke = stroke,
            StrokeThickness = 1
        });
    }

    private void DrawSketchPrimitives2D()
    {
        foreach (var primitive in _sketchPrimitives)
        {
            if (primitive is SketchLinePrimitive line && line.Points.Count >= 2)
            {
                var shape = new Polyline
                {
                    Stroke = ReferenceEquals(primitive, _selectedSketchPrimitive) ? Brushes.DarkOrange : Brushes.MediumPurple,
                    StrokeThickness = ReferenceEquals(primitive, _selectedSketchPrimitive) ? 3 : 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    StrokeLineJoin = PenLineJoin.Round
                };

                foreach (var p in line.Points)
                    shape.Points.Add(p);

                PlanCanvas.Children.Add(shape);
                _canvasToSketchMap[shape] = primitive;
            }
            else if (primitive is SketchRectanglePrimitive rect)
            {
                var left = Math.Min(rect.Start.X, rect.End.X);
                var top = Math.Min(rect.Start.Y, rect.End.Y);
                var width = Math.Max(1, Math.Abs(rect.End.X - rect.Start.X));
                var height = Math.Max(1, Math.Abs(rect.End.Y - rect.Start.Y));
                var shape = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = Brushes.Transparent,
                    Stroke = ReferenceEquals(primitive, _selectedSketchPrimitive) ? Brushes.DarkOrange : Brushes.Teal,
                    StrokeThickness = ReferenceEquals(primitive, _selectedSketchPrimitive) ? 3 : 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                Canvas.SetLeft(shape, left);
                Canvas.SetTop(shape, top);
                PlanCanvas.Children.Add(shape);
                _canvasToSketchMap[shape] = primitive;
            }
        }
    }

    private void DrawSketchLineDraft()
    {
        if (_sketchDraftLinePoints.Count == 0)
            return;

        var preview = new Polyline
        {
            Stroke = Brushes.MediumPurple,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };

        foreach (var point in _sketchDraftLinePoints)
            preview.Points.Add(point);

        PlanCanvas.Children.Add(preview);

        foreach (var point in _sketchDraftLinePoints)
        {
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.MediumPurple,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, point.X - 3);
            Canvas.SetTop(dot, point.Y - 3);
            PlanCanvas.Children.Add(dot);
        }
    }

    private void DrawSketchRectangleDraft()
    {
        if (!_isSketchRectangleDragging)
            return;

        var left = Math.Min(_sketchRectangleStartPoint.X, _lastMousePosition.X);
        var top = Math.Min(_sketchRectangleStartPoint.Y, _lastMousePosition.Y);
        var width = Math.Max(1, Math.Abs(_lastMousePosition.X - _sketchRectangleStartPoint.X));
        var height = Math.Max(1, Math.Abs(_lastMousePosition.Y - _sketchRectangleStartPoint.Y));

        _sketchRectanglePreview = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(Color.FromArgb(20, 0, 128, 128)),
            Stroke = Brushes.Teal,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_sketchRectanglePreview, left);
        Canvas.SetTop(_sketchRectanglePreview, top);
        PlanCanvas.Children.Add(_sketchRectanglePreview);
    }

}
