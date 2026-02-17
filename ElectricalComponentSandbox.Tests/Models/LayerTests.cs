using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class LayerTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var layer = new Layer();

        Assert.NotNull(layer.Id);
        Assert.Equal("Layer", layer.Name);
        Assert.Equal("#808080", layer.Color);
        Assert.True(layer.IsVisible);
        Assert.False(layer.IsLocked);
    }

    [Fact]
    public void CreateDefault_CreatesDefaultLayer()
    {
        var layer = Layer.CreateDefault();

        Assert.Equal("default", layer.Id);
        Assert.Equal("Default", layer.Name);
        Assert.True(layer.IsVisible);
        Assert.False(layer.IsLocked);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var layer = new Layer();

        layer.Name = "Electrical";
        layer.Color = "#FF0000";
        layer.IsVisible = false;
        layer.IsLocked = true;

        Assert.Equal("Electrical", layer.Name);
        Assert.Equal("#FF0000", layer.Color);
        Assert.False(layer.IsVisible);
        Assert.True(layer.IsLocked);
    }
}
