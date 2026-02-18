using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Geometry;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for RDP simplification, orthogonalization, and path conversion.
/// </summary>
public class PathSimplifierTests
{
    [Fact]
    public void RDP_StraightLine_ReturnsTwoPoints()
    {
        var points = new List<Point>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0)
        };

        var result = PathSimplifier.RamerDouglasPeucker(points, 0.5);
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].X);
        Assert.Equal(4, result[1].X);
    }

    [Fact]
    public void RDP_LShape_KeepsCorner()
    {
        var points = new List<Point>
        {
            new(0, 0), new(5, 0), new(5, 5)
        };

        var result = PathSimplifier.RamerDouglasPeucker(points, 0.1);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void RDP_TwoPoints_ReturnsSame()
    {
        var points = new List<Point> { new(0, 0), new(1, 1) };
        var result = PathSimplifier.RamerDouglasPeucker(points, 1.0);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RDP_SinglePoint_ReturnsSame()
    {
        var points = new List<Point> { new(0, 0) };
        var result = PathSimplifier.RamerDouglasPeucker(points, 1.0);
        Assert.Single(result);
    }

    [Fact]
    public void RDP_NoisyLine_SimplifiesCorrectly()
    {
        // Nearly straight line with small perturbations
        var points = new List<Point>();
        for (int i = 0; i <= 20; i++)
        {
            double noise = (i % 3 == 0) ? 0.1 : -0.05;
            points.Add(new Point(i, noise));
        }

        var result = PathSimplifier.RamerDouglasPeucker(points, 0.5);
        Assert.True(result.Count <= 3); // Should simplify to ~2 points
    }

    [Fact]
    public void PerpendicularDistance_PointOnLine_ReturnsZero()
    {
        double dist = PathSimplifier.PerpendicularDistance(
            new Point(1, 0), new Point(0, 0), new Point(2, 0));
        Assert.Equal(0, dist, 6);
    }

    [Fact]
    public void PerpendicularDistance_PointAboveLine_ReturnsDistance()
    {
        double dist = PathSimplifier.PerpendicularDistance(
            new Point(1, 1), new Point(0, 0), new Point(2, 0));
        Assert.Equal(1.0, dist, 6);
    }

    [Fact]
    public void Orthogonalize_SnapsToAxes()
    {
        var points = new List<Point>
        {
            new(0, 0), new(4.9, 0.5), new(5.1, 4.8)
        };

        var result = PathSimplifier.Orthogonalize(points, orthoOnly: true);

        // First segment should snap to horizontal
        Assert.Equal(0, result[0].Y, 1);
        Assert.Equal(result[0].Y, result[1].Y, 1);
    }

    [Fact]
    public void To3DPath_SetsElevation()
    {
        var points = new List<Point> { new(1, 2), new(3, 4) };
        var path3D = PathSimplifier.To3DPath(points, 10.0);

        Assert.Equal(10.0, path3D[0].Z);
        Assert.Equal(10.0, path3D[1].Z);
        Assert.Equal(1.0, path3D[0].X);
    }

    [Fact]
    public void CreateSegmentsFromPath_CreatesCorrectSegmentCount()
    {
        var path = new List<Core.Model.XYZ>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 10, 0)
        };

        var segments = PathSimplifier.CreateSegmentsFromPath(path, "type1");
        Assert.Equal(2, segments.Count);
        Assert.Equal(10.0, segments[0].Length, 3);
    }
}
