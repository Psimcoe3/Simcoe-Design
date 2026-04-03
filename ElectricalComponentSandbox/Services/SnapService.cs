using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides snap-to functionality for 2D drawing: endpoints, midpoints, intersections,
/// nearest-on-curve, perpendicular, center, tangent, and quadrant — professional CAD OSNAP suite.
/// </summary>
public readonly record struct SnapCircle(Point Center, double Radius, double? StartAngleDeg = null, double? SweepAngleDeg = null);

public class SnapService
{
    // ── Snap radius (screen pixels) ──────────────────────────────────────────
    public double SnapRadius { get; set; } = 10.0;

    // ── Master on/off (F3) ────────────────────────────────────────────────────
    public bool IsEnabled { get; set; } = true;

    // ── Toggle flags: basic modes ─────────────────────────────────────────────
    public bool SnapToEndpoints    { get; set; } = true;
    public bool SnapToMidpoints    { get; set; } = true;
    public bool SnapToIntersections{ get; set; } = true;

    // ── Toggle flags: extended OSNAP modes ───────────────────────────────────
    /// <summary>Nearest point on any segment</summary>
    public bool SnapToNearest      { get; set; } = false;
    /// <summary>Perpendicular drop from last point onto a segment or visible circle/arc</summary>
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
        IEnumerable<SnapCircle>? circles = null)
    {
        if (!IsEnabled)
            return new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };

        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;
        var bestPriority = GetPriority(result.Type);
        var segList = segments.ToList();
        var circleList = circles?.ToList() ?? new List<SnapCircle>();

        // 1. Endpoints
        if (SnapToEndpoints)
        {
            foreach (var ep in endpoints)
            {
                double dist = Distance(cursor, ep);
                if (IsBetterCandidate(dist, SnapType.Endpoint, bestDist, bestPriority))
                {
                    bestDist = dist;
                    bestPriority = GetPriority(SnapType.Endpoint);
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
                if (IsBetterCandidate(dist, SnapType.Midpoint, bestDist, bestPriority))
                {
                    bestDist = dist;
                    bestPriority = GetPriority(SnapType.Midpoint);
                    result = new SnapResult { SnappedPoint = mid, Type = SnapType.Midpoint, Snapped = true };
                }
            }

            foreach (var circle in circleList)
            {
                if (!TryGetArcMidpoint(circle, out var midpoint))
                    continue;

                double dist = Distance(cursor, midpoint);
                if (IsBetterCandidate(dist, SnapType.Midpoint, bestDist, bestPriority))
                {
                    bestDist = dist;
                    bestPriority = GetPriority(SnapType.Midpoint);
                    result = new SnapResult { SnappedPoint = midpoint, Type = SnapType.Midpoint, Snapped = true };
                }
            }
        }

        // 3. Intersections
        if (SnapToIntersections)
        {
            var intersectionResult = FindIntersections(cursor, segList, circleList);
            var intersectionDist = Distance(cursor, intersectionResult.SnappedPoint);
            if (intersectionResult.Snapped && IsBetterCandidate(intersectionDist, intersectionResult.Type, bestDist, bestPriority))
            {
                bestDist = intersectionDist;
                bestPriority = GetPriority(intersectionResult.Type);
                result = intersectionResult;
            }
        }

        // 4. Nearest-on-segment
        if (SnapToNearest)
        {
            var nearResult = FindNearestOnPath(cursor, segList);
            var nearDist = Distance(cursor, nearResult.SnappedPoint);
            if (nearResult.Snapped && IsBetterCandidate(nearDist, nearResult.Type, bestDist, bestPriority))
            {
                bestDist = nearDist;
                bestPriority = GetPriority(nearResult.Type);
                result = nearResult;
            }

            if (circleList.Count > 0)
            {
                var nearCircleResult = FindNearestOnCircles(cursor, circleList);
                var nearCircleDist = Distance(cursor, nearCircleResult.SnappedPoint);
                if (nearCircleResult.Snapped && IsBetterCandidate(nearCircleDist, nearCircleResult.Type, bestDist, bestPriority))
                {
                    bestDist = nearCircleDist;
                    bestPriority = GetPriority(nearCircleResult.Type);
                    result = nearCircleResult;
                }
            }
        }

        // 5. Center of circles / arcs
        if (SnapToCenter && circleList.Count > 0)
        {
            var centerResult = FindCenter(cursor, circleList);
            var centerDist = Distance(cursor, centerResult.SnappedPoint);
            if (centerResult.Snapped && IsBetterCandidate(centerDist, centerResult.Type, bestDist, bestPriority))
            {
                bestDist = centerDist;
                bestPriority = GetPriority(centerResult.Type);
                result = centerResult;
            }
        }

        // 6. Quadrant points
        if (SnapToQuadrant && circleList.Count > 0)
        {
            foreach (var circle in circleList)
            {
                var center = circle.Center;
                var radius = circle.Radius;
                Point[] quadrants =
                {
                    new(center.X + radius, center.Y),
                    new(center.X,          center.Y + radius),
                    new(center.X - radius, center.Y),
                    new(center.X,          center.Y - radius)
                };
                double[] quadrantAngles = { 0.0, 90.0, 180.0, 270.0 };
                for (int index = 0; index < quadrants.Length; index++)
                {
                    if (!ContainsAngle(circle, quadrantAngles[index]))
                        continue;

                    var q = quadrants[index];
                    double dist = Distance(cursor, q);
                    if (IsBetterCandidate(dist, SnapType.Quadrant, bestDist, bestPriority))
                    {
                        bestDist = dist;
                        bestPriority = GetPriority(SnapType.Quadrant);
                        result = new SnapResult { SnappedPoint = q, Type = SnapType.Quadrant, Snapped = true };
                    }
                }
            }
        }

        // 7. Perpendicular from last point
        if (SnapToPerpendicular && lastPoint.HasValue && (segList.Count > 0 || circleList.Count > 0))
        {
            var perpResult = FindPerpendicular(cursor, lastPoint.Value, segList, circleList);
            var perpDist = Distance(cursor, perpResult.SnappedPoint);
            if (perpResult.Snapped && IsBetterCandidate(perpDist, perpResult.Type, bestDist, bestPriority))
            {
                bestDist = perpDist;
                bestPriority = GetPriority(perpResult.Type);
                result = perpResult;
            }
        }

        // 8. Tangent from last point onto a circle
        if (SnapToTangent && lastPoint.HasValue && circleList.Count > 0)
        {
            var tangentResult = FindTangent(cursor, lastPoint.Value, circleList);
            var tangentDist = Distance(cursor, tangentResult.SnappedPoint);
            if (tangentResult.Snapped && IsBetterCandidate(tangentDist, tangentResult.Type, bestDist, bestPriority))
            {
                bestPriority = GetPriority(tangentResult.Type);
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

    /// <summary>Returns the nearest point along any circle or visible arc to the cursor.</summary>
    public SnapResult FindNearestOnCircles(Point cursor, IEnumerable<SnapCircle> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = double.MaxValue;

        foreach (var circle in circles)
        {
            var closest = ClosestPointOnCircle(cursor, circle);
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
    public SnapResult FindCenter(Point cursor, IEnumerable<SnapCircle> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        foreach (var circle in circles)
        {
            var center = circle.Center;
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
        return FindPerpendicular(cursor, fromPoint, segments, null);
    }

    /// <summary>
    /// Drops a perpendicular from <paramref name="fromPoint"/> onto each segment or visible circle/arc
    /// and returns the candidate closest to the cursor.
    /// </summary>
    public SnapResult FindPerpendicular(
        Point cursor,
        Point fromPoint,
        IEnumerable<(Point A, Point B)> segments,
        IEnumerable<SnapCircle>? circles)
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

        foreach (var circle in circles ?? Array.Empty<SnapCircle>())
        {
            var radial = new Vector(fromPoint.X - circle.Center.X, fromPoint.Y - circle.Center.Y);
            if (radial.LengthSquared < 1e-10)
                continue;

            var baseAngleDeg = Math.Atan2(radial.Y, radial.X) * 180.0 / Math.PI;
            foreach (var candidateAngleDeg in new[] { baseAngleDeg, baseAngleDeg + 180.0 })
            {
                if (!ContainsAngle(circle, candidateAngleDeg))
                    continue;

                var foot = PointOnCircle(circle.Center, circle.Radius, candidateAngleDeg);
                double dist = Distance(cursor, foot);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    result = new SnapResult { SnappedPoint = foot, Type = SnapType.Perpendicular, Snapped = true };
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the tangent touch-point on a circle closest to the cursor when drawing from
    /// <paramref name="fromPoint"/>.  Returns the tangent point (not fromPoint).
    /// </summary>
    public SnapResult FindTangent(Point cursor, Point fromPoint, IEnumerable<SnapCircle> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        foreach (var circle in circles)
        {
            var center = circle.Center;
            var radius = circle.Radius;
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

                if (!ContainsAngle(circle, ca * 180.0 / Math.PI))
                    continue;

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

    private SnapResult FindIntersections(Point cursor, IReadOnlyList<(Point A, Point B)> segments, IReadOnlyList<SnapCircle> circles)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;

        void Consider(Point candidate)
        {
            double dist = Distance(cursor, candidate);
            if (dist <= bestDist)
            {
                bestDist = dist;
                result = new SnapResult { SnappedPoint = candidate, Type = SnapType.Intersection, Snapped = true };
            }
        }

        for (int i = 0; i < segments.Count; i++)
        {
            for (int j = i + 1; j < segments.Count; j++)
            {
                if (TryGetIntersection(segments[i].A, segments[i].B, segments[j].A, segments[j].B, out var intersection))
                    Consider(intersection);
            }

            foreach (var circle in circles)
            {
                foreach (var intersection in GetSegmentCircleIntersections(segments[i].A, segments[i].B, circle))
                    Consider(intersection);
            }
        }

        for (int i = 0; i < circles.Count; i++)
        {
            for (int j = i + 1; j < circles.Count; j++)
            {
                foreach (var intersection in GetCircleIntersections(circles[i], circles[j]))
                    Consider(intersection);
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

    private static IEnumerable<Point> GetSegmentCircleIntersections(Point a, Point b, SnapCircle circle)
    {
        const double tolerance = 1e-10;
        var intersections = new List<Point>(2);

        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double aCoeff = dx * dx + dy * dy;
        if (aCoeff < tolerance)
            return intersections;

        double fx = a.X - circle.Center.X;
        double fy = a.Y - circle.Center.Y;
        double bCoeff = 2 * (fx * dx + fy * dy);
        double cCoeff = fx * fx + fy * fy - circle.Radius * circle.Radius;
        double discriminant = bCoeff * bCoeff - 4 * aCoeff * cCoeff;

        if (discriminant < -tolerance)
            return intersections;

        if (discriminant < 0)
            discriminant = 0;

        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double denominator = 2 * aCoeff;
        var tValues = discriminant <= tolerance
            ? new[] { -bCoeff / denominator }
            : new[] { (-bCoeff - sqrtDiscriminant) / denominator, (-bCoeff + sqrtDiscriminant) / denominator };

        foreach (var tValue in tValues)
        {
            if (tValue < -tolerance || tValue > 1 + tolerance)
                continue;

            double clampedT = Math.Max(0, Math.Min(1, tValue));
            var point = new Point(a.X + clampedT * dx, a.Y + clampedT * dy);
            if (!IsPointOnVisibleCircle(point, circle) || ContainsPoint(intersections, point))
                continue;

            intersections.Add(point);
        }

        return intersections;
    }

    private static IEnumerable<Point> GetCircleIntersections(SnapCircle first, SnapCircle second)
    {
        const double tolerance = 1e-10;
        var intersections = new List<Point>(2);

        double dx = second.Center.X - first.Center.X;
        double dy = second.Center.Y - first.Center.Y;
        double centerDistance = Math.Sqrt(dx * dx + dy * dy);

        if (centerDistance < tolerance ||
            centerDistance > first.Radius + second.Radius + tolerance ||
            centerDistance < Math.Abs(first.Radius - second.Radius) - tolerance)
        {
            return intersections;
        }

        double alongCenterLine = (first.Radius * first.Radius - second.Radius * second.Radius + centerDistance * centerDistance) / (2 * centerDistance);
        double perpendicularSq = first.Radius * first.Radius - alongCenterLine * alongCenterLine;
        if (perpendicularSq < -tolerance)
            return intersections;

        if (perpendicularSq < 0)
            perpendicularSq = 0;

        double perpendicular = Math.Sqrt(perpendicularSq);
        double midpointX = first.Center.X + alongCenterLine * dx / centerDistance;
        double midpointY = first.Center.Y + alongCenterLine * dy / centerDistance;
        double offsetX = -dy * perpendicular / centerDistance;
        double offsetY = dx * perpendicular / centerDistance;

        var candidates = perpendicular <= tolerance
            ? new[] { new Point(midpointX, midpointY) }
            : new[]
            {
                new Point(midpointX + offsetX, midpointY + offsetY),
                new Point(midpointX - offsetX, midpointY - offsetY)
            };

        foreach (var candidate in candidates)
        {
            if (!IsPointOnVisibleCircle(candidate, first) ||
                !IsPointOnVisibleCircle(candidate, second) ||
                ContainsPoint(intersections, candidate))
            {
                continue;
            }

            intersections.Add(candidate);
        }

        return intersections;
    }

    private static Point ClosestPointOnCircle(Point cursor, SnapCircle circle)
    {
        var offset = new Vector(cursor.X - circle.Center.X, cursor.Y - circle.Center.Y);
        if (offset.LengthSquared < 1e-10)
        {
            var fallbackAngle = circle.StartAngleDeg ?? 0.0;
            return PointOnCircle(circle.Center, circle.Radius, fallbackAngle);
        }

        var cursorAngle = Math.Atan2(offset.Y, offset.X) * 180.0 / Math.PI;
        if (ContainsAngle(circle, cursorAngle))
            return PointOnCircle(circle.Center, circle.Radius, cursorAngle);

        if (!circle.StartAngleDeg.HasValue || !circle.SweepAngleDeg.HasValue)
            return PointOnCircle(circle.Center, circle.Radius, cursorAngle);

        var startPoint = PointOnCircle(circle.Center, circle.Radius, circle.StartAngleDeg.Value);
        var endPoint = PointOnCircle(circle.Center, circle.Radius, circle.StartAngleDeg.Value + circle.SweepAngleDeg.Value);
        return Distance(cursor, startPoint) <= Distance(cursor, endPoint) ? startPoint : endPoint;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDeg)
    {
        var angleRad = angleDeg * Math.PI / 180.0;
        return new Point(
            center.X + Math.Cos(angleRad) * radius,
            center.Y + Math.Sin(angleRad) * radius);
    }

    private static bool TryGetArcMidpoint(SnapCircle circle, out Point midpoint)
    {
        midpoint = default;

        if (!circle.StartAngleDeg.HasValue || !circle.SweepAngleDeg.HasValue)
            return false;

        const double toleranceDeg = 0.001;
        var sweep = circle.SweepAngleDeg.Value;
        if (Math.Abs(sweep) < toleranceDeg || Math.Abs(Math.Abs(sweep) - 360.0) < toleranceDeg)
            return false;

        midpoint = PointOnCircle(circle.Center, circle.Radius, circle.StartAngleDeg.Value + sweep / 2.0);
        return true;
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NormalizeAngle(double angleDeg)
    {
        var normalized = angleDeg % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static bool IsPointOnVisibleCircle(Point point, SnapCircle circle)
    {
        var angleDeg = Math.Atan2(point.Y - circle.Center.Y, point.X - circle.Center.X) * 180.0 / Math.PI;
        return ContainsAngle(circle, angleDeg);
    }

    private static bool ContainsPoint(IEnumerable<Point> points, Point candidate, double tolerance = 0.01)
    {
        return points.Any(point => Distance(point, candidate) <= tolerance);
    }

    private static bool ContainsAngle(SnapCircle circle, double angleDeg)
    {
        if (!circle.StartAngleDeg.HasValue || !circle.SweepAngleDeg.HasValue)
            return true;

        const double toleranceDeg = 0.001;

        var start = NormalizeAngle(circle.StartAngleDeg.Value);
        var sweep = circle.SweepAngleDeg.Value;
        if (Math.Abs(sweep) < toleranceDeg)
            return false;

        var end = NormalizeAngle(start + sweep);
        var angle = NormalizeAngle(angleDeg);

        if (sweep > 0)
        {
            return start <= end
                ? angle >= start - toleranceDeg && angle <= end + toleranceDeg
                : angle >= start - toleranceDeg || angle <= end + toleranceDeg;
        }

        return end <= start
            ? angle <= start + toleranceDeg && angle >= end - toleranceDeg
            : angle <= start + toleranceDeg || angle >= end - toleranceDeg;
    }

    private static bool IsBetterCandidate(double candidateDistance, SnapType candidateType, double bestDistance, int bestPriority)
    {
        if (candidateDistance > bestDistance)
            return false;

        if (candidateDistance < bestDistance)
            return true;

        return GetPriority(candidateType) > bestPriority;
    }

    private static int GetPriority(SnapType type)
    {
        return type switch
        {
            SnapType.Endpoint => 100,
            SnapType.Intersection => 90,
            SnapType.Midpoint => 80,
            SnapType.Center => 70,
            SnapType.Quadrant => 60,
            SnapType.Perpendicular => 50,
            SnapType.Tangent => 40,
            SnapType.Nearest => 30,
            SnapType.Grid => 20,
            _ => 0
        };
    }
}
