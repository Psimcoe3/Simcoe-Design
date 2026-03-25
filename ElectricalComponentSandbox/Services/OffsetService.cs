using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Direction of the offset relative to the polyline travel direction.
/// </summary>
public enum OffsetDirection
{
    Left,
    Right
}

/// <summary>
/// Contains the result of an offset operation, including original and offset geometry.
/// </summary>
public class OffsetResult
{
    public List<Point3D> OriginalPoints { get; set; } = new();
    public List<Point3D> OffsetPoints { get; set; } = new();
    public double OffsetDistance { get; set; }
    public OffsetDirection Direction { get; set; }
}

/// <summary>
/// Creates parallel copies of conduit runs and linear geometry at a specified offset distance.
/// All offset calculations are performed in the XZ plane (Y is elevation and remains unchanged).
/// </summary>
public static class OffsetService
{
    // ── Tolerance for parallel-line detection ───────────────────────────────
    private const double ParallelTolerance = 1e-10;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Offsets a polyline of 3D points by <paramref name="distance"/> perpendicular to each
    /// segment in the XZ plane. Corners are resolved by intersecting adjacent offset lines;
    /// parallel segments fall back to a simple perpendicular shift.
    /// </summary>
    public static List<Point3D> OffsetPolyline(
        IReadOnlyList<Point3D> points,
        double distance,
        OffsetDirection direction)
    {
        if (points.Count == 0)
            return new List<Point3D>();

        if (points.Count == 1)
            return new List<Point3D> { points[0] };

        // Sign convention: Left uses the left-hand normal directly; Right negates it.
        double sign = direction == OffsetDirection.Left ? 1.0 : -1.0;

        int segmentCount = points.Count - 1;

        // Pre-compute the offset line for each segment: a point on the offset line and its direction.
        var offsetPoints = new Point3D[segmentCount];
        var segmentDirs = new Vector3D[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            var normal = ComputeSegmentNormalXZ(points[i], points[i + 1]);
            var offset = new Vector3D(normal.X * distance * sign, 0, normal.Z * distance * sign);

            offsetPoints[i] = new Point3D(
                points[i].X + offset.X,
                points[i].Y,
                points[i].Z + offset.Z);

            segmentDirs[i] = new Vector3D(
                points[i + 1].X - points[i].X,
                0,
                points[i + 1].Z - points[i].Z);
        }

        var result = new List<Point3D>(points.Count);

        // First point: simply offset from the first segment.
        result.Add(offsetPoints[0]);

        // Interior points: intersect adjacent offset lines to find the corner.
        for (int i = 0; i < segmentCount - 1; i++)
        {
            var intersection = IntersectOffsetLines(
                offsetPoints[i], segmentDirs[i],
                offsetPoints[i + 1], segmentDirs[i + 1]);

            if (intersection.HasValue)
            {
                // Preserve the original Y (elevation) at this vertex.
                result.Add(new Point3D(intersection.Value.X, points[i + 1].Y, intersection.Value.Z));
            }
            else
            {
                // Segments are parallel — use the simple offset of the shared vertex.
                var normal = ComputeSegmentNormalXZ(points[i], points[i + 1]);
                result.Add(new Point3D(
                    points[i + 1].X + normal.X * distance * sign,
                    points[i + 1].Y,
                    points[i + 1].Z + normal.Z * distance * sign));
            }
        }

        // Last point: offset along the last segment's normal.
        {
            int last = segmentCount - 1;
            var normal = ComputeSegmentNormalXZ(points[last], points[last + 1]);
            result.Add(new Point3D(
                points[last + 1].X + normal.X * distance * sign,
                points[last + 1].Y,
                points[last + 1].Z + normal.Z * distance * sign));
        }

        return result;
    }

    /// <summary>
    /// Offsets an existing <see cref="ConduitComponent"/> by transforming its path points
    /// to world space, computing the offset polyline, and returning the result.
    /// </summary>
    public static OffsetResult OffsetConduit(
        ConduitComponent conduit,
        double distance,
        OffsetDirection direction)
    {
        var localPoints = conduit.GetPathPoints();

        // Transform local path points to world space by adding the conduit's position.
        var worldPoints = localPoints
            .Select(p => new Point3D(
                p.X + conduit.Position.X,
                p.Y + conduit.Position.Y,
                p.Z + conduit.Position.Z))
            .ToList();

        var offsetPoints = OffsetPolyline(worldPoints, distance, direction);

        return new OffsetResult
        {
            OriginalPoints = worldPoints,
            OffsetPoints = offsetPoints,
            OffsetDistance = distance,
            Direction = direction
        };
    }

    /// <summary>
    /// Creates a new <see cref="ConduitComponent"/> that is a parallel copy of
    /// <paramref name="source"/>, offset by <paramref name="distance"/> in the given direction.
    /// The new component receives a unique Id and an "(Offset)" name suffix.
    /// </summary>
    public static ConduitComponent CreateParallelConduit(
        ConduitComponent source,
        double distance,
        OffsetDirection direction)
    {
        var result = OffsetConduit(source, distance, direction);
        var offsetPts = result.OffsetPoints;

        // The new position is the first offset point.
        var newPosition = offsetPts.Count > 0
            ? offsetPts[0]
            : source.Position;

        // Bend points are the remaining offset points expressed relative to the new position.
        var bendPoints = offsetPts
            .Skip(1)
            .Select(p => new Point3D(
                p.X - newPosition.X,
                p.Y - newPosition.Y,
                p.Z - newPosition.Z))
            .ToList();

        return new ConduitComponent
        {
            Id = Guid.NewGuid().ToString(),
            Name = source.Name + " (Offset)",
            Diameter = source.Diameter,
            ConduitType = source.ConduitType,
            BendRadius = source.BendRadius,
            BendType = source.BendType,
            Length = source.Length,
            Position = newPosition,
            BendPoints = bendPoints
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the left-hand perpendicular normal of the segment from <paramref name="a"/>
    /// to <paramref name="b"/> in the XZ plane, normalised to unit length.
    /// For a segment direction (dx, dz) the left-hand normal is (-dz, 0, dx).
    /// </summary>
    public static Vector3D ComputeSegmentNormalXZ(Point3D a, Point3D b)
    {
        double dx = b.X - a.X;
        double dz = b.Z - a.Z;

        double length = Math.Sqrt(dx * dx + dz * dz);
        if (length < ParallelTolerance)
            return new Vector3D(0, 0, 0);

        // Left-hand normal in XZ: (-dz, 0, dx), then normalise.
        return new Vector3D(-dz / length, 0, dx / length);
    }

    /// <summary>
    /// Intersects two infinite lines in the XZ plane. Each line is defined by a point and
    /// a direction vector. Returns <c>null</c> when the lines are parallel.
    /// </summary>
    public static Point3D? IntersectOffsetLines(
        Point3D p1, Vector3D d1,
        Point3D p2, Vector3D d2)
    {
        // Solve: p1 + t * d1 == p2 + s * d2  (in XZ)
        // Cross product of d1 and d2 in 2D: d1.X * d2.Z - d1.Z * d2.X
        double cross = d1.X * d2.Z - d1.Z * d2.X;
        if (Math.Abs(cross) < ParallelTolerance)
            return null;

        double dpX = p2.X - p1.X;
        double dpZ = p2.Z - p1.Z;

        double t = (dpX * d2.Z - dpZ * d2.X) / cross;

        return new Point3D(
            p1.X + t * d1.X,
            p1.Y,
            p1.Z + t * d1.Z);
    }
}
