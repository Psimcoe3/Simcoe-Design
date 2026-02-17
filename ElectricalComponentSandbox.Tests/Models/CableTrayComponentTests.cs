using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class CableTrayComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var tray = new CableTrayComponent();

        Assert.Equal(ComponentType.CableTray, tray.Type);
        Assert.Equal("Cable Tray", tray.Name);
        Assert.Equal(12.0, tray.TrayWidth);
        Assert.Equal(4.0, tray.TrayDepth);
        Assert.Equal(10.0, tray.Length);
        Assert.Equal("Ladder", tray.TrayType);
        Assert.Equal("#C0C0C0", tray.Parameters.Color);
    }

    [Fact]
    public void Constructor_InheritsFromElectricalComponent()
    {
        var tray = new CableTrayComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(tray);
    }

    [Fact]
    public void GetPathPoints_NoPathPoints_ReturnsTwoPoints()
    {
        var tray = new CableTrayComponent { Length = 15.0 };

        var points = tray.GetPathPoints();

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point3D(0, 0, 0), points[0]);
        Assert.Equal(new Point3D(15, 0, 0), points[1]);
    }

    [Fact]
    public void GetPathPoints_WithPathPoints_ReturnsAll()
    {
        var tray = new CableTrayComponent();
        tray.PathPoints.Add(new Point3D(5, 0, 5));

        var points = tray.GetPathPoints();

        Assert.Equal(2, points.Count); // origin + one path point
        Assert.Equal(new Point3D(0, 0, 0), points[0]);
        Assert.Equal(new Point3D(5, 0, 5), points[1]);
    }
}
