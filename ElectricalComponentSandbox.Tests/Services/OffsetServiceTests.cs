using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class OffsetServiceTests
{
    [Fact]
    public void ComputeSegmentNormalXZ_HorizontalSegment_ReturnsUpward()
    {
        var normal = OffsetService.ComputeSegmentNormalXZ(
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0));

        Assert.Equal(0.0, normal.X, 6);
        Assert.Equal(0.0, normal.Y, 6);
        Assert.Equal(1.0, normal.Z, 6);
    }

    [Fact]
    public void ComputeSegmentNormalXZ_VerticalSegment()
    {
        var normal = OffsetService.ComputeSegmentNormalXZ(
            new Point3D(0, 0, 0),
            new Point3D(0, 0, 10));

        Assert.Equal(-1.0, normal.X, 6);
        Assert.Equal(0.0, normal.Y, 6);
        Assert.Equal(0.0, normal.Z, 6);
    }

    [Fact]
    public void OffsetPolyline_StraightLine_OffsetsCorrectly()
    {
        var points = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0)
        };

        var offset = OffsetService.OffsetPolyline(points, 2.0, OffsetDirection.Left);

        Assert.Equal(2, offset.Count);
        Assert.Equal(0.0, offset[0].X, 6);
        Assert.Equal(2.0, offset[0].Z, 6);
        Assert.Equal(10.0, offset[1].X, 6);
        Assert.Equal(2.0, offset[1].Z, 6);
    }

    [Fact]
    public void OffsetPolyline_RightDirection_OffsetsOpposite()
    {
        var points = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0)
        };

        var offset = OffsetService.OffsetPolyline(points, 2.0, OffsetDirection.Right);

        Assert.Equal(2, offset.Count);
        Assert.Equal(0.0, offset[0].X, 6);
        Assert.Equal(-2.0, offset[0].Z, 6);
        Assert.Equal(10.0, offset[1].X, 6);
        Assert.Equal(-2.0, offset[1].Z, 6);
    }

    [Fact]
    public void CreateParallelConduit_HasNewId()
    {
        var source = new ConduitComponent
        {
            Id = "source-id",
            Name = "Conduit A",
            Length = 12.0
        };

        var parallel = OffsetService.CreateParallelConduit(source, 2.0, OffsetDirection.Left);

        Assert.NotEqual(source.Id, parallel.Id);
        Assert.EndsWith("(Offset)", parallel.Name);
    }

    [Fact]
    public void CreateParallelConduit_SameDiameter()
    {
        var source = new ConduitComponent
        {
            Diameter = 1.25,
            Length = 12.0
        };

        var parallel = OffsetService.CreateParallelConduit(source, 2.0, OffsetDirection.Left);

        Assert.Equal(source.Diameter, parallel.Diameter);
        Assert.Equal(source.ConduitType, parallel.ConduitType);
    }

    [Fact]
    public void OffsetConduit_ReturnsCorrectPointCount()
    {
        var conduit = new ConduitComponent
        {
            Length = 10.0
        };

        var result = OffsetService.OffsetConduit(conduit, 1.0, OffsetDirection.Left);

        Assert.Equal(2, result.OriginalPoints.Count);
        Assert.Equal(2, result.OffsetPoints.Count);
        Assert.Equal(1.0, result.OffsetDistance, 6);
        Assert.Equal(OffsetDirection.Left, result.Direction);
    }

    [Fact]
    public void IntersectOffsetLines_ParallelLines_ReturnsNull()
    {
        var point = OffsetService.IntersectOffsetLines(
            new Point3D(0, 0, 0),
            new Vector3D(1, 0, 0),
            new Point3D(0, 0, 1),
            new Vector3D(1, 0, 0));

        Assert.Null(point);
    }
}
