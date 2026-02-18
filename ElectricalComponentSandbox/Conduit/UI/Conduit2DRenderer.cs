using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Conduit.UI;

/// <summary>
/// View detail level for conduit 2D rendering (Revit-inspired).
/// </summary>
public enum ConduitDetailLevel
{
    /// <summary>Single centerline – for zoomed-out views.</summary>
    Coarse,
    /// <summary>Single line with tick marks – intermediate.</summary>
    Medium,
    /// <summary>True-width double-line with hidden line gaps.</summary>
    Fine
}

/// <summary>
/// Rise/drop symbol types for plan-view rendering.
/// </summary>
public enum RiseDropSymbol
{
    None,
    RiseArrow,  // Arrow pointing up (toward viewer)
    DropArrow   // Arrow pointing down (away from viewer)
}

/// <summary>
/// View range settings for plan-view conduit display (Revit-inspired).
/// Conduits within the view range are shown normally;
/// those above/below get rise/drop symbols.
/// </summary>
public class ConduitViewRange
{
    /// <summary>Top of the view cut plane (feet).</summary>
    public double TopClipElevation { get; set; } = 12.0;

    /// <summary>Bottom of the view cut plane (feet).</summary>
    public double BottomClipElevation { get; set; } = 0.0;

    /// <summary>Cut plane elevation (feet) – conduits at this level are shown in plan.</summary>
    public double CutPlaneElevation { get; set; } = 4.0;

    /// <summary>
    /// Determines what symbol to show for a conduit at a given elevation.
    /// </summary>
    public RiseDropSymbol GetSymbol(double segmentStartZ, double segmentEndZ)
    {
        bool startsInRange = segmentStartZ >= BottomClipElevation && segmentStartZ <= TopClipElevation;
        bool endsInRange = segmentEndZ >= BottomClipElevation && segmentEndZ <= TopClipElevation;

        if (startsInRange && !endsInRange)
            return segmentEndZ > TopClipElevation ? RiseDropSymbol.RiseArrow : RiseDropSymbol.DropArrow;

        if (!startsInRange && endsInRange)
            return segmentStartZ > TopClipElevation ? RiseDropSymbol.DropArrow : RiseDropSymbol.RiseArrow;

        return RiseDropSymbol.None;
    }
}

/// <summary>
/// Per-view graphic overrides for conduit rendering.
/// </summary>
public class OverrideGraphicSettings
{
    public string? ColorOverride { get; set; }
    public double? TransparencyOverride { get; set; }
    public double? LineWeightOverride { get; set; }
    public bool IsHidden { get; set; }
}

/// <summary>
/// 2D rendering data for a single conduit segment at a given detail level.
/// </summary>
public class Conduit2DRenderData
{
    /// <summary>Centerline start in document space.</summary>
    public Point Start { get; set; }
    /// <summary>Centerline end in document space.</summary>
    public Point End { get; set; }

    /// <summary>Detail level used for rendering.</summary>
    public ConduitDetailLevel DetailLevel { get; set; }

    /// <summary>Width in document units (for Fine detail).</summary>
    public double Width { get; set; }

    /// <summary>Hidden line gap segments to omit (start/end t-parameters).</summary>
    public List<(double TStart, double TEnd)> HiddenLineGaps { get; set; } = new();

    /// <summary>Rise/drop symbol at segment endpoint.</summary>
    public RiseDropSymbol Symbol { get; set; } = RiseDropSymbol.None;

    /// <summary>Graphic overrides for this element.</summary>
    public OverrideGraphicSettings? Overrides { get; set; }
}

/// <summary>
/// Generates 2D rendering data for conduit segments based on detail level,
/// view range, and hidden line settings.
/// </summary>
public class Conduit2DRenderer
{
    private readonly CoordinateTransformService _transform;
    private readonly MEPHiddenLineSettings _hiddenLineSettings;
    private readonly ConduitViewRange _viewRange;
    private readonly Dictionary<string, OverrideGraphicSettings> _overrides = new();

    public ConduitDetailLevel DetailLevel { get; set; } = ConduitDetailLevel.Coarse;

    public Conduit2DRenderer(
        CoordinateTransformService transform,
        MEPHiddenLineSettings? hiddenLineSettings = null,
        ConduitViewRange? viewRange = null)
    {
        _transform = transform;
        _hiddenLineSettings = hiddenLineSettings ?? new MEPHiddenLineSettings();
        _viewRange = viewRange ?? new ConduitViewRange();
    }

    /// <summary>
    /// Sets graphic overrides for a specific element.
    /// </summary>
    public void SetOverride(string elementId, OverrideGraphicSettings overrides) =>
        _overrides[elementId] = overrides;

    /// <summary>
    /// Generates 2D render data for a conduit segment.
    /// </summary>
    public Conduit2DRenderData GenerateRenderData(ConduitSegment segment)
    {
        // Convert 3D endpoints to 2D document space (project to XY)
        var rwStart = new Point(segment.StartPoint.X, segment.StartPoint.Y);
        var rwEnd = new Point(segment.EndPoint.X, segment.EndPoint.Y);
        var docStart = _transform.IsCalibrated ? _transform.RealWorldToDocument(rwStart) : rwStart;
        var docEnd = _transform.IsCalibrated ? _transform.RealWorldToDocument(rwEnd) : rwEnd;

        var data = new Conduit2DRenderData
        {
            Start = docStart,
            End = docEnd,
            DetailLevel = DetailLevel
        };

        // Width for Fine detail
        if (DetailLevel == ConduitDetailLevel.Fine)
        {
            double diameterFeet = segment.Diameter / 12.0;
            data.Width = _transform.IsCalibrated
                ? diameterFeet * ((_transform.DocUnitsPerRealX + _transform.DocUnitsPerRealY) / 2.0)
                : segment.Diameter;
        }

        // Rise/drop symbol
        data.Symbol = _viewRange.GetSymbol(segment.StartPoint.Z, segment.EndPoint.Z);

        // Overrides
        _overrides.TryGetValue(segment.Id, out var overrides);
        data.Overrides = overrides;

        return data;
    }

    /// <summary>
    /// Computes hidden line gaps for a segment crossing over/under another.
    /// </summary>
    public void ApplyHiddenLineGaps(
        Conduit2DRenderData foreground,
        Conduit2DRenderData background,
        ConduitSegment fgSegment,
        ConduitSegment bgSegment)
    {
        if (!_hiddenLineSettings.ShowGaps) return;

        bool fgHigher = _hiddenLineSettings.HigherElementHides
            ? fgSegment.StartPoint.Z > bgSegment.StartPoint.Z
            : fgSegment.StartPoint.Z < bgSegment.StartPoint.Z;

        if (!fgHigher) return;

        // Find intersection point in 2D
        if (TryGetIntersection(foreground.Start, foreground.End,
            background.Start, background.End, out var intersection, out double tBg))
        {
            double halfGap = _hiddenLineSettings.GapDistance / 2.0;
            double segLen = Distance(background.Start, background.End);
            if (segLen < 1e-6) return;

            double gapStartT = Math.Max(0, tBg - halfGap / segLen);
            double gapEndT = Math.Min(1, tBg + halfGap / segLen);

            background.HiddenLineGaps.Add((gapStartT, gapEndT));
        }
    }

    private static bool TryGetIntersection(Point a1, Point a2, Point b1, Point b2,
        out Point intersection, out double tB)
    {
        intersection = default;
        tB = 0;

        double d1x = a2.X - a1.X, d1y = a2.Y - a1.Y;
        double d2x = b2.X - b1.X, d2y = b2.Y - b1.Y;
        double cross = d1x * d2y - d1y * d2x;

        if (Math.Abs(cross) < 1e-10) return false;

        double t = ((b1.X - a1.X) * d2y - (b1.Y - a1.Y) * d2x) / cross;
        double u = ((b1.X - a1.X) * d1y - (b1.Y - a1.Y) * d1x) / cross;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = new Point(a1.X + t * d1x, a1.Y + t * d1y);
            tB = u;
            return true;
        }

        return false;
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
