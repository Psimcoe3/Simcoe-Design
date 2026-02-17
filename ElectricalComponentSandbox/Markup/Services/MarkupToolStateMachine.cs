using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Tool types available in the markup engine
/// </summary>
public enum MarkupToolType
{
    Select,
    Polyline,
    Polygon,
    Rectangle,
    Circle,
    Text,
    Box,
    Panel,
    ConduitRun,
    Calibrate,
    Dimension,
    Scale
}

/// <summary>
/// Drawing state of the tool state machine
/// </summary>
public enum ToolState
{
    Idle,
    Drawing,
    Editing,
    Calibrating
}

/// <summary>
/// State machine for markup drawing tools.
/// Manages active tool, drawing state, in-progress vertices, and tool transitions.
/// </summary>
public class MarkupToolStateMachine
{
    public MarkupToolType ActiveTool { get; private set; } = MarkupToolType.Select;
    public ToolState State { get; private set; } = ToolState.Idle;

    /// <summary>
    /// Vertices collected while drawing (in document space)
    /// </summary>
    public List<Point> PendingVertices { get; } = new();

    /// <summary>
    /// The markup currently being edited (for Select tool)
    /// </summary>
    public MarkupRecord? SelectedMarkup { get; set; }

    /// <summary>
    /// Live cursor position during drawing (for rubber-band preview)
    /// </summary>
    public Point CurrentCursor { get; set; }

    /// <summary>
    /// Whether Shift is held (for ortho/45° constraint)
    /// </summary>
    public bool IsShiftHeld { get; set; }

    /// <summary>
    /// Switches to a new tool, cancelling any in-progress drawing
    /// </summary>
    public void SetTool(MarkupToolType tool)
    {
        Cancel();
        ActiveTool = tool;
    }

    /// <summary>
    /// Records a click at a document-space point.
    /// Returns a completed MarkupRecord if the shape is finished, otherwise null.
    /// </summary>
    public MarkupRecord? OnClick(Point docPoint)
    {
        switch (ActiveTool)
        {
            case MarkupToolType.Select:
                return null; // selection handled externally via hit-test

            case MarkupToolType.Polyline:
            case MarkupToolType.ConduitRun:
                return HandlePolylineClick(docPoint, isConduit: ActiveTool == MarkupToolType.ConduitRun);

            case MarkupToolType.Polygon:
            case MarkupToolType.Box:
            case MarkupToolType.Panel:
                return HandlePolygonClick(docPoint);

            case MarkupToolType.Rectangle:
                return HandleRectangleClick(docPoint);

            case MarkupToolType.Circle:
                return HandleCircleClick(docPoint);

            case MarkupToolType.Text:
                return HandleTextClick(docPoint);

            case MarkupToolType.Calibrate:
                return HandleCalibrateClick(docPoint);

            case MarkupToolType.Dimension:
                return HandleDimensionClick(docPoint);

            default:
                return null;
        }
    }

    /// <summary>
    /// Finishes a multi-click shape (polyline/polygon).
    /// Called on double-click or Enter.
    /// </summary>
    public MarkupRecord? FinishShape()
    {
        if (State != ToolState.Drawing) return null;

        MarkupType type;
        int minVertices;

        switch (ActiveTool)
        {
            case MarkupToolType.Polyline:
                type = MarkupType.Polyline;
                minVertices = 2;
                break;
            case MarkupToolType.ConduitRun:
                type = MarkupType.ConduitRun;
                minVertices = 2;
                break;
            case MarkupToolType.Polygon:
                type = MarkupType.Polygon;
                minVertices = 3;
                break;
            case MarkupToolType.Box:
                type = MarkupType.Box;
                minVertices = 3;
                break;
            case MarkupToolType.Panel:
                type = MarkupType.Panel;
                minVertices = 3;
                break;
            default:
                return null;
        }

        if (PendingVertices.Count < minVertices) return null;

        var record = new MarkupRecord
        {
            Type = type,
            Vertices = new List<Point>(PendingVertices)
        };
        record.UpdateBoundingRect();

        Reset();
        return record;
    }

    /// <summary>
    /// Cancels current drawing without creating a markup
    /// </summary>
    public void Cancel()
    {
        Reset();
    }

    private void Reset()
    {
        PendingVertices.Clear();
        State = ToolState.Idle;
        SelectedMarkup = null;
    }

    // ── Per-tool click handlers ──

    private MarkupRecord? HandlePolylineClick(Point p, bool isConduit)
    {
        PendingVertices.Add(p);
        State = ToolState.Drawing;
        return null; // finished via FinishShape()
    }

    private MarkupRecord? HandlePolygonClick(Point p)
    {
        PendingVertices.Add(p);
        State = ToolState.Drawing;
        return null; // finished via FinishShape()
    }

    private MarkupRecord? HandleRectangleClick(Point p)
    {
        PendingVertices.Add(p);
        if (PendingVertices.Count == 2)
        {
            var record = new MarkupRecord
            {
                Type = MarkupType.Rectangle,
                Vertices = new List<Point>(PendingVertices)
            };
            record.UpdateBoundingRect();
            Reset();
            return record;
        }
        State = ToolState.Drawing;
        return null;
    }

    private MarkupRecord? HandleCircleClick(Point p)
    {
        PendingVertices.Add(p);
        if (PendingVertices.Count == 2)
        {
            double radius = GeometryMath.Distance(PendingVertices[0], PendingVertices[1]);
            var record = new MarkupRecord
            {
                Type = MarkupType.Circle,
                Vertices = new List<Point> { PendingVertices[0] },
                Radius = radius
            };
            record.UpdateBoundingRect();
            Reset();
            return record;
        }
        State = ToolState.Drawing;
        return null;
    }

    private MarkupRecord? HandleTextClick(Point p)
    {
        var record = new MarkupRecord
        {
            Type = MarkupType.Text,
            Vertices = new List<Point> { p },
            TextContent = "Text"
        };
        record.UpdateBoundingRect();
        Reset();
        return record;
    }

    private MarkupRecord? HandleCalibrateClick(Point p)
    {
        PendingVertices.Add(p);
        State = PendingVertices.Count < 2 ? ToolState.Calibrating : ToolState.Idle;
        return null; // calibration handled externally once two points are collected
    }

    private MarkupRecord? HandleDimensionClick(Point p)
    {
        PendingVertices.Add(p);
        if (PendingVertices.Count == 2)
        {
            var record = new MarkupRecord
            {
                Type = MarkupType.Dimension,
                Vertices = new List<Point>(PendingVertices)
            };
            record.UpdateBoundingRect();
            Reset();
            return record;
        }
        State = ToolState.Drawing;
        return null;
    }
}
