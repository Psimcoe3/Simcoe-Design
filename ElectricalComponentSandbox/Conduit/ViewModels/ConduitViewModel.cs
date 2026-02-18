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

    // ?? Core services ??

    public ConduitModelStore Store { get; } = new();
    public SmartBendService BendService { get; } = new();
    public AutoRouteService AutoRouter { get; }
    public RunScheduleService ScheduleService { get; }
    public SpoolManager SpoolManager { get; }
    public FreehandConduitTool DrawingTool { get; }
    public Conduit2DRenderer Renderer2D { get; }
    public Conduit3DViewManager View3D { get; }

    // ?? Observable collections for UI binding ??

    public ObservableCollection<ConduitRun> Runs { get; } = new();
    public ObservableCollection<RunScheduleEntry> Schedule { get; } = new();

    // ?? Properties ??

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

        DrawingTool = new FreehandConduitTool(xform, snapService, Store)
        {
            ConduitTypeId = defaultType.Id
        };

        Renderer2D = new Conduit2DRenderer(xform, Store.Settings.HiddenLineSettings);
        View3D = new Conduit3DViewManager(Store);
    }

    // ?? Tool commands ??

    public void SetTool(ConduitToolState tool)
    {
        if (ToolState != ConduitToolState.Select)
            DrawingTool.Cancel();

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
        var run = DrawingTool.FinishDrawing();
        if (run != null)
        {
            Runs.Add(run);
            RefreshSchedule();
            View3D.InvalidateAll();
        }
        return run;
    }

    // ?? Auto-route ??

    public ConduitRun? AutoRouteAlongPath(IReadOnlyList<XYZ> pathway, RoutingOptions? options = null)
    {
        var opts = options ?? new RoutingOptions
        {
            ConduitTypeId = Store.Settings.DefaultConduitTypeId,
            TradeSize = Store.Settings.DefaultTradeSize,
            Elevation = Store.Settings.DefaultElevation
        };

        var run = AutoRouter.AutoRoute(pathway, opts);
        Runs.Add(run);
        RefreshSchedule();
        View3D.InvalidateAll();
        return run;
    }

    // ?? Vertex editing ??

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

    // ?? Schedule & persistence ??

    public void RefreshSchedule()
    {
        Schedule.Clear();
        foreach (var entry in ScheduleService.GenerateSchedule())
            Schedule.Add(entry);
    }

    public string ExportScheduleCsv() => ScheduleService.ExportScheduleCsv();

    public string SerializeToJson() => ConduitPersistence.SerializeToJson(Store);

    public void LoadFromJson(string json)
    {
        var loaded = ConduitPersistence.DeserializeFromJson(json);

        // Transfer data to our store
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

        RefreshSchedule();
        View3D.InvalidateAll();
    }

    // ?? INotifyPropertyChanged ??

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
