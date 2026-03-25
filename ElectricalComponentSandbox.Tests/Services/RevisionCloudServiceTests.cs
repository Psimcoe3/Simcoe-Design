using System.Windows;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class RevisionCloudServiceTests
{
    [Fact]
    public void GenerateFromRect_ReturnsNonEmptyPath()
    {
        var service = new RevisionCloudService();
        var bounds = new Rect(0, 0, 100, 80);

        var path = service.GenerateFromRect(bounds);

        Assert.NotEmpty(path);
    }

    [Fact]
    public void GenerateFromRect_PathFormsClosedLoop()
    {
        var service = new RevisionCloudService();
        var bounds = new Rect(10, 10, 200, 150);

        var path = service.GenerateFromRect(bounds);

        Assert.True(path.Count >= 2, "Path should have at least 2 points");

        var first = path[0];
        var last = path[^1];
        double distance = Math.Sqrt(
            (first.X - last.X) * (first.X - last.X) +
            (first.Y - last.Y) * (first.Y - last.Y));

        Assert.True(distance < 1.0,
            $"First and last point should be close (distance={distance:F4}), " +
            $"first=({first.X:F2},{first.Y:F2}), last=({last.X:F2},{last.Y:F2})");
    }

    [Fact]
    public void GenerateFromRect_PointCountDependsOnArcChordLength()
    {
        var bounds = new Rect(0, 0, 100, 100);

        var serviceLargeChord = new RevisionCloudService { ArcChordLength = 50.0 };
        var serviceSmallChord = new RevisionCloudService { ArcChordLength = 10.0 };

        var pathLarge = serviceLargeChord.GenerateFromRect(bounds);
        var pathSmall = serviceSmallChord.GenerateFromRect(bounds);

        Assert.True(pathSmall.Count > pathLarge.Count,
            $"Smaller chord length should produce more points. " +
            $"Small chord: {pathSmall.Count}, Large chord: {pathLarge.Count}");
    }

    [Fact]
    public void GenerateFromPolygon_Triangle_ReturnsPath()
    {
        var service = new RevisionCloudService();
        var triangle = new List<Point>
        {
            new Point(0, 0),
            new Point(100, 0),
            new Point(50, 86.6)
        };

        var path = service.GenerateFromPolygon(triangle);

        Assert.NotEmpty(path);
        Assert.True(path.Count > 3,
            "Triangle cloud should have more points than just the vertices");
    }

    [Fact]
    public void GenerateFromPolygon_SinglePoint_ReturnsEmpty()
    {
        var service = new RevisionCloudService();
        var singlePoint = new List<Point> { new Point(50, 50) };

        var path = service.GenerateFromPolygon(singlePoint);

        Assert.Empty(path);
    }

    [Fact]
    public void ArcChordLength_AffectsPointDensity()
    {
        var triangle = new List<Point>
        {
            new Point(0, 0),
            new Point(200, 0),
            new Point(100, 173.2)
        };

        var serviceDense = new RevisionCloudService { ArcChordLength = 5.0 };
        var serviceSparse = new RevisionCloudService { ArcChordLength = 50.0 };

        var pathDense = serviceDense.GenerateFromPolygon(triangle);
        var pathSparse = serviceSparse.GenerateFromPolygon(triangle);

        Assert.True(pathDense.Count > pathSparse.Count,
            $"Dense chord (5.0) should produce more points than sparse (50.0). " +
            $"Dense: {pathDense.Count}, Sparse: {pathSparse.Count}");
    }

    [Fact]
    public void BulgeFactor_AffectsOffset()
    {
        var square = new List<Point>
        {
            new Point(0, 0),
            new Point(100, 0),
            new Point(100, 100),
            new Point(0, 100)
        };

        // With bulge = 0, all points should lie very close to the polygon edges
        var serviceFlat = new RevisionCloudService
        {
            ArcChordLength = 10.0,
            BulgeFactor = 0.0
        };

        var serviceBulgy = new RevisionCloudService
        {
            ArcChordLength = 10.0,
            BulgeFactor = 0.5
        };

        var pathFlat = serviceFlat.GenerateFromPolygon(square);
        var pathBulgy = serviceBulgy.GenerateFromPolygon(square);

        // Measure maximum distance from each point to the nearest polygon edge.
        // For bulge=0, points should be essentially on the edges.
        double maxDistFlat = MaxDistanceFromEdges(pathFlat, square);
        double maxDistBulgy = MaxDistanceFromEdges(pathBulgy, square);

        Assert.True(maxDistFlat < 1.0,
            $"With BulgeFactor=0, max distance from edges should be near zero, got {maxDistFlat:F4}");
        Assert.True(maxDistBulgy > maxDistFlat,
            $"With BulgeFactor=0.5, points should deviate more from edges. " +
            $"Bulgy max dist: {maxDistBulgy:F4}, Flat max dist: {maxDistFlat:F4}");
    }

    /// <summary>
    /// Computes the maximum distance from any point in the path to the nearest
    /// edge of the polygon.
    /// </summary>
    private static double MaxDistanceFromEdges(List<Point> path, List<Point> polygon)
    {
        double maxDist = 0;

        foreach (var pt in path)
        {
            double minEdgeDist = double.MaxValue;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                double dist = PointToSegmentDistance(pt, a, b);
                if (dist < minEdgeDist)
                    minEdgeDist = dist;
            }

            if (minEdgeDist > maxDist)
                maxDist = minEdgeDist;
        }

        return maxDist;
    }

    /// <summary>
    /// Computes the shortest distance from a point to a line segment.
    /// </summary>
    private static double PointToSegmentDistance(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-18)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
        t = Math.Clamp(t, 0.0, 1.0);

        double projX = a.X + t * dx;
        double projY = a.Y + t * dy;

        return Math.Sqrt(
            (p.X - projX) * (p.X - projX) +
            (p.Y - projY) * (p.Y - projY));
    }
}
