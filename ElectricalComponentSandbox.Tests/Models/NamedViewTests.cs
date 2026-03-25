using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class NamedViewTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var view = new NamedView();

        Assert.False(string.IsNullOrEmpty(view.Id));
        Assert.Equal("View", view.Name);
        Assert.Equal(0.0, view.PanX);
        Assert.Equal(0.0, view.PanY);
        Assert.Equal(1.0, view.Zoom);
        Assert.Null(view.VisibleLayerIds);
        Assert.Null(view.CameraPosition);
        Assert.Null(view.CameraLookDirection);
        Assert.Null(view.CameraUpDirection);
        Assert.Null(view.CameraFieldOfView);
        Assert.False(view.Has3DCamera);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var view = new NamedView
        {
            Name = "Plan View 1",
            PanX = 150.5,
            PanY = -42.3,
            Zoom = 2.5,
            VisibleLayerIds = new List<string> { "layer-1", "layer-2" }
        };

        Assert.Equal("Plan View 1", view.Name);
        Assert.Equal(150.5, view.PanX);
        Assert.Equal(-42.3, view.PanY);
        Assert.Equal(2.5, view.Zoom);
        Assert.Equal(2, view.VisibleLayerIds!.Count);
        Assert.Contains("layer-1", view.VisibleLayerIds);
    }

    [Fact]
    public void Id_IsUniquePerInstance()
    {
        var a = new NamedView();
        var b = new NamedView();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Has3DCamera_TrueWhenCameraPositionSet()
    {
        var view = new NamedView
        {
            CameraPosition = new Point3D(10, 5, 20),
            CameraLookDirection = new Vector3D(-1, -0.5, -1),
            CameraUpDirection = new Vector3D(0, 1, 0),
            CameraFieldOfView = 60.0
        };

        Assert.True(view.Has3DCamera);
        Assert.Equal(new Point3D(10, 5, 20), view.CameraPosition);
        Assert.Equal(60.0, view.CameraFieldOfView);
    }

    [Fact]
    public void Has3DCamera_FalseWhenNoCameraPosition()
    {
        var view = new NamedView
        {
            CameraLookDirection = new Vector3D(0, 0, -1),
            CameraUpDirection = new Vector3D(0, 1, 0)
        };

        Assert.False(view.Has3DCamera);
    }
}
