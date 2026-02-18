using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Geometry;

/// <summary>
/// Ramer-Douglas-Peucker polyline simplification and path conversion utilities.
/// Converts freehand sketched paths into precise curve paths for the 3D core.
/// </summary>
public static class PathSimplifier
{
    /// <summary>
    /// Simplifies a polyline using the Ramer-Douglas-Peucker algorithm.
    /// </summary>
    /// <param name="points">Input polyline vertices.</param>
    /// <param name="epsilon">Maximum perpendicular distance tolerance.</param>
    /// <returns>Simplified polyline.</returns>
    public static List<Point> RamerDouglasPeucker(IReadOnlyList<Point> points, double epsilon)
    {
        if (points.Count < 3) return new List<Point>(points);

        // Find point with max distance from line(start, end)
        double maxDist = 0;
        int maxIndex = 0;
        var start = points[0];
        var end = points[^1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double dist = PerpendicularDistance(points[i], start, end);
            if (dist > maxDist)
            {
                maxDist = dist;
                maxIndex = i;
            }
        }

        if (maxDist > epsilon)
        {
            // Recursive simplification
            var left = RamerDouglasPeucker(points.Take(maxIndex + 1).ToList(), epsilon);
            var right = RamerDouglasPeucker(points.Skip(maxIndex).ToList(), epsilon);

            // Combine (remove duplicate junction point)
            var result = new List<Point>(left);
            result.AddRange(right.Skip(1));
            return result;
        }
        else
        {
            // All points within tolerance, keep only endpoints
            return new List<Point> { start, end };
        }
    }

    /// <summary>
    /// Perpendicular distance from point P to line segment AB.
    /// </summary>
    public static double PerpendicularDistance(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-12)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        double area2 = Math.Abs(dx * (a.Y - p.Y) - (a.X - p.X) * dy);
        return area2 / Math.Sqrt(lenSq);
    }

    /// <summary>
    /// Orthogonalizes a simplified polyline: snaps each segment angle
    /// to the nearest multiple of 45° (or 90° if orthoOnly is true).
    /// </summary>
    public static List<Point> Orthogonalize(IReadOnlyList<Point> points, bool orthoOnly = false)
    {
        if (points.Count < 2) return new List<Point>(points);

        var result = new List<Point> { points[0] };
        double snapAngle = orthoOnly ? Math.PI / 2 : Math.PI / 4;

        for (int i = 1; i < points.Count; i++)
        {
            var prev = result[^1];
            double dx = points[i].X - prev.X;
            double dy = points[i].Y - prev.Y;
            double angle = Math.Atan2(dy, dx);
            double snapped = Math.Round(angle / snapAngle) * snapAngle;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            result.Add(new Point(
                prev.X + dist * Math.Cos(snapped),
                prev.Y + dist * Math.Sin(snapped)));
        }

        return result;
    }

    /// <summary>
    /// Converts 2D document-space points to 3D XYZ coordinates on the Z=elevation plane.
    /// </summary>
    public static List<XYZ> To3DPath(IReadOnlyList<Point> points2D, double elevation,
        Func<Point, Point>? docToRealWorld = null)
    {
        return points2D.Select(p =>
        {
            var rw = docToRealWorld != null ? docToRealWorld(p) : p;
            return new XYZ(rw.X, rw.Y, elevation);
        }).ToList();
    }

    /// <summary>
    /// Creates conduit segments from an ordered list of 3D points.
    /// </summary>
    public static List<ConduitSegment> CreateSegmentsFromPath(
        IReadOnlyList<XYZ> path3D,
        string conduitTypeId,
        string tradeSize = "1/2",
        ConduitMaterialType material = ConduitMaterialType.EMT,
        string levelId = "Level 1")
    {
        var segments = new List<ConduitSegment>();

        for (int i = 0; i < path3D.Count - 1; i++)
        {
            var seg = new ConduitSegment
            {
                StartPoint = path3D[i],
                EndPoint = path3D[i + 1],
                ConduitTypeId = conduitTypeId,
                TradeSize = tradeSize,
                Material = material,
                LevelId = levelId
            };

            // Set diameter from trade size
            var sizes = ConduitSizeSettings.CreateDefaultEMT();
            var sizeInfo = sizes.GetSize(tradeSize);
            if (sizeInfo != null) seg.Diameter = sizeInfo.OuterDiameter;

            segments.Add(seg);
        }

        return segments;
    }
}
