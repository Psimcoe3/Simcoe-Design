using System.Windows;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class SnapServiceTests
{
    [Fact]
    public void FindSnapPoint_NoPoints_ReturnsUnsnapped()
    {
        var service = new SnapService();
        var cursor = new Point(50, 50);

        var result = service.FindSnapPoint(cursor, 
            Array.Empty<Point>(), 
            Array.Empty<(Point, Point)>());

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void FindSnapPoint_NearEndpoint_SnapsToEndpoint()
    {
        var service = new SnapService { SnapRadius = 15 };
        var cursor = new Point(52, 52);
        var endpoints = new[] { new Point(50, 50) };

        var result = service.FindSnapPoint(cursor, endpoints, 
            Array.Empty<(Point, Point)>());

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Endpoint, result.Type);
        Assert.Equal(new Point(50, 50), result.SnappedPoint);
    }

    [Fact]
    public void FindSnapPoint_NearMidpoint_SnapsToMidpoint()
    {
        var service = new SnapService { SnapRadius = 15 };
        var cursor = new Point(51, 51);
        var segments = new[] { (new Point(0, 0), new Point(100, 100)) };

        var result = service.FindSnapPoint(cursor, 
            Array.Empty<Point>(), segments);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Midpoint, result.Type);
        Assert.Equal(new Point(50, 50), result.SnappedPoint);
    }

    [Fact]
    public void FindSnapPoint_NearIntersection_SnapsToIntersection()
    {
        var service = new SnapService { SnapRadius = 15 };
        var cursor = new Point(51, 51);
        var segments = new[]
        {
            (new Point(0, 0), new Point(100, 100)),
            (new Point(0, 100), new Point(100, 0))
        };

        var result = service.FindSnapPoint(cursor, 
            Array.Empty<Point>(), segments);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Intersection, result.Type);
        Assert.Equal(50.0, result.SnappedPoint.X, 1);
        Assert.Equal(50.0, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_NearLineArcIntersection_SnapsToIntersection()
    {
        var service = new SnapService { SnapRadius = 10 };
        var cursor = new Point(48, 46);
        var segments = new[] { (new Point(48, 30), new Point(48, 60)) };
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            segments,
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Intersection, result.Type);
        Assert.Equal(48.0, result.SnappedPoint.X, 1);
        Assert.Equal(46.0, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_NearArcArcIntersection_SnapsToIntersection()
    {
        var service = new SnapService { SnapRadius = 10 };
        var cursor = new Point(45, 49);
        var circles = new[]
        {
            new SnapCircle(new Point(40, 40), 10.0, 0.0, 180.0),
            new SnapCircle(new Point(50, 40), 10.0, 0.0, 180.0)
        };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Intersection, result.Type);
        Assert.Equal(45.0, result.SnappedPoint.X, 1);
        Assert.Equal(48.7, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_NearOffSweepLineArcIntersection_DoesNotSnap()
    {
        var service = new SnapService
        {
            SnapRadius = 5,
            SnapToEndpoints = false,
            SnapToMidpoints = false,
            SnapToCenter = false,
            SnapToQuadrant = false
        };
        var cursor = new Point(48, 34);
        var segments = new[] { (new Point(48, 30), new Point(48, 60)) };
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            segments,
            circles: circles);

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void FindSnapPoint_NearCircleCenter_SnapsToCenter()
    {
        var service = new SnapService { SnapRadius = 15 };
        var cursor = new Point(42, 39);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Center, result.Type);
        Assert.Equal(new Point(40, 40), result.SnappedPoint);
    }

    [Fact]
    public void FindSnapPoint_NearCircleQuadrant_SnapsToQuadrant()
    {
        var service = new SnapService { SnapRadius = 15 };
        var cursor = new Point(69, 52);
        var circles = new[] { new SnapCircle(new Point(50, 50), 20.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Quadrant, result.Type);
        Assert.Equal(new Point(70, 50), result.SnappedPoint);
    }
    [Fact]
    public void FindSnapPoint_WithLastPointAndSegment_SnapsToPerpendicular()
    {
        var service = new SnapService
        {
            SnapRadius = 15,
            SnapToEndpoints = false,
            SnapToMidpoints = false,
            SnapToIntersections = false
        };
        var cursor = new Point(41, 29);
        var lastPoint = new Point(10, 30);
        var segments = new[] { (new Point(40, -100), new Point(40, 100)) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            segments,
            lastPoint: lastPoint);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Perpendicular, result.Type);
        Assert.Equal(new Point(40, 30), result.SnappedPoint);
    }

    [Fact]
    public void FindSnapPoint_WithLastPointAndVisibleArc_SnapsToPerpendicular()
    {
        var service = new SnapService
        {
            SnapRadius = 10,
            SnapToEndpoints = false,
            SnapToMidpoints = false,
            SnapToIntersections = false,
            SnapToCenter = false,
            SnapToQuadrant = false
        };
        var cursor = new Point(48, 46);
        var lastPoint = new Point(20, 20);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            lastPoint: lastPoint,
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Perpendicular, result.Type);
        Assert.Equal(47.1, result.SnappedPoint.X, 1);
        Assert.Equal(47.1, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_ArcPerpendicularOutsideSweep_DoesNotSnap()
    {
        var service = new SnapService
        {
            SnapRadius = 5,
            SnapToEndpoints = false,
            SnapToMidpoints = false,
            SnapToIntersections = false,
            SnapToCenter = false,
            SnapToQuadrant = false
        };
        var cursor = new Point(48, 46);
        var lastPoint = new Point(20, 20);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 90.0, 45.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            lastPoint: lastPoint,
            circles: circles);

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void FindSnapPoint_WithLastPointAndCircle_SnapsToTangent()
    {
        var service = new SnapService
        {
            SnapRadius = 15,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToTangent = true
        };
        var cursor = new Point(43, 67);
        var lastPoint = new Point(0, 50);
        var circles = new[] { new SnapCircle(new Point(50, 50), 20.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            lastPoint: lastPoint,
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Tangent, result.Type);
        Assert.Equal(42.0, result.SnappedPoint.X, 1);
        Assert.Equal(68.3, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_WithLastPointAndVisibleArc_SnapsToTangent()
    {
        var service = new SnapService
        {
            SnapRadius = 10,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToTangent = true
        };
        var cursor = new Point(49, 45);
        var lastPoint = new Point(40, 60);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            lastPoint: lastPoint,
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Tangent, result.Type);
        Assert.Equal(48.7, result.SnappedPoint.X, 1);
        Assert.Equal(45.0, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_ArcQuadrantOutsideSweep_DoesNotSnap()
    {
        var service = new SnapService
        {
            SnapRadius = 15,
            SnapToCenter = false
        };
        var cursor = new Point(29, 39);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void FindSnapPoint_ArcTangentOutsideSweep_DoesNotSnap()
    {
        var service = new SnapService
        {
            SnapRadius = 15,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToTangent = true
        };
        var cursor = new Point(43, 67);
        var lastPoint = new Point(0, 50);
        var circles = new[] { new SnapCircle(new Point(50, 50), 20.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            lastPoint: lastPoint,
            circles: circles);

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void FindSnapPoint_NearVisibleArcCurve_SnapsToNearest()
    {
        var service = new SnapService
        {
            SnapRadius = 10,
            SnapToEndpoints = false,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToNearest = true
        };
        var cursor = new Point(48, 48);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.True(result.Snapped);
        Assert.Equal(SnapService.SnapType.Nearest, result.Type);
        Assert.Equal(47.1, result.SnappedPoint.X, 1);
        Assert.Equal(47.1, result.SnappedPoint.Y, 1);
    }

    [Fact]
    public void FindSnapPoint_NearInvisibleArcContinuation_DoesNotSnapToNearest()
    {
        var service = new SnapService
        {
            SnapRadius = 5,
            SnapToEndpoints = false,
            SnapToCenter = false,
            SnapToQuadrant = false,
            SnapToNearest = true
        };
        var cursor = new Point(33, 47);
        var circles = new[] { new SnapCircle(new Point(40, 40), 10.0, 0.0, 90.0) };

        var result = service.FindSnapPoint(
            cursor,
            Array.Empty<Point>(),
            Array.Empty<(Point, Point)>(),
            circles: circles);

        Assert.False(result.Snapped);
        Assert.Equal(SnapService.SnapType.None, result.Type);
    }

    [Fact]
    public void TryGetIntersection_Parallel_ReturnsFalse()
    {
        var result = SnapService.TryGetIntersection(
            new Point(0, 0), new Point(10, 0),
            new Point(0, 5), new Point(10, 5),
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryGetIntersection_Crossing_ReturnsTrue()
    {
        var result = SnapService.TryGetIntersection(
            new Point(0, 0), new Point(10, 10),
            new Point(0, 10), new Point(10, 0),
            out var intersection);

        Assert.True(result);
        Assert.Equal(5.0, intersection.X, 1);
        Assert.Equal(5.0, intersection.Y, 1);
    }
}
