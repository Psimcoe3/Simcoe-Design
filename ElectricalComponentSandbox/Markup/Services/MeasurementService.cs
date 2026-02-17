using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Computes calibrated measurements for markup records:
/// length, area, cutouts, volume, and optional slope factor.
/// </summary>
public class MeasurementService
{
    private readonly CoordinateTransformService _transform;

    public MeasurementService(CoordinateTransformService transform)
    {
        _transform = transform;
    }

    /// <summary>
    /// Measures the real-world length of a polyline or conduit-run markup
    /// </summary>
    public double MeasureLength(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 2) return 0;

        double totalReal = 0;
        for (int i = 1; i < markup.Vertices.Count; i++)
        {
            double dx = markup.Vertices[i].X - markup.Vertices[i - 1].X;
            double dy = markup.Vertices[i].Y - markup.Vertices[i - 1].Y;
            totalReal += _transform.DocumentToRealWorldDistance(dx, dy);
        }
        return totalReal;
    }

    /// <summary>
    /// Measures the real-world area of a polygon markup using the Shoelace formula
    /// </summary>
    public double MeasureArea(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 3) return 0;

        // Convert vertices to real-world coordinates
        var realVertices = markup.Vertices
            .Select(v => _transform.DocumentToRealWorld(v))
            .ToList();

        return GeometryMath.PolygonArea(realVertices);
    }

    /// <summary>
    /// Measures the area of a rectangle markup
    /// </summary>
    public double MeasureRectangleArea(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 2) return 0;

        var rw1 = _transform.DocumentToRealWorld(markup.Vertices[0]);
        var rw2 = _transform.DocumentToRealWorld(markup.Vertices[1]);

        return Math.Abs(rw2.X - rw1.X) * Math.Abs(rw2.Y - rw1.Y);
    }

    /// <summary>
    /// Measures the area of a circle markup
    /// </summary>
    public double MeasureCircleArea(MarkupRecord markup)
    {
        if (markup.Vertices.Count < 1) return 0;

        double realRadius = _transform.DocumentToRealWorldDistance(markup.Radius);
        return GeometryMath.CircleArea(realRadius);
    }

    /// <summary>
    /// Computes area with cutouts: outer polygon minus inner polygons
    /// </summary>
    public double MeasureAreaWithCutouts(MarkupRecord outer, IEnumerable<MarkupRecord> inners)
    {
        var outerRW = outer.Vertices
            .Select(v => _transform.DocumentToRealWorld(v))
            .ToList();

        var innerPolys = inners.Select(m =>
            (IReadOnlyList<Point>)m.Vertices
                .Select(v => _transform.DocumentToRealWorld(v))
                .ToList());

        return GeometryMath.AreaWithCutouts(outerRW, innerPolys);
    }

    /// <summary>
    /// Volume = area × depth (depth from markup metadata, in real-world units)
    /// </summary>
    public double MeasureVolume(MarkupRecord markup)
    {
        double area = MeasureArea(markup);
        return GeometryMath.Volume(area, markup.Metadata.Depth);
    }

    /// <summary>
    /// Applies optional slope factor to a measured length
    /// </summary>
    public double MeasureLengthWithSlope(MarkupRecord markup, double slopeRatio)
    {
        double length = MeasureLength(markup);
        return GeometryMath.ApplySlopeFactor(length, slopeRatio);
    }

    /// <summary>
    /// Returns a formatted measurement summary for a markup
    /// </summary>
    public string GetMeasurementSummary(MarkupRecord markup)
    {
        switch (markup.Type)
        {
            case MarkupType.Polyline:
            case MarkupType.ConduitRun:
                double len = MeasureLength(markup);
                return $"Length: {len:F2} ft";

            case MarkupType.Polygon:
            case MarkupType.Box:
            case MarkupType.Panel:
                double area = MeasureArea(markup);
                string summary = $"Area: {area:F2} sq ft";
                if (markup.Metadata.Depth > 0)
                    summary += $", Volume: {MeasureVolume(markup):F2} cu ft";
                return summary;

            case MarkupType.Rectangle:
                return $"Area: {MeasureRectangleArea(markup):F2} sq ft";

            case MarkupType.Circle:
                return $"Area: {MeasureCircleArea(markup):F2} sq ft";

            case MarkupType.Text:
                return markup.TextContent;

            default:
                return string.Empty;
        }
    }
}
