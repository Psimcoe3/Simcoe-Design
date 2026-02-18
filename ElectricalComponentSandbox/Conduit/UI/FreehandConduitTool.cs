using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Geometry;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Conduit.UI;

/// <summary>
/// Tool mode for the 2D conduit drawing tool.
/// </summary>
public enum ConduitDrawMode
{
    /// <summary>Click to place vertices precisely.</summary>
    Precise,
    /// <summary>Freehand pen/scribble mode with continuous sampling.</summary>
    Freehand
}

/// <summary>
/// 2D conduit drawing tool that supports both precise click-to-place
/// and freehand (Bluebeam-style) sketching with snap and angle constraints.
/// Converts drawn paths into ConduitRun objects for the 3D core.
/// </summary>
public class FreehandConduitTool
{
    private readonly CoordinateTransformService _transform;
    private readonly Services.SnapService _snapService;
    private readonly ConduitModelStore _store;

    /// <summary>Current drawing mode.</summary>
    public ConduitDrawMode Mode { get; set; } = ConduitDrawMode.Precise;

    /// <summary>Vertices collected in document space during drawing.</summary>
    public List<Point> PendingVertices { get; } = new();

    /// <summary>Whether the tool is currently drawing.</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>Whether Shift is held (ortho/45° constraint).</summary>
    public bool IsShiftHeld { get; set; }

    /// <summary>Current cursor position for rubber-band preview.</summary>
    public Point CurrentCursor { get; set; }

    /// <summary>Epsilon for RDP simplification (document units).</summary>
    public double SimplifyEpsilon { get; set; } = 5.0;

    /// <summary>Whether to orthogonalize after simplification.</summary>
    public bool Orthogonalize { get; set; } = false;

    /// <summary>Elevation for generated 3D conduit (feet).</summary>
    public double Elevation { get; set; } = 10.0;

    /// <summary>Conduit type ID for generated segments.</summary>
    public string ConduitTypeId { get; set; } = string.Empty;

    /// <summary>Trade size for generated segments.</summary>
    public string TradeSize { get; set; } = "1/2";

    /// <summary>Material for generated segments.</summary>
    public ConduitMaterialType Material { get; set; } = ConduitMaterialType.EMT;

    /// <summary>Freehand sampling distance in screen pixels.</summary>
    public double FreehandSampleDistance { get; set; } = 5.0;

    /// <summary>Typed length override (user enters exact length in real units).</summary>
    public double? TypedLength { get; set; }

    /// <summary>Typed angle override (degrees).</summary>
    public double? TypedAngle { get; set; }

    public FreehandConduitTool(
        CoordinateTransformService transform,
        Services.SnapService snapService,
        ConduitModelStore store)
    {
        _transform = transform;
        _snapService = snapService;
        _store = store;
    }

    /// <summary>
    /// Processes a mouse-down / click at a screen-space point.
    /// In Precise mode, adds a vertex. In Freehand mode, starts sampling.
    /// Returns a completed ConduitRun if double-click finishes the shape, else null.
    /// </summary>
    public ConduitRun? OnClick(Point screenPoint, IEnumerable<Point> snapEndpoints,
        IEnumerable<(Point A, Point B)> snapSegments)
    {
        var docPoint = _transform.ScreenToDocument(screenPoint);

        // Apply snap
        var snapResult = _snapService.FindSnapPoint(docPoint, snapEndpoints, snapSegments);
        if (snapResult.Snapped)
            docPoint = snapResult.SnappedPoint;

        // Apply angle constraint
        if (IsShiftHeld && PendingVertices.Count > 0)
            docPoint = AngleConstraintHelper.Constrain45(PendingVertices[^1], docPoint);

        // Apply typed length
        if (TypedLength.HasValue && PendingVertices.Count > 0)
        {
            var prev = PendingVertices[^1];
            double dx = docPoint.X - prev.X;
            double dy = docPoint.Y - prev.Y;
            double angle = TypedAngle.HasValue
                ? TypedAngle.Value * Math.PI / 180.0
                : Math.Atan2(dy, dx);

            double docLength = TypedLength.Value * ((_transform.DocUnitsPerRealX + _transform.DocUnitsPerRealY) / 2.0);
            docPoint = new Point(
                prev.X + docLength * Math.Cos(angle),
                prev.Y + docLength * Math.Sin(angle));

            TypedLength = null;
            TypedAngle = null;
        }

        PendingVertices.Add(docPoint);
        IsDrawing = true;
        return null;
    }

    /// <summary>
    /// Processes mouse move during freehand drawing.
    /// Samples points at the configured distance.
    /// </summary>
    public void OnMouseMove(Point screenPoint)
    {
        CurrentCursor = _transform.ScreenToDocument(screenPoint);

        if (Mode == ConduitDrawMode.Freehand && IsDrawing && PendingVertices.Count > 0)
        {
            double dist = Distance(PendingVertices[^1], CurrentCursor);
            double docSampleDist = _transform.ScreenToDocumentDistance(FreehandSampleDistance);

            if (dist >= docSampleDist)
            {
                PendingVertices.Add(CurrentCursor);
            }
        }
    }

    /// <summary>
    /// Finishes the current drawing and produces a ConduitRun.
    /// Applies RDP simplification and optional orthogonalization.
    /// </summary>
    public ConduitRun? FinishDrawing()
    {
        if (PendingVertices.Count < 2)
        {
            Cancel();
            return null;
        }

        // Simplify freehand path
        var simplified = Mode == ConduitDrawMode.Freehand
            ? PathSimplifier.RamerDouglasPeucker(PendingVertices, SimplifyEpsilon)
            : new List<Point>(PendingVertices);

        // Optional orthogonalize
        if (Orthogonalize)
            simplified = PathSimplifier.Orthogonalize(simplified);

        // Convert to 3D
        var path3D = PathSimplifier.To3DPath(
            simplified, Elevation,
            _transform.IsCalibrated ? _transform.DocumentToRealWorld : null);

        // Create segments
        var segments = PathSimplifier.CreateSegmentsFromPath(
            path3D, ConduitTypeId, TradeSize, Material);

        // Validate minimum length
        var sizeSettings = ConduitSizeSettings.CreateDefaultEMT();
        segments = segments.Where(s => sizeSettings.IsValidLength(s.Length * 12.0)).ToList();

        if (segments.Count == 0)
        {
            Cancel();
            return null;
        }

        // Create run
        var run = _store.CreateRunFromSegments(segments);

        Cancel();
        return run;
    }

    /// <summary>
    /// Cancels the current drawing without creating a run.
    /// </summary>
    public void Cancel()
    {
        PendingVertices.Clear();
        IsDrawing = false;
        TypedLength = null;
        TypedAngle = null;
    }

    /// <summary>
    /// Gets the simplified preview path for display during drawing.
    /// </summary>
    public List<Point> GetPreviewPath()
    {
        if (PendingVertices.Count < 2) return new List<Point>(PendingVertices);

        var preview = Mode == ConduitDrawMode.Freehand
            ? PathSimplifier.RamerDouglasPeucker(PendingVertices, SimplifyEpsilon)
            : new List<Point>(PendingVertices);

        if (Orthogonalize)
            preview = PathSimplifier.Orthogonalize(preview);

        return preview;
    }

    /// <summary>
    /// Removes the last placed vertex (undo last click).
    /// </summary>
    public void RemoveLastVertex()
    {
        if (PendingVertices.Count > 0)
            PendingVertices.RemoveAt(PendingVertices.Count - 1);

        if (PendingVertices.Count == 0)
            IsDrawing = false;
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
