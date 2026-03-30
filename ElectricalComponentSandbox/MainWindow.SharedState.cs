using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using ElectricalComponentSandbox.Services.RevitIntrospection;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;
    private string? _currentFilePath;
    private readonly Dictionary<ModelVisual3D, ElectricalComponent> _visualToComponentMap = new();
    private bool _isEditingConduitPath = false;
    private readonly List<ModelVisual3D> _bendPointHandles = new();
    private readonly List<Visual3D> _selectionDimensionVisuals = new();
    private readonly Dictionary<Visual3D, char> _dimensionVisualAxisMap = new();
    private readonly Dictionary<string, DimensionAxisOffsets> _dimensionOffsetsByComponentId = new(StringComparer.Ordinal);
    private readonly List<CustomDimensionAnnotation> _customDimensionAnnotations = new();
    private readonly List<Visual3D> _customDimensionPreviewVisuals = new();
    private readonly HashSet<Visual3D> _customDimensionPreviewVisualSet = new();
    private bool _isDraggingDimensionAnnotation = false;
    private char _draggingDimensionAxis = '\0';
    private Point _dimensionDragStartMousePosition;
    private double _dimensionDragStartOffsetFeet = 0.0;
    private bool _isAddingCustomDimension = false;
    private CustomDimensionAnchor? _pendingCustomDimensionStartAnchor = null;
    private CustomDimensionSnapMode _customDimensionSnapMode = CustomDimensionSnapMode.Auto;
    private ModelVisual3D? _draggedHandle = null;
    private Point _lastMousePosition;
    private const double MobileMinCanvasScale = 0.2;
    private const double MobileMaxCanvasScale = 12.0;

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
    private readonly DispatcherTimer _interactionQualityRestoreTimer = new();
    private DateTime _lastInteractionInputUtc = DateTime.MinValue;
    private bool _isFastInteractionMode = false;
    private bool _queuedSceneRefresh = false;
    private bool _pending2DRefresh = false;
    private bool _pending3DRefresh = false;
    private bool _pendingPropertiesRefresh = false;
    private ElectricalComponent? _pendingPlacementComponent = null;
    private string? _pendingPlacementSource = null;
    private StratusImperialDefaults _stratusImperialDefaults = StratusImperialDefaults.Empty;
    private const string DefaultStratusXmlDirectory = @"C:\Users\Paul\STRATUS\2.23.26\xml files";
    private readonly RevitIntrospectionOptions _revitIntrospectionOptions;
    private readonly RevitGeometryMeasurementIntrospectionService _revitIntrospectionService;
    private readonly DimensionSnapSelectionService _dimensionSnapSelectionService = new();
    private readonly DimensionPlacementState _customDimensionPlacementState = new();

    private bool _isDrawingConduit = false;
#pragma warning disable CS0414
    private ConduitComponent? _drawingConduit = null;
#pragma warning restore CS0414
    private readonly List<Point> _drawingCanvasPoints = new();
    private Line? _rubberBandLine = null;
    private Ellipse? _snapIndicator = null;
    private bool _isPdfCalibrationMode = false;
    private Point? _pdfCalibrationFirstCanvasPoint = null;
    private Point? _pdfCalibrationFirstDocumentPoint = null;
    private Line? _pdfCalibrationPreviewLine = null;
    private Ellipse? _pdfCalibrationStartMarker = null;

    private const double BendPointHandleRadius = 0.3;
    private static readonly Color EditModeButtonColor = Color.FromRgb(255, 200, 100);
    private static readonly Color BendPointHandleColor = Colors.Orange;
    private const double Conduit2DHandleRadius = 6.0;
    private const double Conduit2DInsertThreshold = 12.0;
    private const double Conduit2DHitThreshold = 10.0;
    private const double CatalogDimensionTolerance = 0.0005;

    private const int MaxSegmentResolution = 50;
    private const int MinSegmentResolution = 5;
    private const double ResolutionScaleFactor = 10.0;
    private const double CanvasWorldOrigin = 1000.0;
    private const double CanvasWorldScale = 20.0;
    private const double DefaultPlanCanvasSize = 2000.0;
    private const int DefaultDimensionInchFractionDenominator = 16;
    private const double InViewDimensionMinSpan = 1e-4;
    private const double BaseDimensionSnapTolerancePx = 12.0;
    private const double BaselineWorldUnitsPerPixel = 0.05;

    private DimensionDisplayMode _dimensionDisplayMode = DimensionDisplayMode.FeetInches;
    private int _dimensionInchFractionDenominator = DefaultDimensionInchFractionDenominator;
    private readonly Dictionary<FrameworkElement, int> _canvasToGripIndexMap = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        SetMobileTheme(MobileTheme.IOS);
        _interactionQualityRestoreTimer.Interval = TimeSpan.FromMilliseconds(120);
        _interactionQualityRestoreTimer.Tick += InteractionQualityRestoreTimer_Tick;
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeDimensionDisplayDefaults();
        _revitIntrospectionOptions = RevitIntrospectionOptions.FromEnvironment();
        _revitIntrospectionService = RevitGeometryMeasurementIntrospectionService.CreateDefault(_revitIntrospectionOptions);
        _customDimensionSnapMode = GetSelectedCustomDimensionSnapMode();
        InitializeStratusImperialDefaults();
        InitializeReferenceMenu();
        ApplyDesktopPaneLayout();
        UpdateCustomDimensionUiState();
        InitializeCanvasInteractionController();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.MarkupTool.PropertyChanged += MarkupTool_PropertyChanged;
        _viewModel.LayerManager.LayerRowChanged += LayerManager_LayerRowChanged;

        SkiaBackground.RenderFrame += OnSkiaBackgroundRenderFrame;
        PlanScrollViewer.ScrollChanged += PlanScrollViewer_ScrollChanged;

        ActionLogService.Instance.Log(LogCategory.Application, "MainWindow initialized");
        ActionLogService.Instance.Log(LogCategory.Application, "Revit introspection feature state",
            $"Enabled: {_revitIntrospectionOptions.IsEnabled}, PathOverride: {_revitIntrospectionOptions.InstallPathOverride ?? "(auto)"}");
        Closed += (s, e) =>
        {
            ActionLogService.Instance.LogSeparator("SESSION SUMMARY");
            ActionLogService.Instance.Log(LogCategory.Application, "Final state",
                $"Components: {_viewModel.Components.Count}, Layers: {_viewModel.Layers.Count}, Units: {_viewModel.UnitSystemName}, Grid: {_viewModel.GridSize}");
            ActionLogService.Instance.Log(LogCategory.Application, "MainWindow closed");
        };
    }

    private static string GetStratusXmlDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("STRATUS_XML_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultStratusXmlDirectory
            : configured.Trim();
    }

    private void InitializeStratusImperialDefaults()
    {
        var stratusDirectory = GetStratusXmlDirectory();
        _stratusImperialDefaults = StratusImperialDefaultsLoader.LoadImperialDefaults(stratusDirectory);

        if (!_stratusImperialDefaults.HasData)
        {
            ActionLogService.Instance.Log(LogCategory.Application, "STRATUS imperial defaults not loaded",
                $"Directory: {stratusDirectory}");
            return;
        }

        ConfigureConduitEngineFromStratusDefaults();

        ActionLogService.Instance.Log(LogCategory.Application, "STRATUS imperial defaults loaded",
            $"Directory: {stratusDirectory}, BendSettings: {_stratusImperialDefaults.BendSettings.Count}, RunSpacings: {_stratusImperialDefaults.RunAlignmentSpacings.Count}, WireSpecs: {_stratusImperialDefaults.WireSpecifications.Count}, MaxFill: {_stratusImperialDefaults.MaximumWireFill?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a"}");
    }

    private void ConfigureConduitEngineFromStratusDefaults()
    {
        if (!_stratusImperialDefaults.HasData || _stratusImperialDefaults.BendSettings.Count == 0)
            return;

        var groupedByTradeSize = _stratusImperialDefaults.BendSettings
            .GroupBy(setting => setting.TradeSize, StringComparer.OrdinalIgnoreCase);

        foreach (var tradeGroup in groupedByTradeSize)
        {
            var selectedSetting = tradeGroup
                .OrderBy(setting => string.Equals(setting.BenderName, _stratusImperialDefaults.PreferredBenderName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(setting => setting.BenderName, StringComparer.OrdinalIgnoreCase)
                .First();

            var existing90 = ConduitEngine.BendService.LookupDeduct(tradeGroup.Key, 90);
            var bendRadiusInches = selectedSetting.RadiusFeet * UnitConversionService.InchesPerFoot;
            var deductInches = selectedSetting.DeductFeet * UnitConversionService.InchesPerFoot;
            var tangentInches = existing90?.TangentLengthInches ?? bendRadiusInches;
            var gainInches = existing90?.GainInches ?? Math.Max(0.0, deductInches * 0.5);

            ConduitEngine.BendService.UpsertEntry(
                tradeGroup.Key,
                90.0,
                bendRadiusInches,
                deductInches,
                tangentInches,
                gainInches);
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var name = source?.Name;
        if (string.IsNullOrEmpty(name))
            name = source?.GetType().Name ?? "Unknown";

        var pos = e.GetPosition(this);
        ActionLogService.Instance.Log(LogCategory.Input, "Left click",
            $"Element: {name}, Position: ({pos.X:F0}, {pos.Y:F0})");
        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var name = source?.Name;
        if (string.IsNullOrEmpty(name))
            name = source?.GetType().Name ?? "Unknown";

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
        var modStr = modifiers != ModifierKeys.None ? $"{modifiers}+" : string.Empty;
        ActionLogService.Instance.Log(LogCategory.Input, "Key press", $"Key: {modStr}{e.Key}");
        base.OnPreviewKeyDown(e);
    }
}
