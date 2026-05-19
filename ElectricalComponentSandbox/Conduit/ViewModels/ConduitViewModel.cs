using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Geometry;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;
using ElectricalComponentSandbox.Conduit.Persistence;
using ElectricalComponentSandbox.Conduit.UI;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Conduit.ViewModels;

/// <summary>
/// Active tool state for the conduit authoring workflow.
/// </summary>
public enum ConduitToolState
{
    Select,
    DrawPrecise,
    DrawFreehand,
    EditVertex,
    AutoRoute
}

/// <summary>
/// ViewModel coordinating the 2D/3D conduit authoring engine.
/// Manages tool state, the model store, rendering, and persistence.
/// </summary>
public class ConduitViewModel : INotifyPropertyChanged
{
    private ConduitToolState _toolState = ConduitToolState.Select;
    private string _selectedRunId = string.Empty;
    private ConduitDetailLevel _detailLevel = ConduitDetailLevel.Coarse;

    public ConduitModelStore Store { get; } = new();
    public SmartBendService BendService { get; } = new();
    public AutoRouteService AutoRouter { get; }
    public RunScheduleService ScheduleService { get; }
    public SpoolManager SpoolManager { get; }
    public FreehandConduitTool DrawingTool { get; }
    public Conduit2DRenderer Renderer2D { get; }
    public Conduit3DViewManager View3D { get; }

    public ObservableCollection<ConduitRun> Runs { get; } = new();
    public ObservableCollection<RunScheduleEntry> Schedule { get; } = new();

    public ConduitToolState ToolState
    {
        get => _toolState;
        set { _toolState = value; OnPropertyChanged(); }
    }

    public string SelectedRunId
    {
        get => _selectedRunId;
        set { _selectedRunId = value; OnPropertyChanged(); }
    }

    public ConduitDetailLevel DetailLevel
    {
        get => _detailLevel;
        set
        {
            _detailLevel = value;
            Renderer2D.DetailLevel = value;
            OnPropertyChanged();
        }
    }

    public ConduitViewModel(CoordinateTransformService? transform = null)
    {
        var xform = transform ?? new CoordinateTransformService();
        var snapService = new Services.SnapService();

        // Initialize default conduit type
        var defaultType = new Core.Model.ConduitType();
        Store.AddType(defaultType);
        Store.Settings.DefaultConduitTypeId = defaultType.Id;

        AutoRouter = new AutoRouteService(Store, BendService);
        ScheduleService = new RunScheduleService(Store, BendService);
        SpoolManager = new SpoolManager(Store);

        DrawingTool = new FreehandConduitTool(xform, snapService, Store);
        Renderer2D = new Conduit2DRenderer(xform, Store.Settings.HiddenLineSettings);
        View3D = new Conduit3DViewManager(Store);

        ApplyStoreRoutingDefaultsToDrawingTool();
    }

    public void SetTool(ConduitToolState tool)
    {
        if (ToolState != ConduitToolState.Select)
            DrawingTool.Cancel();

        ApplyStoreRoutingDefaultsToDrawingTool();

        ToolState = tool;
        DrawingTool.Mode = tool == ConduitToolState.DrawFreehand
            ? ConduitDrawMode.Freehand
            : ConduitDrawMode.Precise;
    }

    public void OnClick(Point screenPoint)
    {
        if (ToolState == ConduitToolState.DrawPrecise || ToolState == ConduitToolState.DrawFreehand)
        {
            DrawingTool.OnClick(screenPoint,
                Enumerable.Empty<Point>(),
                Enumerable.Empty<(Point, Point)>());
        }
    }

    public void OnMouseMove(Point screenPoint)
    {
        if (ToolState == ConduitToolState.DrawPrecise || ToolState == ConduitToolState.DrawFreehand)
        {
            DrawingTool.OnMouseMove(screenPoint);
        }
    }

    public ConduitRun? FinishDrawing()
    {
        ApplyStoreRoutingDefaultsToDrawingTool();

        var run = DrawingTool.FinishDrawing();
        if (run != null)
        {
            Runs.Add(run);
            RefreshSchedule();
            View3D.InvalidateAll();
        }
        return run;
    }

    public ConduitRun? AutoRouteAlongPath(IReadOnlyList<XYZ> pathway, RoutingOptions? options = null)
    {
        var opts = options ?? new RoutingOptions
        {
            ConduitTypeId = Store.Settings.DefaultConduitTypeId,
            TradeSize = Store.Settings.DefaultTradeSize,
            Elevation = Store.Settings.DefaultElevation
        };

        var defaults = Store.ResolveRoutingDefaults(opts.ConduitTypeId, opts.TradeSize, opts.Material);
        opts.ConduitTypeId = defaults.ConduitTypeId;
        opts.TradeSize = defaults.TradeSize;
        opts.Material = defaults.Material;

        var run = AutoRouter.AutoRoute(pathway, opts);
        Runs.Add(run);
        RefreshSchedule();
        View3D.InvalidateAll();
        return run;
    }

    public bool MoveVertex(string segmentId, bool isStart, XYZ newPosition)
    {
        var seg = Store.GetSegment(segmentId);
        if (seg == null) return false;

        if (isStart)
            seg.StartPoint = newPosition;
        else
            seg.EndPoint = newPosition;

        seg.InitializeConnectors();
        View3D.InvalidateCache(segmentId);
        return true;
    }

    public void RefreshSchedule()
    {
        Schedule.Clear();
        foreach (var entry in ScheduleService.GenerateSchedule())
            Schedule.Add(entry);
    }

    public string ExportScheduleCsv() => ScheduleService.ExportScheduleCsv();

    // ── Spool sheet authoring ────────────────────────────────────────────

    /// <summary>
    /// Builds a printable spool sheet for the given run id. <paramref name="hangers"/>
    /// optionally supplies the trapeze hangers placed along the run for the
    /// hanger schedule + trapeze BOM sections.
    /// </summary>
    public SpoolSheet BuildSpoolSheet(
        string runId,
        IReadOnlyList<HangerComponent>? hangers = null,
        SpoolSheetTitleBlock? titleBlock = null)
    {
        var run = ResolveRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        var builder = new SpoolSheetBuilder(Store);
        return builder.Build(run.Id, hangers, titleBlock);
    }

    /// <summary>
    /// Builds spool sheets for every run in the named spool package and
    /// caches them on the package for re-open. Returns the freshly built
    /// sheet list.
    /// </summary>
    public IReadOnlyList<SpoolSheet> BuildSpoolSheetsForPackage(
        string spoolId,
        Func<string, IReadOnlyList<HangerComponent>>? hangerSelector = null,
        SpoolSheetTitleBlock? titleTemplate = null)
        => SpoolManager.BuildSheets(spoolId, hangerSelector, titleTemplate);

    /// <summary>
    /// Renders a spool sheet to layout geometry suitable for a WPF /
    /// PDF painter. Defaults to ANSI B (11x17).
    /// </summary>
    public SpoolSheetRenderGeometry RenderSpoolSheet(
        SpoolSheet sheet,
        PaperSizeType paperSize = PaperSizeType.ANSI_B)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var renderer = new SpoolSheetRenderer();
        return renderer.Render(sheet, paperSize);
    }

    /// <summary>
    /// Opens an in-app preview window for the given run. Combines build →
    /// render → show into a single call so a UI command (toolbar button,
    /// menu item, context action) needs only the run id and any hangers
    /// to show a printable shop drawing.
    /// </summary>
    /// <param name="runId">Run id (internal GUID or user-facing "CR-001").</param>
    /// <param name="hangers">Optional hangers placed along the run.</param>
    /// <param name="titleBlock">Optional title-block overrides.</param>
    /// <param name="owner">Optional owner window for centering.</param>
    /// <returns>
    /// The opened window so callers can subscribe to <see cref="System.Windows.Window.Closed"/>
    /// or modify the window after it's shown.
    /// </returns>
    public SpoolSheetPreviewWindow OpenSpoolSheetPreview(
        string runId,
        IReadOnlyList<HangerComponent>? hangers = null,
        SpoolSheetTitleBlock? titleBlock = null,
        System.Windows.Window? owner = null)
    {
        var sheet = BuildSpoolSheet(runId, hangers, titleBlock);
        var geometry = RenderSpoolSheet(sheet);
        var fileName = string.IsNullOrWhiteSpace(sheet.TitleBlock.SheetNumber)
            ? sheet.RunId
            : sheet.TitleBlock.SheetNumber;
        var window = new SpoolSheetPreviewWindow(geometry, fileName);
        if (owner != null) window.Owner = owner;
        window.Show();
        return window;
    }

    private ConduitRun? ResolveRun(string runId)
    {
        // Accept either the internal GUID or the user-facing "CR-001" id.
        return Store.GetAllRuns().FirstOrDefault(r =>
            string.Equals(r.Id, runId, StringComparison.Ordinal) ||
            string.Equals(r.RunId, runId, StringComparison.Ordinal));
    }

    public string SerializeToJson() => ConduitPersistence.SerializeToJson(Store);

    public void LoadFromJson(string json)
    {
        var loaded = ConduitPersistence.DeserializeFromJson(json);

        Runs.Clear();

        foreach (var type in loaded.GetAllTypes())
            Store.AddType(type);
        foreach (var seg in loaded.GetAllSegments())
            Store.AddSegment(seg);
        foreach (var fit in loaded.GetAllFittings())
            Store.AddFitting(fit);
        foreach (var run in loaded.GetAllRuns())
        {
            Store.AddRun(run);
            Runs.Add(run);
        }

        ApplyStoreRoutingDefaultsToDrawingTool();
        RefreshSchedule();
        View3D.InvalidateAll();
    }

    private void ApplyStoreRoutingDefaultsToDrawingTool()
    {
        var defaults = Store.ResolveRoutingDefaults();
        DrawingTool.ConduitTypeId = defaults.ConduitTypeId;
        DrawingTool.TradeSize = defaults.TradeSize;
        DrawingTool.Material = defaults.Material;
        DrawingTool.Elevation = Store.Settings.DefaultElevation;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
