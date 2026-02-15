using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class ConduitComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var conduit = new ConduitComponent();

        Assert.Equal(ComponentType.Conduit, conduit.Type);
        Assert.Equal("Conduit", conduit.Name);
        Assert.Equal(0.5, conduit.Diameter);
        Assert.Equal(10.0, conduit.Length);
        Assert.Equal("EMT", conduit.ConduitType);
        Assert.Empty(conduit.BendPoints);
        Assert.Equal(1.0, conduit.BendRadius);
        Assert.Equal(BendType.Degree90, conduit.BendType);
    }

    [Fact]
    public void GetPathPoints_NoBendPoints_ReturnsStartAndEnd()
    {
        var conduit = new ConduitComponent { Length = 15.0 };

        var points = conduit.GetPathPoints();

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point3D(0, 0, 0), points[0]);
        Assert.Equal(new Point3D(0, 0, 15.0), points[1]);
    }

    [Fact]
    public void GetPathPoints_WithBendPoints_ReturnsStartAndBendPoints()
    {
        var conduit = new ConduitComponent();
        conduit.BendPoints.Add(new Point3D(5, 0, 5));
        conduit.BendPoints.Add(new Point3D(10, 0, 10));

        var points = conduit.GetPathPoints();

        Assert.Equal(3, points.Count);
        Assert.Equal(new Point3D(0, 0, 0), points[0]);
        Assert.Equal(new Point3D(5, 0, 5), points[1]);
        Assert.Equal(new Point3D(10, 0, 10), points[2]);
    }

    [Fact]
    public void GetPathPoints_SingleBendPoint_ReturnsTwoPoints()
    {
        var conduit = new ConduitComponent();
        conduit.BendPoints.Add(new Point3D(3, 4, 5));

        var points = conduit.GetPathPoints();

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point3D(0, 0, 0), points[0]);
        Assert.Equal(new Point3D(3, 4, 5), points[1]);
    }

    [Fact]
    public void BendType_CanBeSetToDegree45()
    {
        var conduit = new ConduitComponent { BendType = BendType.Degree45 };

        Assert.Equal(BendType.Degree45, conduit.BendType);
    }

    [Fact]
    public void BendRadius_CanBeModified()
    {
        var conduit = new ConduitComponent { BendRadius = 2.5 };

        Assert.Equal(2.5, conduit.BendRadius);
    }

    [Fact]
    public void InheritsFromElectricalComponent()
    {
        var conduit = new ConduitComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(conduit);
        Assert.NotNull(conduit.Id);
        Assert.NotEmpty(conduit.Id);
    }
}
