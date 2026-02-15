using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class BoxComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var box = new BoxComponent();

        Assert.Equal(ComponentType.Box, box.Type);
        Assert.Equal("Electrical Box", box.Name);
        Assert.Equal(4, box.KnockoutCount);
        Assert.Equal("Junction Box", box.BoxType);
        Assert.Equal(4.0, box.Parameters.Width);
        Assert.Equal(4.0, box.Parameters.Height);
        Assert.Equal(2.0, box.Parameters.Depth);
    }

    [Fact]
    public void InheritsFromElectricalComponent()
    {
        var box = new BoxComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(box);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var box = new BoxComponent
        {
            KnockoutCount = 8,
            BoxType = "Device Box"
        };

        Assert.Equal(8, box.KnockoutCount);
        Assert.Equal("Device Box", box.BoxType);
    }
}
