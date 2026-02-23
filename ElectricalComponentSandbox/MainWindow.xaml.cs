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
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;
using PDFtoImage;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private string? _currentFilePath;
    private readonly Dictionary<ModelVisual3D, ElectricalComponent> _visualToComponentMap = new();
    private bool _isEditingConduitPath = false;
    private readonly List<ModelVisual3D> _bendPointHandles = new();
    private ModelVisual3D? _draggedHandle = null;
    private Point _lastMousePosition;
    private BitmapSource? _cachedPdfBitmap;
    private string? _cachedPdfPath;
    private int _cachedPdfPage = -1;
    private ModelVisual3D? _pdfUnderlayVisual3D;
    private const double PdfUnderlayPlaneY = -0.02;
    private bool _isMobileView = false;
    private WindowState _desktopWindowState = WindowState.Maximized;
    private double _desktopWidth = 1400;
    private double _desktopHeight = 800;
    private GridLength _desktopLibraryColumnWidth = new GridLength(200);
    private GridLength _desktopViewportColumnWidth = new GridLength(1, GridUnitType.Star);
    private GridLength _desktopPropertiesColumnWidth = new GridLength(300);
    private bool _showDesktopLibraryPanel = true;
    private bool _showDesktopPropertiesPanel = true;
    private MobilePane _activeMobilePane = MobilePane.Canvas;
    private MobileTheme _mobileTheme = MobileTheme.IOS;
    private const double MobileWindowWidth = 430;
    private const double MobileWindowHeight = 932;
    private const double MobileMinCanvasScale = 0.2;
    private const double MobileMaxCanvasScale = 12.0;
    
    
    // 2D canvas state
    private bool _isDragging2D = false;
    private FrameworkElement? _draggedElement2D = null;
    private readonly Dictionary<FrameworkElement, ElectricalComponent> _canvasToComponentMap = new();
    private readonly Dictionary<FrameworkElement, int> _conduitBendHandleToIndexMap = new();
    private readonly Dictionary<FrameworkElement, SketchPrimitive> _canvasToSketchMap = new();
    private readonly List<Point> _snapEndpointsCache = new();
    private readonly List<(Point A, Point B)> _snapSegmentsCache = new();
    private Point _dragStartCanvasPosition;
    private bool _mobileSelectionCandidate = false;
    private bool _isDraggingConduitBend2D = false;
    private int _draggingConduitBendIndex2D = -1;
    private ConduitComponent? _draggingConduit2D = null;
    private readonly List<SketchPrimitive> _sketchPrimitives = new();
    private SketchPrimitive? _selectedSketchPrimitive = null;
    private bool _isSketchLineMode = false;
    private bool _isSketchRectangleMode = false;
    private readonly List<Point> _sketchDraftLinePoints = new();
    private Line? _sketchRubberBandLine = null;
    private Rectangle? _sketchRectanglePreview = null;
    private bool _isSketchRectangleDragging = false;
    private Point _sketchRectangleStartPoint;
    private ConduitVisualHost? _conduitVisualHost;
    private DrawingBrush? _cachedGridBrush;
    private double _cachedGridSizePx = -1;
    private readonly DispatcherTimer _interactionQualityRestoreTimer = new();
    private DateTime _lastInteractionInputUtc = DateTime.MinValue;
    private bool _isFastInteractionMode = false;
    private bool _queuedSceneRefresh = false;
    private bool _pending2DRefresh = false;
    private bool _pending3DRefresh = false;
    private bool _pendingPropertiesRefresh = false;
    private ElectricalComponent? _pendingPlacementComponent = null;
    private string? _pendingPlacementSource = null;
    
    // Conduit drawing mode (Bluebeam-style polyline tool)
    private bool _isDrawingConduit = false;
    #pragma warning disable CS0414
    private ConduitComponent? _drawingConduit = null;
    #pragma warning restore CS0414
    private readonly List<Point> _drawingCanvasPoints = new(); // canvas-space vertices placed so far
    private Line? _rubberBandLine = null; // live preview from last vertex to cursor
    private Ellipse? _snapIndicator = null; // visual indicator when snapping
    
    // Constants for bend point visualization
    private const double BendPointHandleRadius = 0.3;
    private static readonly Color EditModeButtonColor = Color.FromRgb(255, 200, 100);
    private static readonly Color BendPointHandleColor = Colors.Orange;
    private const double Conduit2DHandleRadius = 6.0;
    private const double Conduit2DInsertThreshold = 12.0;
    private const double Conduit2DHitThreshold = 10.0;
    private const double CatalogDimensionTolerance = 0.0005;
    
    // Constants for smooth conduit rendering
    private const int MaxSegmentResolution = 50;
    private const int MinSegmentResolution = 5;
    private const double ResolutionScaleFactor = 10.0;
    private const double CanvasWorldOrigin = 1000.0;
    private const double CanvasWorldScale = 20.0;
    private const double DefaultPlanCanvasSize = 2000.0;

    private readonly record struct PlanWorldBounds(double MinX, double MaxX, double MinZ, double MaxZ);
    private readonly record struct UnderlayCanvasFrame(PdfUnderlay Underlay, double ScaledWidth, double ScaledHeight, Point[] Corners);

    private enum MobilePane
    {
        Canvas,
        Library,
        Properties
    }

    private enum MobileTheme
    {
        IOS,
        AndroidMaterial
    }

    private abstract record SketchPrimitive(string Id);
    private sealed record SketchLinePrimitive(string Id, List<Point> Points) : SketchPrimitive(Id);
    private sealed record SketchRectanglePrimitive(string Id, Point Start, Point End) : SketchPrimitive(Id);
    
    public MainWindow()
    {
        InitializeComponent();
        SetMobileTheme(MobileTheme.IOS);
        _interactionQualityRestoreTimer.Interval = TimeSpan.FromMilliseconds(120);
        _interactionQualityRestoreTimer.Tick += InteractionQualityRestoreTimer_Tick;
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        ApplyDesktopPaneLayout();
        
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        ActionLogService.Instance.Log(LogCategory.Application, "MainWindow initialized");
        Closed += (s, e) =>
        {
            ActionLogService.Instance.LogSeparator("SESSION SUMMARY");
            ActionLogService.Instance.Log(LogCategory.Application, "Final state",
                $"Components: {_viewModel.Components.Count}, Layers: {_viewModel.Layers.Count}, " +
                $"Units: {_viewModel.UnitSystemName}, Grid: {_viewModel.GridSize}");
            ActionLogService.Instance.Log(LogCategory.Application, "MainWindow closed");
        };
    }
    
    // ===== Window-Level Input Logging (captures ALL clicks, keys, scrolls) =====
    
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var name = source?.Name;
        if (string.IsNullOrEmpty(name)) name = source?.GetType().Name ?? "Unknown";
        var pos = e.GetPosition(this);
        ActionLogService.Instance.Log(LogCategory.Input, "Left click",
            $"Element: {name}, Position: ({pos.X:F0}, {pos.Y:F0})");
        base.OnPreviewMouseLeftButtonDown(e);
    }
    
    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var name = source?.Name;
        if (string.IsNullOrEmpty(name)) name = source?.GetType().Name ?? "Unknown";
        var pos = e.GetPosition(this);
        ActionLogService.Instance.Log(LogCategory.Input, "Right click",
            $"Element: {name}, Position: ({pos.X:F0}, {pos.Y:F0})");
        base.OnPreviewMouseRightButtonDown(e);
    }
    
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.Input, "Mouse wheel",
            $"Delta: {e.Delta}, Direction: {(e.Delta > 0 ? "Up" : "Down")}");
        base.OnPreviewMouseWheel(e);
    }
    
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        var modStr = modifiers != ModifierKeys.None ? $"{modifiers}+" : "";
        ActionLogService.Instance.Log(LogCategory.Input, "Key press", $"Key: {modStr}{e.Key}");
        base.OnPreviewKeyDown(e);
    }

    private void BeginFastInteractionMode()
    {
        _lastInteractionInputUtc = DateTime.UtcNow;
        if (!_isFastInteractionMode)
        {
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(PlanCanvas, BitmapScalingMode.LowQuality);
            _isFastInteractionMode = true;
        }

        if (!_interactionQualityRestoreTimer.IsEnabled)
            _interactionQualityRestoreTimer.Start();
    }

    private void InteractionQualityRestoreTimer_Tick(object? sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastInteractionInputUtc).TotalMilliseconds < 180)
            return;

        _interactionQualityRestoreTimer.Stop();
        if (_isFastInteractionMode)
        {
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(PlanCanvas, BitmapScalingMode.HighQuality);
            _isFastInteractionMode = false;
        }
    }

    private void QueueSceneRefresh(bool update2D = false, bool update3D = false, bool updateProperties = false)
    {
        _pending2DRefresh |= update2D;
        _pending3DRefresh |= update3D;
        _pendingPropertiesRefresh |= updateProperties;

        if (_queuedSceneRefresh) return;
        _queuedSceneRefresh = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _queuedSceneRefresh = false;
            var do2D = _pending2DRefresh;
            var do3D = _pending3DRefresh;
            var doProperties = _pendingPropertiesRefresh;
            _pending2DRefresh = false;
            _pending3DRefresh = false;
            _pendingPropertiesRefresh = false;

            if (do3D) UpdateViewport();
            if (do2D) Update2DCanvas();
            if (doProperties) UpdatePropertiesPanel();
        }));
    }
    
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedComponent))
        {
            QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        }
        else if (e.PropertyName == nameof(MainViewModel.Components))
        {
            QueueSceneRefresh(update2D: true, update3D: true);
        }
    }

    private Dictionary<string, bool> BuildLayerVisibilityLookup()
    {
        var lookup = new Dictionary<string, bool>(_viewModel.Layers.Count, StringComparer.Ordinal);
        foreach (var layer in _viewModel.Layers)
        {
            lookup[layer.Id] = layer.IsVisible;
        }

        return lookup;
    }

    private static bool IsLayerVisible(IReadOnlyDictionary<string, bool> layerVisibilityById, string layerId)
    {
        return !layerVisibilityById.TryGetValue(layerId, out var visible) || visible;
    }
    
    private void UpdatePropertiesPanel()
    {
        var component = _viewModel.SelectedComponent;
        if (component == null)
        {
            ClearPropertiesPanel();
            return;
        }
        
        NameTextBox.Text = component.Name;
        TypeTextBox.Text = component.Type.ToString();
        
        PositionXTextBox.Text = component.Position.X.ToString("F2");
        PositionYTextBox.Text = component.Position.Y.ToString("F2");
        PositionZTextBox.Text = component.Position.Z.ToString("F2");
        
        RotationXTextBox.Text = component.Rotation.X.ToString("F2");
        RotationYTextBox.Text = component.Rotation.Y.ToString("F2");
        RotationZTextBox.Text = component.Rotation.Z.ToString("F2");
        
        WidthTextBox.Text = FormatDimension(component.Parameters.Width);
        HeightTextBox.Text = FormatDimension(component.Parameters.Height);
        DepthTextBox.Text = FormatDimension(component.Parameters.Depth);
        MaterialTextBox.Text = component.Parameters.Material;
        ElevationTextBox.Text = component.Parameters.Elevation.ToString("F2");
        ColorTextBox.Text = component.Parameters.Color;
        ManufacturerTextBox.Text = component.Parameters.Manufacturer;
        PartNumberTextBox.Text = component.Parameters.PartNumber;
        ReferenceUrlTextBox.Text = component.Parameters.ReferenceUrl;
        
        // Set layer combo
        var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
        if (layer != null)
            LayerComboBox.SelectedItem = layer;
        
        // Update conduit-specific properties
        if (component is ConduitComponent conduit)
        {
            ConduitProperties.Visibility = Visibility.Visible;
            BendPointsTextBlock.Text = conduit.BendPoints.Count.ToString();
        }
        else
        {
            ConduitProperties.Visibility = Visibility.Collapsed;
        }
    }
    
    private void ClearPropertiesPanel()
    {
        NameTextBox.Text = string.Empty;
        TypeTextBox.Text = string.Empty;
        PositionXTextBox.Text = string.Empty;
        PositionYTextBox.Text = string.Empty;
        PositionZTextBox.Text = string.Empty;
        RotationXTextBox.Text = string.Empty;
        RotationYTextBox.Text = string.Empty;
        RotationZTextBox.Text = string.Empty;
        WidthTextBox.Text = string.Empty;
        HeightTextBox.Text = string.Empty;
        DepthTextBox.Text = string.Empty;
        MaterialTextBox.Text = string.Empty;
        ElevationTextBox.Text = string.Empty;
        ColorTextBox.Text = string.Empty;
        ManufacturerTextBox.Text = string.Empty;
        PartNumberTextBox.Text = string.Empty;
        ReferenceUrlTextBox.Text = string.Empty;
    }

    private static string FormatDimension(double value) => value.ToString("0.#####");
    
    private void UpdateViewport()
    {
        // Clear existing models and mapping
        for (int i = Viewport.Children.Count - 1; i >= 0; i--)
        {
            if (Viewport.Children[i] is ModelVisual3D visual && visual.Content is GeometryModel3D)
            {
                Viewport.Children.RemoveAt(i);
            }
        }
        
        _pdfUnderlayVisual3D = null;
        _visualToComponentMap.Clear();
        var layerVisibilityById = BuildLayerVisibilityLookup();
        AddPdfUnderlayToViewport();
        
        // Add components to viewport
        foreach (var component in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibilityById, component.LayerId))
                continue;
            
            AddComponentToViewport(component);
        }

        UpdateConduitRuns3D();
    }
    
    private void AddComponentToViewport(ElectricalComponent component)
    {
        var visual = new ModelVisual3D();
        var geometry = CreateComponentGeometry(component);
        
        var color = ResolveComponentColor(component, Colors.SlateGray);
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        
        Material appliedMaterial;
        if (component == _viewModel.SelectedComponent)
        {
            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(material);
            materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 165, 0))));
            appliedMaterial = materialGroup;
        }
        else
        {
            appliedMaterial = material;
        }
        
        var model = new GeometryModel3D(geometry, appliedMaterial);
        
        // Apply transformations
        var transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(new TranslateTransform3D(component.Position.X, component.Position.Y, component.Position.Z));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), component.Rotation.X)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), component.Rotation.Y)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), component.Rotation.Z)));
        transformGroup.Children.Add(new ScaleTransform3D(component.Scale.X, component.Scale.Y, component.Scale.Z));
        
        model.Transform = transformGroup;
        visual.Content = model;
        
        Viewport.Children.Add(visual);
        _visualToComponentMap[visual] = component;
    }
    
    private MeshGeometry3D CreateComponentGeometry(ElectricalComponent component)
    {
        var builder = new MeshBuilder();
        var profile = ElectricalComponentCatalog.GetProfile(component);
        
        switch (component.Type)
        {
            case ComponentType.Conduit:
                if (component is ConduitComponent conduit)
                {
                    CreateConduitGeometry(builder, conduit, profile);
                }
                break;
                
            case ComponentType.Hanger:
                if (component is HangerComponent hanger)
                {
                    CreateHangerGeometry(builder, hanger, profile);
                }
                break;
                
            case ComponentType.CableTray:
                if (component is CableTrayComponent tray)
                {
                    CreateCableTrayGeometry(builder, tray, profile);
                }
                break;
                
            case ComponentType.Box:
                if (component is BoxComponent box)
                    CreateBoxGeometry(builder, box, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;

            case ComponentType.Panel:
                if (component is PanelComponent panel)
                    CreatePanelGeometry(builder, panel, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;

            case ComponentType.Support:
                if (component is SupportComponent support)
                    CreateSupportGeometry(builder, support, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;
        }
        
        return builder.ToMesh();
    }
    
    private void CreateConduitGeometry(MeshBuilder builder, ConduitComponent conduit, string profile)
    {
        var pathPoints = conduit.GetPathPoints();
        if (pathPoints.Count < 2)
            return;

        var renderPath = pathPoints.Count == 2
            ? pathPoints
            : GenerateSmoothPath(pathPoints, conduit.BendRadius);
        if (renderPath.Count < 2)
            return;

        var radius = Math.Max(0.03, conduit.Diameter * 0.5);
        var thetaDiv = 20;
        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.ConduitPvc:
                radius *= 1.08;
                thetaDiv = 16;
                break;
            case ElectricalComponentCatalog.Profiles.ConduitRigidMetal:
                radius *= 1.18;
                thetaDiv = 24;
                break;
            case ElectricalComponentCatalog.Profiles.ConduitFlexibleMetal:
                radius *= 0.95;
                thetaDiv = 14;
                break;
        }

        for (int i = 0; i < renderPath.Count - 1; i++)
        {
            var start = renderPath[i];
            var end = renderPath[i + 1];
            builder.AddCylinder(start, end, radius, thetaDiv);
        }

        for (int i = 1; i < renderPath.Count - 1; i++)
        {
            var couplingRadius = profile == ElectricalComponentCatalog.Profiles.ConduitRigidMetal
                ? radius * 1.24
                : radius * 1.14;
            builder.AddSphere(renderPath[i], couplingRadius, 10, 8);
        }

        if (profile == ElectricalComponentCatalog.Profiles.ConduitFlexibleMetal)
        {
            AddFlexibleConduitRibbing(builder, renderPath, radius);
        }
        else if (profile == ElectricalComponentCatalog.Profiles.ConduitRigidMetal)
        {
            AddRigidConduitEndCollars(builder, renderPath, radius);
        }
    }

    private static void AddFlexibleConduitRibbing(MeshBuilder builder, IReadOnlyList<Point3D> points, double baseRadius)
    {
        const double spacing = 0.45;
        var ribRadius = baseRadius * 1.06;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var segment = points[i + 1] - points[i];
            var length = segment.Length;
            if (length < 1e-4)
                continue;

            var dir = segment;
            dir.Normalize();
            var ribCount = (int)(length / spacing);
            for (int rib = 1; rib < ribCount; rib++)
            {
                var distance = rib * spacing;
                var center = points[i] + dir * distance;
                var half = dir * 0.04;
                builder.AddCylinder(center - half, center + half, ribRadius, 8);
            }
        }
    }

    private static void AddRigidConduitEndCollars(MeshBuilder builder, IReadOnlyList<Point3D> points, double radius)
    {
        if (points.Count < 2)
            return;

        var collarLength = Math.Max(0.08, radius * 0.9);
        AddEndCollar(builder, points[0], points[1], collarLength, radius * 1.26);
        AddEndCollar(builder, points[^1], points[^2], collarLength, radius * 1.26);
    }

    private static void AddEndCollar(MeshBuilder builder, Point3D endPoint, Point3D adjacentPoint, double length, double radius)
    {
        var dir = endPoint - adjacentPoint;
        if (dir.Length < 1e-5)
            return;

        dir.Normalize();
        var inner = endPoint - dir * length;
        builder.AddCylinder(inner, endPoint, radius, 14);
    }

    private static void CreateBoxGeometry(MeshBuilder builder, BoxComponent box, string profile)
    {
        var width = Math.Max(0.1, box.Parameters.Width);
        var height = Math.Max(0.1, box.Parameters.Height);
        var depth = Math.Max(0.1, box.Parameters.Depth);
        var center = new Point3D(0, height / 2, 0);

        builder.AddBox(center, width, height, depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.BoxPull:
                AddBoxKnockouts(builder, width, height, depth, 3, 0.9);
                builder.AddBox(new Point3D(0, height + 0.08, 0), width * 0.92, 0.16, depth * 0.92);
                break;
            case ElectricalComponentCatalog.Profiles.BoxFloor:
                builder.AddCylinder(
                    new Point3D(0, height + 0.02, 0),
                    new Point3D(0, height + 0.16, 0),
                    Math.Min(width, depth) * 0.3,
                    20);
                builder.AddCylinder(
                    new Point3D(0, height + 0.16, 0),
                    new Point3D(0, height + 0.3, 0),
                    Math.Min(width, depth) * 0.08,
                    14);
                break;
            case ElectricalComponentCatalog.Profiles.BoxDisconnectSwitch:
                builder.AddBox(new Point3D(0, height * 0.65, depth * 0.52), width * 0.42, height * 0.38, 0.22);
                builder.AddCylinder(
                    new Point3D(width * 0.2, height * 0.65, depth * 0.62),
                    new Point3D(width * 0.42, height * 0.65, depth * 0.62),
                    Math.Min(width, height) * 0.06,
                    14);
                break;
            default:
                AddBoxKnockouts(builder, width, height, depth, 2, 1.0);
                break;
        }
    }

    private static void AddBoxKnockouts(MeshBuilder builder, double width, double height, double depth, int countPerSide, double scale)
    {
        var radius = Math.Max(0.06, Math.Min(width, depth) * 0.08 * scale);
        var y = height * 0.55;
        var zStep = depth / (countPerSide + 1);
        var xStep = width / (countPerSide + 1);

        for (int i = 1; i <= countPerSide; i++)
        {
            var z = -depth / 2 + zStep * i;
            builder.AddCylinder(new Point3D(-width / 2 - 0.08, y, z), new Point3D(-width / 2 + 0.08, y, z), radius, 10);
            builder.AddCylinder(new Point3D(width / 2 - 0.08, y, z), new Point3D(width / 2 + 0.08, y, z), radius, 10);
        }

        for (int i = 1; i <= countPerSide; i++)
        {
            var x = -width / 2 + xStep * i;
            builder.AddCylinder(new Point3D(x, y, -depth / 2 - 0.08), new Point3D(x, y, -depth / 2 + 0.08), radius, 10);
            builder.AddCylinder(new Point3D(x, y, depth / 2 - 0.08), new Point3D(x, y, depth / 2 + 0.08), radius, 10);
        }
    }

    private static void CreatePanelGeometry(MeshBuilder builder, PanelComponent panel, string profile)
    {
        var width = Math.Max(1, panel.Parameters.Width);
        var height = Math.Max(1, panel.Parameters.Height);
        var depth = Math.Max(0.6, panel.Parameters.Depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.PanelSwitchboard:
                CreateSegmentedPanel(builder, width, height, depth, 4, true);
                break;
            case ElectricalComponentCatalog.Profiles.PanelMcc:
                CreateSegmentedPanel(builder, width, height, depth, 3, false);
                AddHorizontalCompartmentSeams(builder, width, height, depth, 5);
                break;
            case ElectricalComponentCatalog.Profiles.PanelLighting:
                builder.AddBox(new Point3D(0, height / 2, 0), width, height, depth);
                AddVerticalPanelSeams(builder, width, height, depth, 2);
                AddPanelHandle(builder, width, height, depth, -width * 0.18);
                break;
            default:
                builder.AddBox(new Point3D(0, height / 2, 0), width, height, depth);
                AddVerticalPanelSeams(builder, width, height, depth, 3);
                AddPanelHandle(builder, width, height, depth, width * 0.2);
                break;
        }
    }

    private static void CreateSegmentedPanel(MeshBuilder builder, double width, double height, double depth, int sections, bool addCenterBus)
    {
        var sectionWidth = width / sections;
        for (int i = 0; i < sections; i++)
        {
            var centerX = -width / 2 + sectionWidth * i + sectionWidth / 2;
            builder.AddBox(new Point3D(centerX, height / 2, 0), sectionWidth * 0.96, height, depth);
            AddPanelHandle(builder, width, height, depth, centerX + sectionWidth * 0.25);
        }

        if (addCenterBus)
        {
            builder.AddBox(new Point3D(0, height * 0.5, depth * 0.46), width * 0.04, height * 0.88, 0.2);
        }
    }

    private static void AddVerticalPanelSeams(MeshBuilder builder, double width, double height, double depth, int seamCount)
    {
        for (int i = 1; i <= seamCount; i++)
        {
            var x = -width / 2 + width * i / (seamCount + 1);
            builder.AddBox(new Point3D(x, height / 2, depth * 0.52), Math.Max(0.04, width * 0.01), height * 0.92, 0.08);
        }
    }

    private static void AddHorizontalCompartmentSeams(MeshBuilder builder, double width, double height, double depth, int seamCount)
    {
        for (int i = 1; i <= seamCount; i++)
        {
            var y = height * i / (seamCount + 1);
            builder.AddBox(new Point3D(0, y, depth * 0.52), width * 0.92, Math.Max(0.04, height * 0.008), 0.08);
        }
    }

    private static void AddPanelHandle(MeshBuilder builder, double width, double height, double depth, double xOffset)
    {
        var clampX = Math.Max(-width * 0.46, Math.Min(width * 0.46, xOffset));
        var y = height * 0.55;
        var z = depth * 0.54;
        builder.AddCylinder(
            new Point3D(clampX, y - height * 0.08, z),
            new Point3D(clampX, y + height * 0.08, z),
            Math.Max(0.04, width * 0.012),
            12);
    }

    private static void CreateSupportGeometry(MeshBuilder builder, SupportComponent support, string profile)
    {
        var width = Math.Max(0.08, support.Parameters.Width);
        var height = Math.Max(0.08, support.Parameters.Height);
        var length = Math.Max(0.2, support.Parameters.Depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.SupportTrapeze:
                var rodOffset = Math.Max(0.2, length * 0.35);
                builder.AddBox(new Point3D(0, 0, 0), length, Math.Max(0.08, width), Math.Max(0.08, width));
                builder.AddCylinder(
                    new Point3D(-rodOffset, 0, 0),
                    new Point3D(-rodOffset, Math.Max(1.2, height), 0),
                    Math.Max(0.04, width * 0.35),
                    12);
                builder.AddCylinder(
                    new Point3D(rodOffset, 0, 0),
                    new Point3D(rodOffset, Math.Max(1.2, height), 0),
                    Math.Max(0.04, width * 0.35),
                    12);
                break;

            case ElectricalComponentCatalog.Profiles.SupportWallBracket:
                var armLength = Math.Max(0.5, length);
                var armThickness = Math.Max(0.08, width * 0.5);
                builder.AddBox(new Point3D(0, height * 0.5, 0), armThickness, height, armLength);
                builder.AddBox(new Point3D(armLength * 0.5, armThickness * 0.5, 0), armLength, armThickness, armThickness);
                builder.AddCylinder(
                    new Point3D(-armThickness * 0.45, height * 0.4, -armLength * 0.2),
                    new Point3D(armThickness * 0.45, height * 0.4, -armLength * 0.2),
                    armThickness * 0.28,
                    10);
                break;

            default:
                CreateUnistrutGeometry(builder, width, height, length);
                break;
        }
    }

    private static void CreateUnistrutGeometry(MeshBuilder builder, double width, double height, double length)
    {
        var channelWidth = Math.Max(0.12, width);
        var channelHeight = Math.Max(0.12, height);
        var wall = Math.Max(0.015, Math.Min(channelWidth, channelHeight) * 0.18);
        var halfWidth = channelWidth / 2;
        var halfHeight = channelHeight / 2;

        builder.AddBox(new Point3D(-halfWidth + wall / 2, 0, 0), wall, channelHeight, length);
        builder.AddBox(new Point3D(0, halfHeight - wall / 2, 0), channelWidth, wall, length);
        builder.AddBox(new Point3D(0, -halfHeight + wall / 2, 0), channelWidth, wall, length);

        var slotSpacing = Math.Max(0.5, length / 6);
        var slotRadius = Math.Max(0.03, wall * 0.8);
        for (double z = -length / 2 + slotSpacing; z < length / 2 - slotSpacing / 2; z += slotSpacing)
        {
            builder.AddCylinder(
                new Point3D(-halfWidth - 0.01, 0, z),
                new Point3D(-halfWidth + wall + 0.01, 0, z),
                slotRadius,
                10);
        }
    }

    private static void CreateCableTrayGeometry(MeshBuilder builder, CableTrayComponent tray, string profile)
    {
        var points = tray.GetPathPoints();
        if (points.Count < 2)
        {
            points = new List<Point3D>
            {
                new Point3D(0, 0, 0),
                new Point3D(Math.Max(1.0, tray.Length), 0, 0)
            };
        }

        var trayWidth = Math.Max(1.0, tray.TrayWidth);
        var trayDepth = Math.Max(0.5, tray.TrayDepth);
        var railRadius = Math.Max(0.08, trayDepth * 0.12);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var dir = end - start;
            var length = dir.Length;
            if (length < 1e-4)
                continue;

            dir.Normalize();
            var side = new Vector3D(-dir.Z, 0, dir.X);
            if (side.Length < 1e-5)
                side = new Vector3D(1, 0, 0);
            else
                side.Normalize();

            var up = new Vector3D(0, 1, 0);
            var halfW = trayWidth / 2;
            var railYOffset = trayDepth * 0.45;
            var leftStart = start + side * halfW + up * railYOffset;
            var leftEnd = end + side * halfW + up * railYOffset;
            var rightStart = start - side * halfW + up * railYOffset;
            var rightEnd = end - side * halfW + up * railYOffset;

            builder.AddCylinder(leftStart, leftEnd, railRadius, 12);
            builder.AddCylinder(rightStart, rightEnd, railRadius, 12);

            switch (profile)
            {
                case ElectricalComponentCatalog.Profiles.TrayWireMesh:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, railYOffset, Math.Max(0.8, trayWidth * 0.3), railRadius * 0.65);
                    AddTrayLongitudinalWires(builder, start, end, side, up, railYOffset * 0.5, halfW, railRadius * 0.5);
                    break;
                case ElectricalComponentCatalog.Profiles.TraySolidBottom:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, 0.1, Math.Max(0.7, trayDepth * 0.35), railRadius * 0.75);
                    builder.AddCylinder(start + up * 0.05, end + up * 0.05, railRadius * 0.85, 10);
                    break;
                default:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, railYOffset, Math.Max(1.4, trayWidth * 0.5), railRadius * 0.75);
                    break;
            }
        }
    }

    private static void AddTrayRungs(MeshBuilder builder, Point3D segmentStart, Vector3D direction, Vector3D side, Vector3D up,
        double segmentLength, double halfWidth, double yOffset, double spacing, double radius)
    {
        for (double dist = spacing; dist < segmentLength; dist += spacing)
        {
            var center = segmentStart + direction * dist + up * yOffset;
            builder.AddCylinder(center - side * halfWidth, center + side * halfWidth, Math.Max(0.04, radius), 10);
        }
    }

    private static void AddTrayLongitudinalWires(MeshBuilder builder, Point3D start, Point3D end, Vector3D side, Vector3D up,
        double yOffset, double halfWidth, double radius)
    {
        var offsetA = side * (halfWidth * 0.35) + up * yOffset;
        var offsetB = side * (-halfWidth * 0.35) + up * yOffset;
        builder.AddCylinder(start + offsetA, end + offsetA, Math.Max(0.03, radius), 10);
        builder.AddCylinder(start + offsetB, end + offsetB, Math.Max(0.03, radius), 10);
    }

    private static void CreateHangerGeometry(MeshBuilder builder, HangerComponent hanger, string profile)
    {
        var rodDiameter = Math.Max(0.08, hanger.RodDiameter);
        var rodLength = Math.Max(0.5, hanger.RodLength);
        var start = new Point3D(0, 0, 0);
        var end = new Point3D(0, rodLength, 0);

        if (profile == ElectricalComponentCatalog.Profiles.HangerSeismicBrace)
        {
            var braceEnd = new Point3D(rodLength * 0.65, rodLength * 0.65, 0);
            builder.AddCylinder(start, braceEnd, rodDiameter * 0.9, 12);
            builder.AddBox(new Point3D(0, 0, 0), rodDiameter * 2.4, rodDiameter * 0.8, rodDiameter * 2.4);
            builder.AddBox(new Point3D(braceEnd.X, braceEnd.Y, braceEnd.Z), rodDiameter * 2.0, rodDiameter * 0.8, rodDiameter * 2.0);
        }
        else
        {
            builder.AddCylinder(start, end, rodDiameter, 12);
            var nutHeight = Math.Max(0.06, rodDiameter * 0.5);
            builder.AddBox(new Point3D(0, rodLength * 0.82, 0), rodDiameter * 1.8, nutHeight, rodDiameter * 1.8);
            builder.AddBox(new Point3D(0, rodLength * 0.22, 0), rodDiameter * 1.8, nutHeight, rodDiameter * 1.8);
        }
    }
    
    private List<Point3D> GenerateSmoothPath(List<Point3D> controlPoints, double bendRadius)
    {
        var smoothPoints = new List<Point3D>();
        
        if (controlPoints.Count < 2)
            return smoothPoints;
        
        int segmentResolution = Math.Min(MaxSegmentResolution, 
            Math.Max(MinSegmentResolution, (int)(bendRadius * ResolutionScaleFactor)));
        
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Point3D p0 = (i == 0) ? controlPoints[i] : controlPoints[i - 1];
            Point3D p1 = controlPoints[i];
            Point3D p2 = controlPoints[i + 1];
            Point3D p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : controlPoints[i + 1];
            
            for (int j = 0; j < segmentResolution; j++)
            {
                double t = (double)j / segmentResolution;
                Point3D interpolated = CatmullRomInterpolate(p0, p1, p2, p3, t);
                smoothPoints.Add(interpolated);
            }
        }
        
        smoothPoints.Add(controlPoints[controlPoints.Count - 1]);
        
        return smoothPoints;
    }
    
    private Point3D CatmullRomInterpolate(Point3D p0, Point3D p1, Point3D p2, Point3D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        
        double c0 = -0.5 * t3 + t2 - 0.5 * t;
        double c1 = 1.5 * t3 - 2.5 * t2 + 1.0;
        double c2 = -1.5 * t3 + 2.0 * t2 + 0.5 * t;
        double c3 = 0.5 * t3 - 0.5 * t2;
        
        double x = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
        double y = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
        double z = c0 * p0.Z + c1 * p1.Z + c2 * p2.Z + c3 * p3.Z;
        
        return new Point3D(x, y, z);
    }
    
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

    /// <summary>
    /// Computes and sets PdfUnderlay.Scale so the image fills the canvas (2000×2000) with a 5 % margin.
    /// Also resets the canvas zoom (PlanCanvasScale) to 1 so the full drawing is visible.
    /// </summary>
    private void AutoFitPdfScale(PdfUnderlay underlay, string filePath)
    {
        const double canvasSize = 2000.0;
        const double margin     = 0.95; // leave 5 % breathing room

        try
        {
            int imgWidth, imgHeight;
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                var bytes = System.IO.File.ReadAllBytes(filePath);
                var size  = PDFtoImage.Conversion.GetPageSize(bytes, page: Math.Max(0, underlay.PageNumber - 1));
                // GetPageSize returns PDF points (72 dpi). PDFtoImage.Conversion.ToImage() renders at 300 dpi,
                // so actual pixel dimensions = PDF points × (300 / 72).
                const double renderDpi = 300.0;
                imgWidth  = (int)(size.Width  * renderDpi / 72.0);
                imgHeight = (int)(size.Height * renderDpi / 72.0);
            }
            else
            {
                // For images, decode just the pixel dimensions without loading the full bitmap
                using var fs = System.IO.File.OpenRead(filePath);
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    fs,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                imgWidth  = frame.PixelWidth;
                imgHeight = frame.PixelHeight;
            }

            if (imgWidth <= 0 || imgHeight <= 0) return;

            // Scale that fits the larger dimension within the canvas
            double fitScale = canvasSize * margin / Math.Max(imgWidth, imgHeight);
            underlay.Scale = Math.Round(fitScale, 6);

            // Reset canvas zoom so the whole thing is visible immediately
            PlanCanvasScale.ScaleX = 1.0;
            PlanCanvasScale.ScaleY = 1.0;

            ActionLogService.Instance.Log(LogCategory.View, "PDF auto-fitted",
                $"Image: {imgWidth}x{imgHeight} pts, FitScale: {underlay.Scale:F6}");
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "AutoFitPdfScale failed", ex.Message);
            // Leave scale at default (1.0) — not a fatal error
        }
    }

    private void InvalidatePdfCache()
    {
        _cachedPdfBitmap = null;
        _cachedPdfPath = null;
        _cachedPdfPage = -1;
    }

    private BitmapSource? GetUnderlayBitmap()
    {
        if (_viewModel.PdfUnderlay == null || string.IsNullOrEmpty(_viewModel.PdfUnderlay.FilePath))
            return null;

        if (!System.IO.File.Exists(_viewModel.PdfUnderlay.FilePath))
            return null;

        var filePath = _viewModel.PdfUnderlay.FilePath;
        var pageNumber = Math.Max(0, _viewModel.PdfUnderlay.PageNumber - 1);

        // Use cached bitmap if the file and page haven't changed.
        if (_cachedPdfBitmap != null && _cachedPdfPath == filePath && _cachedPdfPage == pageNumber)
            return _cachedPdfBitmap;

        BitmapSource? bitmap = null;
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var pdfBytes = System.IO.File.ReadAllBytes(filePath);
            using var skBitmap = Conversion.ToImage(pdfBytes, page: pageNumber);
            using var image = skBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var memStream = new System.IO.MemoryStream();

            image.SaveTo(memStream);
            memStream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            bitmap = bitmapImage;
        }
        else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            bitmap = bitmapImage;
        }

        _cachedPdfBitmap = bitmap;
        _cachedPdfPath = filePath;
        _cachedPdfPage = pageNumber;
        return bitmap;
    }

    private static Point RotateCanvasPoint(Point point, double angleDegrees)
    {
        if (Math.Abs(angleDegrees) < 0.001)
            return point;

        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        return new Point(
            (point.X * cos) - (point.Y * sin),
            (point.X * sin) + (point.Y * cos));
    }

    private static Point[] BuildUnderlayCanvasCorners(PdfUnderlay underlay, double scaledWidth, double scaledHeight)
    {
        var localTopLeft = new Point(0, 0);
        var localTopRight = new Point(scaledWidth, 0);
        var localBottomRight = new Point(scaledWidth, scaledHeight);
        var localBottomLeft = new Point(0, scaledHeight);

        var topLeftCanvas = RotateCanvasPoint(localTopLeft, underlay.RotationDegrees);
        topLeftCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var topRightCanvas = RotateCanvasPoint(localTopRight, underlay.RotationDegrees);
        topRightCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var bottomRightCanvas = RotateCanvasPoint(localBottomRight, underlay.RotationDegrees);
        bottomRightCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var bottomLeftCanvas = RotateCanvasPoint(localBottomLeft, underlay.RotationDegrees);
        bottomLeftCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        return
        [
            topLeftCanvas,
            topRightCanvas,
            bottomRightCanvas,
            bottomLeftCanvas
        ];
    }

    private bool TryGetUnderlayCanvasFrame(out UnderlayCanvasFrame frame)
    {
        frame = default;

        var underlay = _viewModel.PdfUnderlay;
        if (underlay == null)
            return false;

        var bitmap = GetUnderlayBitmap();
        if (bitmap == null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
            return false;

        var scaledWidth = bitmap.PixelWidth * underlay.Scale;
        var scaledHeight = bitmap.PixelHeight * underlay.Scale;
        if (scaledWidth <= 0 || scaledHeight <= 0)
            return false;

        frame = new UnderlayCanvasFrame(
            underlay,
            scaledWidth,
            scaledHeight,
            BuildUnderlayCanvasCorners(underlay, scaledWidth, scaledHeight));

        return true;
    }

    private void AddPdfUnderlayToViewport()
    {
        try
        {
            if (!TryGetUnderlayCanvasFrame(out var frame))
                return;

            var bitmap = GetUnderlayBitmap();
            if (bitmap == null)
                return;

            var topLeftWorld = CanvasToWorld(frame.Corners[0]);
            topLeftWorld.Y = PdfUnderlayPlaneY;
            var topRightWorld = CanvasToWorld(frame.Corners[1]);
            topRightWorld.Y = PdfUnderlayPlaneY;
            var bottomRightWorld = CanvasToWorld(frame.Corners[2]);
            bottomRightWorld.Y = PdfUnderlayPlaneY;
            var bottomLeftWorld = CanvasToWorld(frame.Corners[3]);
            bottomLeftWorld.Y = PdfUnderlayPlaneY;

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    topLeftWorld,
                    topRightWorld,
                    bottomRightWorld,
                    bottomLeftWorld
                },
                TextureCoordinates = new PointCollection
                {
                    new Point(0, 0),
                    new Point(1, 0),
                    new Point(1, 1),
                    new Point(0, 1)
                },
                TriangleIndices = new Int32Collection
                {
                    0, 1, 2,
                    0, 2, 3
                }
            };

            var brush = new ImageBrush(bitmap)
            {
                Opacity = frame.Underlay.Opacity,
                Stretch = Stretch.Fill
            };
            var material = new DiffuseMaterial(brush);

            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };

            _pdfUnderlayVisual3D = new ModelVisual3D
            {
                Content = model
            };

            Viewport.Children.Add(_pdfUnderlayVisual3D);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Invalid page number for 3D PDF underlay",
                $"Page {_viewModel.PdfUnderlay?.PageNumber ?? 1} does not exist in the document. {ex.Message}");
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Failed to render 3D PDF/Image underlay", ex.Message);
        }
    }

    private void DrawPdfUnderlay()
    {
        if (_viewModel.PdfUnderlay == null)
            return;

        try
        {
            var bitmap = GetUnderlayBitmap();
            
            if (bitmap != null)
            {
                // Create WPF Image element
                var image = new Image
                {
                    Source = bitmap,
                    Opacity = _viewModel.PdfUnderlay.Opacity,
                    Stretch = Stretch.None,
                    // Origin (0,0) = scale/rotate from the top-left corner so the image
                    // stays anchored at Canvas.SetLeft/Top and doesn't drift off-screen.
                    RenderTransformOrigin = new Point(0, 0)
                };
                
                // Apply transformations (scale first, then rotation)
                var transformGroup = new TransformGroup();
                
                // Apply scale first
                var scale = _viewModel.PdfUnderlay.Scale;
                transformGroup.Children.Add(new ScaleTransform(scale, scale));
                
                // Apply rotation after scale if specified
                if (_viewModel.PdfUnderlay.RotationDegrees != 0)
                {
                    transformGroup.Children.Add(new RotateTransform(_viewModel.PdfUnderlay.RotationDegrees));
                }
                
                image.RenderTransform = transformGroup;
                
                // Position the image using offsets
                Canvas.SetLeft(image, _viewModel.PdfUnderlay.OffsetX);
                Canvas.SetTop(image, _viewModel.PdfUnderlay.OffsetY);
                
                // Add to canvas at the beginning (so it appears behind everything)
                PlanCanvas.Children.Insert(0, image);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // Page number is out of range
            ActionLogService.Instance.Log(LogCategory.Error, "Invalid page number for PDF underlay", 
                $"Page {_viewModel.PdfUnderlay.PageNumber} does not exist in the document. {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            ActionLogService.Instance.Log(LogCategory.Error, "Failed to render PDF/Image underlay", ex.Message);
        }
    }

    
    private void UpdatePlanCanvasBackground()
    {
        if (!_viewModel.ShowGrid)
        {
            PlanCanvas.Background = Brushes.White;
            return;
        }

        var gridSizePx = Math.Max(4.0, _viewModel.GridSize * 20.0);
        if (_cachedGridBrush == null || Math.Abs(_cachedGridSizePx - gridSizePx) > 0.001)
        {
            _cachedGridBrush = CreateGridBrush(gridSizePx);
            _cachedGridSizePx = gridSizePx;
        }

        PlanCanvas.Background = _cachedGridBrush != null ? _cachedGridBrush : Brushes.White;
    }

    private static DrawingBrush CreateGridBrush(double gridSizePx)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(232, 232, 232)), 0.6);
        pen.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, 0), new Point(gridSizePx, 0))));
        group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, 0), new Point(0, gridSizePx))));
        group.Freeze();

        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, gridSizePx, gridSizePx),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, gridSizePx, gridSizePx),
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };

        System.Windows.Media.RenderOptions.SetCachingHint(brush, CachingHint.Cache);
        System.Windows.Media.RenderOptions.SetCacheInvalidationThresholdMinimum(brush, 0.5);
        System.Windows.Media.RenderOptions.SetCacheInvalidationThresholdMaximum(brush, 2.0);
        brush.Freeze();
        return brush;
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
        var isSelected = component == _viewModel.SelectedComponent;

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

    private void AddComponentWithUndo(ElectricalComponent component)
    {
        if (_viewModel.ActiveLayer != null)
            component.LayerId = _viewModel.ActiveLayer.Id;

        var action = new AddComponentAction(_viewModel.Components, component);
        _viewModel.UndoRedo.Execute(action);
        _viewModel.SelectedComponent = component;
    }

    private void UpdatePlanCanvasCursor()
    {
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

    private void ClearSketchSelection()
    {
        _selectedSketchPrimitive = null;
        Update2DCanvas();
    }
    
    // ===== 2D Canvas mouse handlers =====
    
    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);

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

        // --- Sketch line tool ---
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

        // --- Sketch rectangle tool ---
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
        
        // --- Conduit drawing mode: place a vertex ---
        if (_isDrawingConduit)
        {
            // Double-click finishes the conduit
            if (e.ClickCount == 2)
            {
                FinishDrawingConduit();
                e.Handled = true;
                return;
            }
            
            var snapped = ApplyDrawingSnap(pos);
            
            // If Shift is held, constrain to orthogonal angles
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
            
            // Redraw canvas to show committed segments
            Update2DCanvas();
            DrawConduitPreview();
            e.Handled = true;
            return;
        }

        // --- Conduit edit mode in 2D: drag existing bend points or tap segments to add one ---
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

        // --- Default mode: select / drag ---
        var hit = PlanCanvas.InputHitTest(pos) as FrameworkElement;
        if (hit != null && _canvasToComponentMap.ContainsKey(hit))
        {
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
            _selectedSketchPrimitive = hitSketch;
            Update2DCanvas();
            e.Handled = true;
            return;
        }

        // Conduit paths are rendered via DrawingVisual (not UIElement), so hit-test manually.
        if (TryHitConduitPath2D(pos, out var hitConduit) && hitConduit != null)
        {
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
        
        PlanCanvas.CaptureMouse();
    }
    
    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);

        if (HandleFreehandMouseMove(pos))
        {
            e.Handled = true;
            return;
        }

        if (_pendingPlacementComponent != null)
        {
            var snapped = ClampCanvasToPlanBounds(ApplyDrawingSnap(pos));
            UpdateSnapIndicator(snapped, pos);
            e.Handled = true;
            return;
        }

        if (_isSketchLineMode && _sketchDraftLinePoints.Count > 0)
        {
            var snapped = ApplyDrawingSnap(pos);
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                snapped = ConstrainToAngle(_sketchDraftLinePoints[^1], snapped);

            UpdateSketchLineRubberBand(_sketchDraftLinePoints[^1], snapped);
            UpdateSnapIndicator(snapped, pos);
            return;
        }

        if (_isSketchRectangleMode && _isSketchRectangleDragging)
        {
            _lastMousePosition = ApplyDrawingSnap(pos);
            Update2DCanvas();
            return;
        }
        
        // --- Conduit drawing mode: update rubber-band line ---
        if (_isDrawingConduit && _drawingCanvasPoints.Count > 0)
        {
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

        // --- Bend point drag mode ---
        if (_isDraggingConduitBend2D && _draggingConduit2D != null)
        {
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
                _draggingConduit2D.BendPoints[_draggingConduitBendIndex2D] = relativePoint;
                ConstrainConduitPathToPlanBounds(_draggingConduit2D);
                QueueSceneRefresh(update2D: true, update3D: true);
            }

            return;
        }
        
        // --- Drag mode ---
        if (_isDragging2D && _draggedElement2D != null && _viewModel.SelectedComponent != null)
        {
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
        _isDragging2D = false;
        _draggedElement2D = null;
        _mobileSelectionCandidate = false;
        PlanCanvas.ReleaseMouseCapture();
    }
    
    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        BeginFastInteractionMode();

        var oldScale = PlanCanvasScale.ScaleX;
        var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
        var newScale = Math.Max(0.1, Math.Min(10.0, oldScale * zoomFactor));
        if (Math.Abs(newScale - oldScale) < 0.0001)
            return;

        var cursorInScroll = e.GetPosition(PlanScrollViewer);
        var absoluteX = (cursorInScroll.X + PlanScrollViewer.HorizontalOffset) / oldScale;
        var absoluteY = (cursorInScroll.Y + PlanScrollViewer.VerticalOffset) / oldScale;

        PlanCanvasScale.ScaleX = newScale;
        PlanCanvasScale.ScaleY = newScale;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            PlanScrollViewer.ScrollToHorizontalOffset(absoluteX * newScale - cursorInScroll.X);
            PlanScrollViewer.ScrollToVerticalOffset(absoluteY * newScale - cursorInScroll.Y);
        }));
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
    
    // ===== Conduit Drawing Tool (Bluebeam-style polyline) =====
    
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
        
        // Cancel any other editing mode
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
            // Create the conduit component from the placed vertices
            var conduit = new ConduitComponent
            {
                VisualProfile = ElectricalComponentCatalog.Profiles.ConduitEmt
            };
            
            if (_viewModel.ActiveLayer != null)
                conduit.LayerId = _viewModel.ActiveLayer.Id;
            
            // First point becomes the component position (world coords)
            var firstPt = _drawingCanvasPoints[0];
            conduit.Position = ClampWorldToPlanBounds(CanvasToWorld(firstPt));
            
            // Remaining points become bend points (relative to position)
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
        
        // Reset drawing state
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
        
        // Draw vertex dots
        foreach (var pt in _drawingCanvasPoints)
        {
            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = Brushes.DodgerBlue,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, pt.X - 4);
            Canvas.SetTop(dot, pt.Y - 4);
            PlanCanvas.Children.Add(dot);
        }
    }
    
    /// <summary>
    /// Updates the rubber-band line from the last placed vertex to the current cursor position.
    /// </summary>
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
    
    /// <summary>
    /// Shows a small circle at the snap target when the cursor snaps to a grid point or existing vertex.
    /// </summary>
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
                Width = 12, Height = 12,
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
    
    /// <summary>
    /// Applies grid snapping and component endpoint snapping to a raw canvas position.
    /// </summary>
    private Point ApplyDrawingSnap(Point canvasPos)
    {
        // Use prebuilt scene snap data plus active tool vertices.
        var endpoints = _drawingCanvasPoints.Count == 0
            ? _snapEndpointsCache
            : _snapEndpointsCache.Concat(_drawingCanvasPoints);

        // Try endpoint/midpoint/intersection snap first.
        var snapResult = _viewModel.SnapService.FindSnapPoint(canvasPos, endpoints, _snapSegmentsCache);
        if (snapResult.Snapped)
            return snapResult.SnappedPoint;
        
        // Fall back to grid snap
        if (_viewModel.SnapToGrid)
        {
            double gridPx = _viewModel.GridSize * 20;
            double snappedX = Math.Round(canvasPos.X / gridPx) * gridPx;
            double snappedY = Math.Round(canvasPos.Y / gridPx) * gridPx;
            return new Point(snappedX, snappedY);
        }
        
        return canvasPos;
    }
    
    /// <summary>
    /// Constrains the target point to the nearest 45-degree increment from the anchor (Shift modifier).
    /// Snaps to 0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°.
    /// </summary>
    private static Point ConstrainToAngle(Point anchor, Point target)
    {
        double dx = target.X - anchor.X;
        double dy = target.Y - anchor.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return target;
        
        double angle = Math.Atan2(dy, dx);
        // Round to nearest 45° (π/4)
        double snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
        
        return new Point(
            anchor.X + dist * Math.Cos(snapped),
            anchor.Y + dist * Math.Sin(snapped));
    }
    
    /// <summary>
    /// Converts a canvas pixel position to world coordinates (matching Draw2DComponent's inverse).
    /// </summary>
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

    private static void UpdateConduitLengthFromPath(ConduitComponent conduit)
    {
        var points = conduit.GetPathPoints();
        double total = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            total += (points[i + 1] - points[i]).Length;
        }

        conduit.Length = total;
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
    
    // ===== Component Add Handlers =====

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
        unistrut.Parameters.Width = 0.135;  // ~1-5/8 in
        unistrut.Parameters.Height = 0.135; // ~1-5/8 in
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
    
    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedComponent();
    }
    
    private void LibraryItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
        if (!_isMobileView) return;

        SetMobilePane(MobilePane.Canvas);
        ViewTabs.SelectedIndex = 1;
        Update2DCanvas();
    }
    
    // ===== Undo/Redo =====
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Undo();
        UpdateViewport();
        Update2DCanvas();
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Redo();
        UpdateViewport();
        Update2DCanvas();
    }
    
    // ===== Layer Management =====
    
    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var name = $"Layer {_viewModel.Layers.Count + 1}";
        _viewModel.AddLayer(name);
    }
    
    private void RemoveLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            _viewModel.RemoveLayer(layer);
        }
    }
    
    private void LayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            ActionLogService.Instance.Log(LogCategory.Layer, "Active layer changed",
                $"Name: {layer.Name}, Id: {layer.Id}");
            _viewModel.ActiveLayer = layer;
        }
    }
    
    // ===== Unit System =====
    
    private void UnitSystem_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (UnitSystemCombo?.SelectedItem is ComboBoxItem item)
        {
            var system = item.Content?.ToString() ?? "Imperial";
            ActionLogService.Instance.Log(LogCategory.View, "Unit system changed", $"System: {system}");
            _viewModel.UnitSystemName = system;
        }
    }
    
    // ===== View Switching =====

    private void ShowLibraryPanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _showDesktopLibraryPanel = ShowLibraryPanelMenuItem.IsChecked;
        if (_isMobileView) return;

        ApplyDesktopPaneLayout();
        ActionLogService.Instance.Log(LogCategory.View, "Left panel visibility changed",
            $"Visible: {_showDesktopLibraryPanel}");
    }

    private void ShowPropertiesPanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _showDesktopPropertiesPanel = ShowPropertiesPanelMenuItem.IsChecked;
        if (_isMobileView) return;

        ApplyDesktopPaneLayout();
        ActionLogService.Instance.Log(LogCategory.View, "Right panel visibility changed",
            $"Visible: {_showDesktopPropertiesPanel}");
    }

    private void LibraryGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isMobileView || !_showDesktopLibraryPanel)
            return;

        if (LibraryColumn.Width.Value <= 0)
            return;

        _desktopLibraryColumnWidth = LibraryColumn.Width;
        ActionLogService.Instance.Log(LogCategory.View, "Left panel resized",
            $"Width: {_desktopLibraryColumnWidth.Value:F0}");
    }

    private void PropertiesGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isMobileView || !_showDesktopPropertiesPanel)
            return;

        if (PropertiesColumn.Width.Value <= 0)
            return;

        _desktopPropertiesColumnWidth = PropertiesColumn.Width;
        ActionLogService.Instance.Log(LogCategory.View, "Right panel resized",
            $"Width: {_desktopPropertiesColumnWidth.Value:F0}");
    }

    private void ApplyDesktopPaneLayout()
    {
        if (_isMobileView)
            return;

        ViewportPanelContainer.Visibility = Visibility.Visible;
        ViewportColumn.Width = EnsureVisibleColumnWidth(_desktopViewportColumnWidth, 1, GridUnitType.Star);

        if (_showDesktopLibraryPanel)
        {
            LibraryPanelContainer.Visibility = Visibility.Visible;
            LibraryColumn.Width = EnsureVisibleColumnWidth(_desktopLibraryColumnWidth, 200);
        }
        else
        {
            if (LibraryColumn.Width.Value > 0)
                _desktopLibraryColumnWidth = LibraryColumn.Width;

            LibraryPanelContainer.Visibility = Visibility.Collapsed;
            LibraryColumn.Width = new GridLength(0);
        }

        if (_showDesktopPropertiesPanel)
        {
            PropertiesPanelContainer.Visibility = Visibility.Visible;
            PropertiesColumn.Width = EnsureVisibleColumnWidth(_desktopPropertiesColumnWidth, 300);
        }
        else
        {
            if (PropertiesColumn.Width.Value > 0)
                _desktopPropertiesColumnWidth = PropertiesColumn.Width;

            PropertiesPanelContainer.Visibility = Visibility.Collapsed;
            PropertiesColumn.Width = new GridLength(0);
        }

        LibraryGridSplitter.Visibility = _showDesktopLibraryPanel ? Visibility.Visible : Visibility.Collapsed;
        PropertiesGridSplitter.Visibility = _showDesktopPropertiesPanel ? Visibility.Visible : Visibility.Collapsed;
        ShowLibraryPanelMenuItem.IsChecked = _showDesktopLibraryPanel;
        ShowPropertiesPanelMenuItem.IsChecked = _showDesktopPropertiesPanel;
    }

    private static GridLength EnsureVisibleColumnWidth(GridLength width, double fallbackValue, GridUnitType fallbackUnit = GridUnitType.Pixel)
    {
        if (width.Value > 0)
            return width;

        return new GridLength(fallbackValue, fallbackUnit);
    }
    
    private void Show2DView_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Switched to 2D Plan View");
        ViewTabs.SelectedIndex = 1;
        Update2DCanvas();
        if (_isMobileView)
        {
            SetMobilePane(MobilePane.Canvas);
        }
    }
    
    private void Show3DView_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Switched to 3D Viewport");
        ViewTabs.SelectedIndex = 0;
        if (_isMobileView)
        {
            SetMobilePane(MobilePane.Canvas);
            MobileSectionTitleText.Text = "Plan (3D)";
        }
    }

    private void MobileViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMobileView)
        {
            _desktopWindowState = WindowState;
            _desktopWidth = Width;
            _desktopHeight = Height;
            if (_showDesktopLibraryPanel && LibraryColumn.Width.Value > 0)
                _desktopLibraryColumnWidth = LibraryColumn.Width;
            if (ViewportColumn.Width.Value > 0)
                _desktopViewportColumnWidth = ViewportColumn.Width;
            if (_showDesktopPropertiesPanel && PropertiesColumn.Width.Value > 0)
                _desktopPropertiesColumnWidth = PropertiesColumn.Width;

            WindowState = WindowState.Normal;
            Width = MobileWindowWidth;
            Height = MobileWindowHeight;

            TopMenu.Visibility = Visibility.Collapsed;
            DesktopToolBar.Visibility = Visibility.Collapsed;
            MobileTopBar.Visibility = Visibility.Visible;
            MobileBottomNav.Visibility = Visibility.Visible;
            PdfControlsPanel.Visibility = Visibility.Collapsed;

            _isMobileView = true;
            ApplyMobileTheme();
            ViewTabs.SelectedIndex = 1;
            Update2DCanvas();
            SetMobilePane(MobilePane.Canvas);

            MobileViewButton.Content = "Desktop View";
            ActionLogService.Instance.Log(LogCategory.View, "Mobile view enabled", $"Window size set to {MobileWindowWidth}x{MobileWindowHeight}");
        }
        else
        {
            TopMenu.Visibility = Visibility.Visible;
            DesktopToolBar.Visibility = Visibility.Visible;
            MobileTopBar.Visibility = Visibility.Collapsed;
            MobileBottomNav.Visibility = Visibility.Collapsed;
            PdfControlsPanel.Visibility = Visibility.Visible;
            MainContentGrid.Background = Brushes.Transparent;

            Width = _desktopWidth;
            Height = _desktopHeight;
            WindowState = _desktopWindowState;

            MobileViewButton.Content = "Mobile View";
            _isMobileView = false;
            ApplyDesktopPaneLayout();
            ActionLogService.Instance.Log(LogCategory.View, "Mobile view disabled");
        }
    }

    private void MobileThemeIosMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMobileTheme(MobileTheme.IOS);
    }

    private void MobileThemeAndroidMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMobileTheme(MobileTheme.AndroidMaterial);
    }

    private void MobileCanvasButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Canvas);
    }

    private void MobileLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Library);
    }

    private void MobilePropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        SetMobilePane(MobilePane.Properties);
    }

    private void MobileMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
    }

    private void MobileAddTopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            OpenContextMenuFromButton(button, PlacementMode.Bottom);
        }
    }

    private static void OpenContextMenuFromButton(Button button, PlacementMode placementMode)
    {
        if (button.ContextMenu == null) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = placementMode;
        button.ContextMenu.IsOpen = true;
    }

    private void SetMobileTheme(MobileTheme theme)
    {
        _mobileTheme = theme;
        MobileThemeIosMenuItem.IsChecked = theme == MobileTheme.IOS;
        MobileThemeAndroidMenuItem.IsChecked = theme == MobileTheme.AndroidMaterial;
        ApplyMobileTheme();
    }

    private void ApplyMobileTheme()
    {
        bool isIOS = _mobileTheme == MobileTheme.IOS;
        var primary = new SolidColorBrush(isIOS ? Color.FromRgb(0, 122, 255) : Color.FromRgb(26, 115, 232));
        var border = new SolidColorBrush(isIOS ? Color.FromRgb(198, 198, 200) : Color.FromRgb(218, 220, 224));
        var altSurface = new SolidColorBrush(isIOS ? Color.FromRgb(242, 242, 247) : Color.FromRgb(250, 250, 250));

        MobileTopBar.Background = Brushes.White;
        MobileTopBar.BorderBrush = border;
        MobileBottomNav.Background = Brushes.White;
        MobileBottomNav.BorderBrush = border;
        if (_isMobileView)
        {
            MainContentGrid.Background = altSurface;
        }

        MobileTopBarGrid.Height = isIOS ? 52 : 56;
        MobileSectionTitleText.FontSize = isIOS ? 17 : 18;
        MobileSectionTitleText.FontWeight = isIOS ? FontWeights.SemiBold : FontWeights.Medium;

        MobileUndoButton.Foreground = primary;
        MobileRedoButton.Foreground = primary;
        MobileAddTopButton.Foreground = primary;
        MobileMoreButton.Foreground = primary;
        MobileUndoButton.Content = isIOS ? "Undo" : "Undo";
        MobileRedoButton.Content = isIOS ? "Redo" : "Redo";
        MobileAddTopButton.Content = isIOS ? "Add" : "+ Add";
        MobileMoreButton.Content = isIOS ? "More" : "Menu";

        MobileCanvasButton.Content = isIOS ? "Canvas\nPlan" : "Canvas";
        MobileLibraryButton.Content = isIOS ? "Library\nParts" : "Library";
        MobilePropertiesButton.Content = isIOS ? "Properties\nEdit" : "Properties";

        UpdateMobileNavigationVisuals();
    }

    private void SetMobilePane(MobilePane pane)
    {
        _activeMobilePane = pane;
        if (!_isMobileView) return;
        LibraryGridSplitter.Visibility = Visibility.Collapsed;
        PropertiesGridSplitter.Visibility = Visibility.Collapsed;

        switch (_activeMobilePane)
        {
            case MobilePane.Canvas:
                LibraryPanelContainer.Visibility = Visibility.Collapsed;
                ViewportPanelContainer.Visibility = Visibility.Visible;
                PropertiesPanelContainer.Visibility = Visibility.Collapsed;
                LibraryColumn.Width = new GridLength(0);
                ViewportColumn.Width = new GridLength(1, GridUnitType.Star);
                PropertiesColumn.Width = new GridLength(0);
                MobileAddTopButton.Visibility = Visibility.Visible;
                MobileSectionTitleText.Text = "Plan";
                ViewTabs.SelectedIndex = 1;
                Update2DCanvas();
                break;

            case MobilePane.Library:
                LibraryPanelContainer.Visibility = Visibility.Visible;
                ViewportPanelContainer.Visibility = Visibility.Collapsed;
                PropertiesPanelContainer.Visibility = Visibility.Collapsed;
                LibraryColumn.Width = new GridLength(1, GridUnitType.Star);
                ViewportColumn.Width = new GridLength(0);
                PropertiesColumn.Width = new GridLength(0);
                MobileAddTopButton.Visibility = Visibility.Visible;
                MobileSectionTitleText.Text = "Library";
                break;

            case MobilePane.Properties:
                LibraryPanelContainer.Visibility = Visibility.Collapsed;
                ViewportPanelContainer.Visibility = Visibility.Collapsed;
                PropertiesPanelContainer.Visibility = Visibility.Visible;
                LibraryColumn.Width = new GridLength(0);
                ViewportColumn.Width = new GridLength(0);
                PropertiesColumn.Width = new GridLength(1, GridUnitType.Star);
                MobileAddTopButton.Visibility = Visibility.Collapsed;
                MobileSectionTitleText.Text = "Properties";
                break;
        }

        UpdateMobileNavigationVisuals();
    }

    private void UpdateMobileNavigationVisuals()
    {
        bool isIOS = _mobileTheme == MobileTheme.IOS;
        var selectedBrush = isIOS ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(232, 240, 254));
        var selectedText = isIOS ? new SolidColorBrush(Color.FromRgb(0, 122, 255)) : new SolidColorBrush(Color.FromRgb(26, 115, 232));
        var defaultBrush = Brushes.Transparent;
        var defaultText = isIOS ? new SolidColorBrush(Color.FromRgb(142, 142, 147)) : new SolidColorBrush(Color.FromRgb(95, 99, 104));

        MobileCanvasButton.Background = _activeMobilePane == MobilePane.Canvas ? selectedBrush : defaultBrush;
        MobileLibraryButton.Background = _activeMobilePane == MobilePane.Library ? selectedBrush : defaultBrush;
        MobilePropertiesButton.Background = _activeMobilePane == MobilePane.Properties ? selectedBrush : defaultBrush;
        MobileCanvasButton.Foreground = _activeMobilePane == MobilePane.Canvas ? selectedText : defaultText;
        MobileLibraryButton.Foreground = _activeMobilePane == MobilePane.Library ? selectedText : defaultText;
        MobilePropertiesButton.Foreground = _activeMobilePane == MobilePane.Properties ? selectedText : defaultText;
        MobileCanvasButton.FontWeight = _activeMobilePane == MobilePane.Canvas ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
        MobileLibraryButton.FontWeight = _activeMobilePane == MobilePane.Library ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
        MobilePropertiesButton.FontWeight = _activeMobilePane == MobilePane.Properties ? FontWeights.SemiBold : (isIOS ? FontWeights.Normal : FontWeights.Medium);
    }
    
    // ===== PDF Underlay =====
    
    private void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Import PDF dialog requested");
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = "Import PDF/Image Underlay"
        };
        
        if (dialog.ShowDialog() == true)
        {
            var underlay = new PdfUnderlay
            {
                FilePath = dialog.FileName,
                Opacity  = PdfOpacitySlider.Value,
                IsLocked = PdfLockCheck.IsChecked ?? true
            };

            // Auto-fit: compute scale so the PDF fills the canvas (2000×2000) with a small margin
            AutoFitPdfScale(underlay, dialog.FileName);

            _viewModel.PdfUnderlay = underlay;
            ActionLogService.Instance.Log(LogCategory.FileOperation, "PDF imported",
                $"File: {dialog.FileName}, FitScale: {underlay.Scale:F4}");
            
            // Render the PDF/Image on the canvas
            Update2DCanvas();
            UpdateViewport();
            
            MessageBox.Show($"Underlay imported: {System.IO.Path.GetFileName(dialog.FileName)}\n" +
                $"Auto-fitted to canvas (scale {underlay.Scale:F4}).\n" +
                "Use 'Calibrate Scale' to set the real-world drawing scale.", 
                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void PdfOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            ActionLogService.Instance.Log(LogCategory.View, "PDF opacity changed", $"Value: {e.NewValue:F2}");
            _viewModel.PdfUnderlay.Opacity = e.NewValue;
            Update2DCanvas();
            UpdateViewport();
        }
    }
    
    private void PdfLock_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            var locked = PdfLockCheck.IsChecked ?? true;
            ActionLogService.Instance.Log(LogCategory.View, "PDF lock changed", $"Locked: {locked}");
            _viewModel.PdfUnderlay.IsLocked = locked;
        }
    }
    
    private void CalibrateScale_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Calibrate scale requested");
        if (_viewModel.PdfUnderlay == null)
        {
            MessageBox.Show("Please import a PDF underlay first.", "No Underlay", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        MessageBox.Show("PDF Scale Calibration:\n\n" +
            "1. Click two known points on the PDF\n" +
            "2. Enter the real-world distance between them\n\n" +
            "This feature requires picking two points on the 2D canvas.",
            "Calibrate Scale", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    // ===== 3D Viewport interaction =====
    
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position)?
            .Where(hit => !ReferenceEquals(hit.Visual, _pdfUnderlayVisual3D))
            .ToList();

        if (_pendingPlacementComponent != null)
        {
            var hitPoint = hits?.FirstOrDefault()?.Position ?? new Point3D(0, 0, 0);
            if (TryPlacePendingComponentAtWorld(hitPoint))
            {
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
        
        _viewModel.SelectedComponent = matchedComponent;
        
        if (_isEditingConduitPath && matchedComponent is ConduitComponent)
        {
            EnsureConduitHasEditableEndPoint((ConduitComponent)matchedComponent);
            ShowBendPointHandles();
            Update2DCanvas();
        }
        else if (_isMobileView && matchedComponent != null)
        {
            SetMobilePane(MobilePane.Properties);
        }
    }
    
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedHandle == null || _viewModel.SelectedComponent is not ConduitComponent conduit)
            return;
        
        var position = e.GetPosition(Viewport);
        
        int handleIndex = _bendPointHandles.IndexOf(_draggedHandle);
        if (handleIndex >= 0)
        {
            if (handleIndex < conduit.BendPoints.Count)
            {
                var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position);
                var filteredHits = hits?
                    .Where(hit => !ReferenceEquals(hit.Visual, _pdfUnderlayVisual3D))
                    .ToList();
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
                    
                    conduit.BendPoints[handleIndex] = newPoint;
                    ConstrainConduitPathToPlanBounds(conduit);
                    UpdateViewport();
                    Update2DCanvas();
                    UpdatePropertiesPanel();
                    ShowBendPointHandles();
                }
            }
        }
        
        _lastMousePosition = position;
    }
    
    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
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
                UpdateViewport();
                UpdatePropertiesPanel();
                
                if (_isEditingConduitPath)
                {
                    ShowBendPointHandles();
                }
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
            {
                ShowBendPointHandles();
            }
        }
        else
        {
            MessageBox.Show("No bend points to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var component = _viewModel.SelectedComponent;
        if (component == null) return;
        ActionLogService.Instance.Log(LogCategory.Property, "Applying property changes",
            $"Component: {component.Name}, Type: {component.Type}");
        
        try
        {
            component.Name = NameTextBox.Text;
            
            component.Position = new Point3D(
                double.Parse(PositionXTextBox.Text),
                double.Parse(PositionYTextBox.Text),
                double.Parse(PositionZTextBox.Text));
            
            component.Rotation = new Vector3D(
                double.Parse(RotationXTextBox.Text),
                double.Parse(RotationYTextBox.Text),
                double.Parse(RotationZTextBox.Text));
            
            component.Parameters.Width = double.Parse(WidthTextBox.Text);
            component.Parameters.Height = double.Parse(HeightTextBox.Text);
            component.Parameters.Depth = double.Parse(DepthTextBox.Text);
            component.Parameters.Material = MaterialTextBox.Text;
            component.Parameters.Elevation = double.Parse(ElevationTextBox.Text);
            component.Parameters.Color = ColorTextBox.Text;
            component.Parameters.Manufacturer = ManufacturerTextBox.Text;
            component.Parameters.PartNumber = PartNumberTextBox.Text;
            component.Parameters.ReferenceUrl = ReferenceUrlTextBox.Text;
            var catalogDataCleared = ClearCatalogMetadataIfDimensionsChanged(component);
            
            // Update layer assignment
            if (LayerComboBox.SelectedItem is Layer layer)
            {
                component.LayerId = layer.Id;
            }
            
            UpdateViewport();
            Update2DCanvas();
            UpdatePropertiesPanel();
            ActionLogService.Instance.Log(LogCategory.Property, "Properties applied",
                $"Name: {component.Name}, Pos: ({component.Position.X:F2}, {component.Position.Y:F2}, {component.Position.Z:F2}), " +
                $"Material: {component.Parameters.Material}, Color: {component.Parameters.Color}, " +
                $"Mfr: {component.Parameters.Manufacturer}, Part#: {component.Parameters.PartNumber}");
            var successMessage = catalogDataCleared
                ? "Properties updated. Catalog metadata was cleared because dimensions no longer match the validated catalog size."
                : "Properties updated successfully!";
            MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to apply properties", ex);
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool ClearCatalogMetadataIfDimensionsChanged(ElectricalComponent component)
    {
        var parameters = component.Parameters;
        if (!parameters.CatalogWidth.HasValue || !parameters.CatalogHeight.HasValue || !parameters.CatalogDepth.HasValue)
            return false;

        var matchesCatalog =
            Math.Abs(parameters.Width - parameters.CatalogWidth.Value) <= CatalogDimensionTolerance &&
            Math.Abs(parameters.Height - parameters.CatalogHeight.Value) <= CatalogDimensionTolerance &&
            Math.Abs(parameters.Depth - parameters.CatalogDepth.Value) <= CatalogDimensionTolerance;

        if (matchesCatalog)
            return false;

        if (string.IsNullOrWhiteSpace(parameters.Manufacturer) &&
            string.IsNullOrWhiteSpace(parameters.PartNumber) &&
            string.IsNullOrWhiteSpace(parameters.ReferenceUrl))
            return false;

        parameters.Manufacturer = string.Empty;
        parameters.PartNumber = string.Empty;
        parameters.ReferenceUrl = string.Empty;
        return true;
    }
    
    // ===== Project File Operations =====
    
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "New project requested");
        if (MessageBox.Show("Create a new project? Any unsaved changes will be lost.", "New Project", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            CancelPendingPlacement(logCancellation: false);
            _viewModel.Components.Clear();
            _viewModel.Layers.Clear();
            _viewModel.PdfUnderlay = null;
            _viewModel.UndoRedo.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
            
            // Re-initialize default layer
            var defaultLayer = Layer.CreateDefault();
            _viewModel.Layers.Add(defaultLayer);
            _viewModel.ActiveLayer = defaultLayer;
            ActionLogService.Instance.Log(LogCategory.FileOperation, "New project created");
            
            UpdateViewport();
            Update2DCanvas();
        }
    }
    
    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Open project dialog requested");
        var dialog = new OpenFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Open Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var project = await _viewModel.ProjectFileService.LoadProjectAsync(dialog.FileName);
                if (project != null)
                {
                    CancelPendingPlacement(logCancellation: false);
                    _viewModel.LoadFromProject(project);
                    _currentFilePath = dialog.FileName;
                    Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
                    ActionLogService.Instance.Log(LogCategory.FileOperation, "Project opened", $"File: {dialog.FileName}");
                    UpdateViewport();
                    Update2DCanvas();
                }
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to open project", ex);
                MessageBox.Show($"Error opening project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Save project requested",
            $"Current path: {_currentFilePath ?? "(none)"}");
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveProjectAs_Click(sender, e);
            return;
        }
        
        await SaveProjectAsync(_currentFilePath);
    }
    
    private async void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Save As dialog requested");
        var dialog = new SaveFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Save Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await SaveProjectAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
        }
    }
    
    private async Task SaveProjectAsync(string filePath)
    {
        try
        {
            var project = _viewModel.ToProjectModel();
            await _viewModel.ProjectFileService.SaveProjectAsync(project, filePath);
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Project saved", $"File: {filePath}");
            MessageBox.Show("Project saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to save project", ex);
            MessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Export JSON requested");
        if (_viewModel.SelectedComponent == null)
        {
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Export JSON aborted", "No component selected");
            MessageBox.Show("Please select a component to export.", "No Component", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export to JSON"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.FileService.ExportToJsonAsync(_viewModel.SelectedComponent, dialog.FileName);
                ActionLogService.Instance.Log(LogCategory.FileOperation, "JSON exported", $"File: {dialog.FileName}");
                MessageBox.Show("Component exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to export JSON", ex);
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void ExportBomCsv_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Export BOM CSV requested");
        if (!_viewModel.Components.Any())
        {
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Export BOM aborted", "No components");
            MessageBox.Show("No components to export.", "No Components", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Export Bill of Materials"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.BomExport.ExportToCsvAsync(_viewModel.Components, dialog.FileName);
                ActionLogService.Instance.Log(LogCategory.FileOperation, "BOM exported",
                    $"File: {dialog.FileName}, Components: {_viewModel.Components.Count}");
                MessageBox.Show("BOM exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to export BOM", ex);
                MessageBox.Show($"Error exporting BOM: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.Application, "Exit requested via menu");
        Close();
    }
    
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.S)
            {
                SaveProjectAs_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportJson_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        else if (modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Delete)
            {
                DeleteComponent_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_isFreehandDrawing)
                {
                    FinishFreehandConduit();
                    e.Handled = true;
                }
                else if (_isSketchLineMode || _isSketchRectangleMode)
                {
                    ExitSketchModes();
                    Update2DCanvas();
                    e.Handled = true;
                }
                else if (_isDrawingConduit)
                {
                    FinishDrawingConduit();
                    e.Handled = true;
                }
                else if (_isEditingConduitPath)
                {
                    ToggleEditConduitPath_Click(sender, e);
                    e.Handled = true;
                }
                else if (_pendingPlacementComponent != null)
                {
                    CancelPendingPlacement();
                    e.Handled = true;
                }
            }
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
                EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
                EditConduitPathButton.Content = "Edit Conduit Path";
                MessageBox.Show("Please select a conduit component first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
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
        
        for (int i = 1; i < pathPoints.Count; i++)
        {
            var point = pathPoints[i];
            var handle = CreateBendPointHandle(point);
            _bendPointHandles.Add(handle);
            Viewport.Children.Add(handle);
        }
    }
    
    private void HideBendPointHandles()
    {
        foreach (var handle in _bendPointHandles)
        {
            Viewport.Children.Remove(handle);
        }
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

    private sealed class ConduitVisualHost : FrameworkElement
    {
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _drawingVisual;

        public ConduitVisualHost()
        {
            _visuals = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _visuals.Add(_drawingVisual);

            // Initialize visual tree first so any property-change callbacks
            // that query VisualChildrenCount cannot hit null state.
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        public void Render(Action<DrawingContext> draw)
        {
            using var dc = _drawingVisual.RenderOpen();
            draw(dc);
        }

        protected override int VisualChildrenCount => _visuals?.Count ?? 0;

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }
    }

    private void OpenReference_Click(object sender, RoutedEventArgs e)
    {
        var url = ReferenceUrlTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("No reference URL is set for this component.", "Reference", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open URL: {ex.Message}", "Reference", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
