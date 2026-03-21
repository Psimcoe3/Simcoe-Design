using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides snap-to functionality for 2D drawing: endpoints, midpoints, intersections,
/// nearest-on-curve, perpendicular, center, tangent, and quadrant — professional CAD OSNAP suite.
/// </summary>
public class SnapService
{
    // ── Snap radius (screen pixels) ──────────────────────────────────────────
    public double SnapRadius { get; set; } = 10.0;

    // ── Toggle flags: basic modes ─────────────────────────────────────────────
    public bool SnapToEndpoints    { get; set; } = true;
    public bool SnapToMidpoints    { get; set; } = true;
    public bool SnapToIntersections{ get; set; } = true;

    // ── Toggle flags: extended OSNAP modes ───────────────────────────────────
    /// <summary>Nearest point on any segment</summary>
    public bool SnapToNearest      { get; set; } = false;
    /// <summary>Perpendicular drop from last point onto a segment</summary>
    public bool SnapToPerpendicular{ get; set; } = true;
    /// <summary>Centre of a circle / arc markup</summary>
    public bool SnapToCenter       { get; set; } = true;
    /// <summary>Quadrant points (0°, 90°, 180°, 270°) of a circle / arc</summary>
    public bool SnapToQuadrant     { get; set; } = true;
    /// <summary>Tangent from last point onto a circle / arc</summary>
    public bool SnapToTangent      { get; set; } = false;

    // ── Inner types ───────────────────────────────────────────────────────────

    /// <summary>Result of a snap operation</summary>
    public class SnapResult
    {
        public Point SnappedPoint { get; set; }
        public SnapType Type { get; set; }
        public bool Snapped { get; set; }
    }

    public enum SnapType
    {
        None,
        Endpoint,
        Midpoint,
        Intersection,
        Grid,
        Nearest,
        Perpendicular,
        Center,
        Quadrant,
        Tangent
    }
    
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the nearest snap point given a cursor position and a set of geometry points.
    /// <paramref name="lastPoint"/> (optional) enables Perpendicular and Tangent snaps.
    /// <paramref name="circles"/> (optional) enables Center, Quadrant, and Tangent snaps.
    /// </summary>
    public SnapResult FindSnapPoint(
        Point cursor,
        IEnumerable<Point> endpoints,
        IEnumerable<(Point A, Point B)> segments,
        Point? lastPoint = null,
        IEnumerable<(Point Center, double Radius)>? circles = null)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;
        var segList = segments.ToList();
        var circleList = circles?.ToList() ?? new List<(Point, double)>();

        // 1. Endpoints
        if (SnapToEndpoints)
        {
            foreach (var ep in endpoints)
            {
                double dist = Distance(cursor, ep);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    result = new SnapResult { SnappedPoint = ep, Type = SnapType.Endpoint, Snapped = true };
                }
            }
        }

        // 2. Midpoints
        if (SnapToMidpoints)
        {
            foreach (var seg in segList)
            {
                var mid = new Point((seg.A.X + seg.B.X) / 2, (seg.A.Y + seg.B.Y) / 2);
                double dist = Distance(cursor, mid);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    result = new SnapResult { SnappedPoint = mid, Type = SnapType.Midpoint, Snapped = true };
                }
            }
        }

        // 3. Intersections
        if (SnapToIntersections)
        {
            for (int i = 0; i < segList.Count; i++)
            {
                for (int j = i + 1; j < segList.Count; j++)
                {
                    if (TryGetIntersection(segList[i].A, segList[i].B, segList[j].A, segList[j].B, out var intersection))
                    {
                        double dist = Distance(cursor, intersection);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            result = new SnapResult { SnappedPoint = intersection, Type = SnapType.Intersection, Snapped = true };
                        }
                    }
                }
            }
        }

        // 4. Nearest-on-segment
        if (SnapToNearest)
        {
            var nearResult = FindNearestOnPath(cursor, segList);
            if (nearResult.Snapped && Distance(cursor, nearResult.SnappedPoint) < bestDist)
            {
                bestDist = Distance(cursor, nearResult.SnappedPoint);
                result = nearResult;
            }
        }

        // 5. Center of circles / arcs
        if (SnapToCenter && circleList.Count > 0)
        {
            var centerResult = FindCenter(cursor, circleList);
            if (centerResult.Snapped && Distance(cursor, centerResult.SnappedPoint) < bestDist)
            {
                bestDist = Distance(cursor, centerResult.SnappedPoint);
                result = centerResult;
            }
        }

        // 6. Quadrant points
        if (SnapToQuadrant && circleList.Count > 0)
        {
            foreach (var (center, radius) in circleList)
            {
                Point[] quadrants =
                {
                    new(center.X + radius, center.Y),
                    new(center.X,          center.Y + radius),
                    new(center.X - radius, center.Y),
                    new(center.X,          center.Y - radius)
                };
                foreach (var q in quadrants)
                {
                    double dist = Distance(cursor, q);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        result = new SnapResult { SnappedPoint = q, Type = SnapType.Quadrant, Snapped = true };
                    }
                }
            }
        }

        // 7. Perpendicular from last point
        if (SnapToPerpendicular && lastPoint.HasValue && segList.Count > 0)
        {
            var perpResult = FindPerpendicular(cursor, lastPoint.Value, segList);
            if (perpResult.Snapped && Distance(cursor, perpResult.SnappedPoint) < bestDist)
            {
                bestDist = Distance(cursor, perpResult.SnappedPoint);
                result = perpResult;
            }
        }

        // 8. Tangent from last point onto a circle
        if (SnapToTangent && lastPoint.HasValue && circleList.Count > 0)
        {
            var tangentResult = FindTangent(cursor, lastPoint.Value, circleList);
            if (tangentResult.Snapped && Distance(cursor, tangentResult.SnappedPoint) < bestDist)
            {
                result = tangentResult;
            }
        }

        return result;
    }

    // ── Extended OSNAP helpers ────────────────────────────────────────────────

    /// <summary>Returns the nearest point along any segment to the cursor.</summary>
    public SnapResult FindNearestOnPath(Point cursor, IEnumerable<(Point A, Point B)> segments)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = double.MaxValue;

        foreach (var (a, b) in segments)
        {
            var closest = ClosestPointOnSegment(cursor, a, b);
            double dist = Distance(cursor, closest);
            if (dist < bestDist)
            {
                bestDist = dist;
                result = new SnapResult { SnappedPoint = closest, Type = SnapType.Nearest, Snapped = true };
            }
        }

        return result.Snapped && bestDist <= SnapRadius ? result
             : new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
    }

    /// <summary>Returns the center(s) of circles whose center falls within SnapRadius of the cursor.</summary>
    public SnapResult FindCenter(Point cursor, IEnumerable<(Point Center, double Radius)> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        foreach (var (center, _) in circles)
        {
            double dist = Distance(cursor, center);
            if (dist < bestDist)
            {
                bestDist = dist;
                result = new SnapResult { SnappedPoint = center, Type = SnapType.Center, Snapped = true };
            }
        }

        return result;
    }

    /// <summary>
    /// Drops a perpendicular from <paramref name="fromPoint"/> onto each segment and returns the
    /// candidate closest to the cursor.
    /// </summary>
    public SnapResult FindPerpendicular(Point cursor, Point fromPoint, IEnumerable<(Point A, Point B)> segments)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        foreach (var (a, b) in segments)
        {
            // Project fromPoint onto the infinite line through (A,B)
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-10) continue;

            double t = ((fromPoint.X - a.X) * dx + (fromPoint.Y - a.Y) * dy) / lenSq;
            // Clamp to segment extent
            t = Math.Max(0, Math.Min(1, t));
            var foot = new Point(a.X + t * dx, a.Y + t * dy);

            double dist = Distance(cursor, foot);
            if (dist < bestDist)
            {
                bestDist = dist;
                result = new SnapResult { SnappedPoint = foot, Type = SnapType.Perpendicular, Snapped = true };
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the tangent touch-point on a circle closest to the cursor when drawing from
    /// <paramref name="fromPoint"/>.  Returns the tangent point (not fromPoint).
    /// </summary>
    public SnapResult FindTangent(Point cursor, Point fromPoint, IEnumerable<(Point Center, double Radius)> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        foreach (var (center, radius) in circles)
        {
            double d = Distance(fromPoint, center);
            if (d < radius) continue;  // fromPoint is inside the circle

            double tangentLen = Math.Sqrt(d * d - radius * radius);
            if (tangentLen < 1e-6) continue;

            // Angle from fromPoint to center
            double angle = Math.Atan2(center.Y - fromPoint.Y, center.X - fromPoint.X);
            double alpha = Math.Asin(radius / d);  // half-angle to tangent

            // Two tangent contact points on the circle
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                double touchAngle = angle + sign * (Math.PI / 2 - alpha);
                var touch = new Point(
                    center.X - radius * Math.Sin(touchAngle - Math.PI / 2),
                    center.Y + radius * Math.Cos(touchAngle - Math.PI / 2));

                // Re-derive more accurately: tangent contact point
                double beta = angle + sign * alpha;
                var contactPoint = new Point(
                    fromPoint.X + tangentLen * Math.Cos(beta),
                    fromPoint.Y + tangentLen * Math.Sin(beta));

                // Use the point on the circle circumference closest to contactPoint
                double ca = Math.Atan2(contactPoint.Y - center.Y, contactPoint.X - center.X);
                var circlePoint = new Point(center.X + radius * Math.Cos(ca), center.Y + radius * Math.Sin(ca));

                double dist = Distance(cursor, circlePoint);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    result = new SnapResult { SnappedPoint = circlePoint, Type = SnapType.Tangent, Snapped = true };
                }
            }
        }

        return result;
    }

    // ── Line geometry helpers (static, reusable by other services) ────────────

    /// <summary>
    /// Attempts to find the intersection point of two line segments.
    /// </summary>
    public static bool TryGetIntersection(Point a1, Point a2, Point b1, Point b2, out Point intersection)
    {
        intersection = default;

        double d1x = a2.X - a1.X, d1y = a2.Y - a1.Y;
        double d2x = b2.X - b1.X, d2y = b2.Y - b1.Y;

        double cross = d1x * d2y - d1y * d2x;
        if (Math.Abs(cross) < 1e-10) return false;

        double t = ((b1.X - a1.X) * d2y - (b1.Y - a1.Y) * d2x) / cross;
        double u = ((b1.X - a1.X) * d1y - (b1.Y - a1.Y) * d1x) / cross;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = new Point(a1.X + t * d1x, a1.Y + t * d1y);
            return true;
        }

        return false;
    }

    /// <summary>Returns the closest point on the segment [A, B] to <paramref name="p"/>.</summary>
    public static Point ClosestPointOnSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-10) return a;
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
        return new Point(a.X + t * dx, a.Y + t * dy);
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
