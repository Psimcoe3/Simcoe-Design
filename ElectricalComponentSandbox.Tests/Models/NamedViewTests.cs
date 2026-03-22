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
}
