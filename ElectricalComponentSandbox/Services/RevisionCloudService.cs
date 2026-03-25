using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generates revision cloud geometry from a bounding rectangle or polygon.
/// Each "bump" is an arc segment along the perimeter, producing the characteristic
/// cloud outlines used in AutoCAD, Bluebeam, and Revit to highlight changed areas.
/// </summary>
public class RevisionCloudService
{
    /// <summary>Arc chord length in document units. Smaller = more bumps.</summary>
    public double ArcChordLength { get; set; } = 15.0;

    /// <summary>Arc bulge factor (0.0-1.0). Higher = more rounded bumps.</summary>
    public double BulgeFactor { get; set; } = 0.3;

    /// <summary>
    /// Generates a revision cloud path from a rectangle.
    /// Returns a list of points that form the cloud outline with arc bumps.
    /// The points include the arc approximation segments.
    /// </summary>
    public List<Point> GenerateFromRect(Rect bounds)
    {
        var corners = new List<Point>
        {
            bounds.TopLeft,
            bounds.TopRight,
            bounds.BottomRight,
            bounds.BottomLeft
        };

        return GenerateFromPolygon(corners);
    }

    /// <summary>
    /// Generates a revision cloud path from an arbitrary closed polygon.
    /// Each edge of the polygon is subdivided into arc segments.
    /// </summary>
    public List<Point> GenerateFromPolygon(IReadOnlyList<Point> vertices)
    {
        if (vertices == null || vertices.Count < 2)
            return new List<Point>();

        var result = new List<Point>();

        // Compute the centroid so we can determine the outward direction for bulges.
        var centroid = ComputeCentroid(vertices);

        for (int i = 0; i < vertices.Count; i++)
        {
            var edgeStart = vertices[i];
            var edgeEnd = vertices[(i + 1) % vertices.Count];

            var edgeDx = edgeEnd.X - edgeStart.X;
            var edgeDy = edgeEnd.Y - edgeStart.Y;
            var edgeLength = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);

            if (edgeLength < 1e-9)
                continue;

            // Number of arc segments along this edge
            int arcCount = Math.Max(1, (int)Math.Ceiling(edgeLength / ArcChordLength));

            // Determine the outward perpendicular direction.
            // The perpendicular of (dx, dy) is (-dy, dx) or (dy, -dx).
            // Pick the one that points away from the centroid.
            var edgeMidpoint = new Point(
                (edgeStart.X + edgeEnd.X) / 2.0,
                (edgeStart.Y + edgeEnd.Y) / 2.0);

            // Normalized perpendicular candidates
            double perpX1 = -edgeDy / edgeLength;
            double perpY1 = edgeDx / edgeLength;

            // Test which perpendicular points away from centroid
            double testX = edgeMidpoint.X + perpX1 - centroid.X;
            double testY = edgeMidpoint.Y + perpY1 - centroid.Y;
            double baseDx = edgeMidpoint.X - centroid.X;
            double baseDy = edgeMidpoint.Y - centroid.Y;

            // If adding the perpendicular moves us further from centroid, it's outward
            double distWithPerp = testX * testX + testY * testY;
            double distWithout = baseDx * baseDx + baseDy * baseDy;

            double outwardPerpX, outwardPerpY;
            if (distWithPerp >= distWithout)
            {
                outwardPerpX = perpX1;
                outwardPerpY = perpY1;
            }
            else
            {
                outwardPerpX = -perpX1;
                outwardPerpY = -perpY1;
            }

            // Generate arc segments along this edge
            for (int j = 0; j < arcCount; j++)
            {
                double t0 = (double)j / arcCount;
                double t1 = (double)(j + 1) / arcCount;

                var arcStart = new Point(
                    edgeStart.X + edgeDx * t0,
                    edgeStart.Y + edgeDy * t0);
                var arcEnd = new Point(
                    edgeStart.X + edgeDx * t1,
                    edgeStart.Y + edgeDy * t1);

                double chordLen = Math.Sqrt(
                    (arcEnd.X - arcStart.X) * (arcEnd.X - arcStart.X) +
                    (arcEnd.Y - arcStart.Y) * (arcEnd.Y - arcStart.Y));

                double bulgeOffset = BulgeFactor * chordLen;

                var arcPoints = GenerateArcSegment(arcStart, arcEnd, bulgeOffset, outwardPerpX, outwardPerpY);

                // Add the start point of the first arc on this edge (avoid duplicates)
                if (j == 0)
                    result.Add(arcStart);

                result.AddRange(arcPoints);
                result.Add(arcEnd);
            }
        }

        // Close the loop: ensure the last point matches the first
        if (result.Count > 1)
        {
            var first = result[0];
            var last = result[^1];
            double closeDist = Math.Sqrt(
                (first.X - last.X) * (first.X - last.X) +
                (first.Y - last.Y) * (first.Y - last.Y));

            if (closeDist > 1e-9)
                result.Add(first);
        }

        return result;
    }

    /// <summary>
    /// Generates arc approximation points between two points with a bulge.
    /// Returns intermediate points (not including start and end) that approximate
    /// a circular arc bulging outward.
    /// </summary>
    private List<Point> GenerateArcSegment(Point start, Point end, double bulge,
        double outwardPerpX, double outwardPerpY)
    {
        var points = new List<Point>();

        double midX = (start.X + end.X) / 2.0;
        double midY = (start.Y + end.Y) / 2.0;

        // The peak of the arc: midpoint offset by bulge along outward perpendicular
        double peakX = midX + outwardPerpX * bulge;
        double peakY = midY + outwardPerpY * bulge;

        // Generate 3 intermediate points using quadratic Bezier approximation
        // with control point at the peak (elevated for arc shape: actual control
        // point is 2x the offset for a quadratic Bezier to pass through the peak).
        double controlX = midX + outwardPerpX * bulge * 2.0;
        double controlY = midY + outwardPerpY * bulge * 2.0;

        // Evaluate quadratic Bezier at t = 0.25, 0.50, 0.75
        double[] tValues = { 0.25, 0.50, 0.75 };
        foreach (double t in tValues)
        {
            double oneMinusT = 1.0 - t;
            double bx = oneMinusT * oneMinusT * start.X
                      + 2.0 * oneMinusT * t * controlX
                      + t * t * end.X;
            double by = oneMinusT * oneMinusT * start.Y
                      + 2.0 * oneMinusT * t * controlY
                      + t * t * end.Y;

            points.Add(new Point(bx, by));
        }

        return points;
    }

    /// <summary>
    /// Computes the centroid (geometric center) of a polygon.
    /// </summary>
    private static Point ComputeCentroid(IReadOnlyList<Point> vertices)
    {
        double cx = 0, cy = 0;
        foreach (var v in vertices)
        {
            cx += v.X;
            cy += v.Y;
        }
        return new Point(cx / vertices.Count, cy / vertices.Count);
    }
}
