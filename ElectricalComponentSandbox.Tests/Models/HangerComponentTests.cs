using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class HangerComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var hanger = new HangerComponent();

        Assert.Equal(ComponentType.Hanger, hanger.Type);
        Assert.Equal("Hanger", hanger.Name);
        Assert.Equal(0.375, hanger.RodDiameter);
        Assert.Equal(12.0, hanger.RodLength);
        Assert.Equal("Threaded Rod", hanger.HangerType);
        Assert.Equal(150.0, hanger.LoadCapacity);
    }

    [Fact]
    public void Constructor_InheritsFromElectricalComponent()
    {
        var hanger = new HangerComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(hanger);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var hanger = new HangerComponent();

        hanger.RodDiameter = 0.5;
        hanger.RodLength = 24.0;
        hanger.HangerType = "All Thread";
        hanger.LoadCapacity = 200.0;

        Assert.Equal(0.5, hanger.RodDiameter);
        Assert.Equal(24.0, hanger.RodLength);
        Assert.Equal("All Thread", hanger.HangerType);
        Assert.Equal(200.0, hanger.LoadCapacity);
    }
}
