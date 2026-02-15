using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class SupportComponentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var support = new SupportComponent();

        Assert.Equal(ComponentType.Support, support.Type);
        Assert.Equal("Support Bracket", support.Name);
        Assert.Equal(100.0, support.LoadCapacity);
        Assert.Equal("Bracket", support.SupportType);
        Assert.Equal(2.0, support.Parameters.Width);
        Assert.Equal(2.0, support.Parameters.Height);
        Assert.Equal(1.0, support.Parameters.Depth);
    }

    [Fact]
    public void InheritsFromElectricalComponent()
    {
        var support = new SupportComponent();

        Assert.IsAssignableFrom<ElectricalComponent>(support);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var support = new SupportComponent
        {
            LoadCapacity = 250.0,
            SupportType = "Strut"
        };

        Assert.Equal(250.0, support.LoadCapacity);
        Assert.Equal("Strut", support.SupportType);
    }
}
