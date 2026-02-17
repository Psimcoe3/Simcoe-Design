using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides snap-to functionality for 2D drawing: endpoints, midpoints, and intersections
/// </summary>
public class SnapService
{
    public double SnapRadius { get; set; } = 10.0;
    public bool SnapToEndpoints { get; set; } = true;
    public bool SnapToMidpoints { get; set; } = true;
    public bool SnapToIntersections { get; set; } = true;
    
    /// <summary>
    /// Result of a snap operation
    /// </summary>
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
        Grid
    }
    
    /// <summary>
    /// Finds the nearest snap point given a cursor position and a set of geometry points
    /// </summary>
    public SnapResult FindSnapPoint(Point cursor, IEnumerable<Point> endpoints, IEnumerable<(Point A, Point B)> segments)
    {
        var result = new SnapResult { SnappedPoint = cursor, Type = SnapType.None, Snapped = false };
        double bestDist = SnapRadius;
        
        // Check endpoints
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
        
        // Check midpoints
        if (SnapToMidpoints)
        {
            foreach (var seg in segments)
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
        
        // Check intersections
        if (SnapToIntersections)
        {
            var segList = segments.ToList();
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
        
        return result;
    }
    
    /// <summary>
    /// Attempts to find the intersection point of two line segments
    /// </summary>
    public static bool TryGetIntersection(Point a1, Point a2, Point b1, Point b2, out Point intersection)
    {
        intersection = default;
        
        double d1x = a2.X - a1.X;
        double d1y = a2.Y - a1.Y;
        double d2x = b2.X - b1.X;
        double d2y = b2.Y - b1.Y;
        
        double cross = d1x * d2y - d1y * d2x;
        
        if (Math.Abs(cross) < 1e-10)
            return false;
        
        double t = ((b1.X - a1.X) * d2y - (b1.Y - a1.Y) * d2x) / cross;
        double u = ((b1.X - a1.X) * d1y - (b1.Y - a1.Y) * d1x) / cross;
        
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = new Point(a1.X + t * d1x, a1.Y + t * d1y);
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
