using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class PanelComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var panel = new PanelComponent();

        Assert.Equal(ComponentType.Panel, panel.Type);
        Assert.Equal("Electrical Panel", panel.Name);
        Assert.Equal(24, panel.CircuitCount);
        Assert.Equal(200.0, panel.Amperage);
        Assert.Equal("Distribution Panel", panel.PanelType);
        Assert.Equal(20.0, panel.Parameters.Width);
        Assert.Equal(30.0, panel.Parameters.Height);
        Assert.Equal(4.0, panel.Parameters.Depth);
    }

    [Fact]
    public void InheritsFromElectricalComponent()
    {
        var panel = new PanelComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(panel);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var panel = new PanelComponent
        {
            CircuitCount = 42,
            Amperage = 400.0,
            PanelType = "Main Panel"
        };

        Assert.Equal(42, panel.CircuitCount);
        Assert.Equal(400.0, panel.Amperage);
        Assert.Equal("Main Panel", panel.PanelType);
    }
}
