using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Hit-testing for markup records. Determines which markup a cursor is over.
/// Tolerance is specified in screen pixels and converted via the coordinate service.
/// </summary>
public class HitTestService
{
    /// <summary>
    /// Hit-test tolerance in document-space units
    /// </summary>
    public double Tolerance { get; set; } = 5.0;

    /// <summary>
    /// Tests whether a point (in Document space) hits a markup
    /// </summary>
    public bool HitTest(Point docPoint, MarkupRecord markup)
    {
        switch (markup.Type)
        {
            case MarkupType.Polyline:
                return HitTestPolyline(docPoint, markup.Vertices);

            case MarkupType.Polygon:
            case MarkupType.Box:
            case MarkupType.Panel:
                return HitTestPolygon(docPoint, markup.Vertices);

            case MarkupType.Rectangle:
                return HitTestRectangle(docPoint, markup);

            case MarkupType.Circle:
                return HitTestCircle(docPoint, markup);

            case MarkupType.ConduitRun:
                return HitTestPolyline(docPoint, markup.Vertices);

            case MarkupType.Text:
                markup.UpdateBoundingRect();
                return GeometryMath.PointInRect(docPoint, markup.BoundingRect);

            default:
                return false;
        }
    }

    /// <summary>
    /// Finds the top-most markup hit at a point (last in list = top-most)
    /// </summary>
    public MarkupRecord? FindTopHit(Point docPoint, IReadOnlyList<MarkupRecord> markups)
    {
        for (int i = markups.Count - 1; i >= 0; i--)
        {
            if (HitTest(docPoint, markups[i]))
                return markups[i];
        }
        return null;
    }

    private bool HitTestPolyline(Point p, IReadOnlyList<Point> vertices)
    {
        if (vertices.Count < 2) return false;
        for (int i = 1; i < vertices.Count; i++)
        {
            if (GeometryMath.PointToSegmentDistance(p, vertices[i - 1], vertices[i]) <= Tolerance)
                return true;
        }
        return false;
    }

    private bool HitTestPolygon(Point p, IReadOnlyList<Point> vertices)
    {
        if (vertices.Count < 3) return false;
        // First check fill area
        if (GeometryMath.PointInPolygon(p, vertices))
            return true;
        // Then check edges for near-miss
        for (int i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            if (GeometryMath.PointToSegmentDistance(p, a, b) <= Tolerance)
                return true;
        }
        return false;
    }

    private static bool HitTestRectangle(Point p, MarkupRecord markup)
    {
        if (markup.Vertices.Count >= 2)
        {
            var rect = new Rect(markup.Vertices[0], markup.Vertices[1]);
            return rect.Contains(p);
        }
        return markup.BoundingRect.Contains(p);
    }

    private static bool HitTestCircle(Point p, MarkupRecord markup)
    {
        if (markup.Vertices.Count < 1) return false;
        return GeometryMath.PointInCircle(p, markup.Vertices[0], markup.Radius);
    }
}
