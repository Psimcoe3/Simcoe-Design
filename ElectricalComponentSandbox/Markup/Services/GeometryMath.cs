using System.Windows;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Geometry and math utilities for markup measurements and hit-testing.
/// All methods operate in Document-space (PDF points) unless noted.
/// </summary>
public static class GeometryMath
{
    /// <summary>
    /// Euclidean distance between two points
    /// </summary>
    public static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Total length of a polyline (sum of segment lengths)
    /// </summary>
    public static double PolylineLength(IReadOnlyList<Point> vertices)
    {
        if (vertices.Count < 2) return 0;

        double total = 0;
        for (int i = 1; i < vertices.Count; i++)
        {
            total += Distance(vertices[i - 1], vertices[i]);
        }
        return total;
    }

    /// <summary>
    /// Signed area of a polygon using the Shoelace formula.
    /// Positive if vertices are counter-clockwise.
    /// </summary>
    public static double SignedArea(IReadOnlyList<Point> vertices)
    {
        if (vertices.Count < 3) return 0;

        double sum = 0;
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % n];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }
        return sum / 2.0;
    }

    /// <summary>
    /// Absolute area of a polygon using the Shoelace formula
    /// </summary>
    public static double PolygonArea(IReadOnlyList<Point> vertices)
    {
        return Math.Abs(SignedArea(vertices));
    }

    /// <summary>
    /// Area with cutouts: outer polygon area minus sum of inner polygon areas
    /// </summary>
    public static double AreaWithCutouts(IReadOnlyList<Point> outerVertices, IEnumerable<IReadOnlyList<Point>> innerPolygons)
    {
        double outer = PolygonArea(outerVertices);
        double inner = 0;
        foreach (var poly in innerPolygons)
        {
            inner += PolygonArea(poly);
        }
        return Math.Max(0, outer - inner);
    }

    /// <summary>
    /// Volume = area * depth
    /// </summary>
    public static double Volume(double area, double depth)
    {
        return area * Math.Abs(depth);
    }

    /// <summary>
    /// Applies a slope factor to a measured length: adjusted = length * sqrt(1 + slope^2)
    /// where slope = rise/run
    /// </summary>
    public static double ApplySlopeFactor(double length, double slopeRatio)
    {
        return length * Math.Sqrt(1.0 + slopeRatio * slopeRatio);
    }

    /// <summary>
    /// Tests if a point is inside a polygon using the ray-casting algorithm
    /// </summary>
    public static bool PointInPolygon(Point test, IReadOnlyList<Point> polygon)
    {
        if (polygon.Count < 3) return false;

        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((polygon[i].Y > test.Y) != (polygon[j].Y > test.Y) &&
                test.X < (polygon[j].X - polygon[i].X) * (test.Y - polygon[i].Y) /
                          (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Minimum distance from a point to a line segment
    /// </summary>
    public static double PointToSegmentDistance(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < 1e-12)
            return Distance(p, a);

        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSq, 0, 1);
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return Distance(p, proj);
    }

    /// <summary>
    /// Tests if a point is inside a rectangle
    /// </summary>
    public static bool PointInRect(Point test, Rect rect)
    {
        return rect.Contains(test);
    }

    /// <summary>
    /// Tests if a point is inside a circle
    /// </summary>
    public static bool PointInCircle(Point test, Point center, double radius)
    {
        return Distance(test, center) <= radius;
    }

    /// <summary>
    /// Snaps an angle to the nearest multiple of 45° (for orthogonal/45° constraint).
    /// Input: unconstrained endpoint, anchor point.
    /// Returns: constrained endpoint.
    /// </summary>
    public static Point ConstrainAngle45(Point anchor, Point free)
    {
        double dx = free.X - anchor.X;
        double dy = free.Y - anchor.Y;
        double angle = Math.Atan2(dy, dx);
        double snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
        double dist = Distance(anchor, free);
        return new Point(
            anchor.X + dist * Math.Cos(snapped),
            anchor.Y + dist * Math.Sin(snapped));
    }

    /// <summary>
    /// Circumference of a circle
    /// </summary>
    public static double CircleCircumference(double radius)
    {
        return 2.0 * Math.PI * radius;
    }

    /// <summary>
    /// Area of a circle
    /// </summary>
    public static double CircleArea(double radius)
    {
        return Math.PI * radius * radius;
    }
}
