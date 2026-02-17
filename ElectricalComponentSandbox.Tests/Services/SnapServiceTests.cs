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
