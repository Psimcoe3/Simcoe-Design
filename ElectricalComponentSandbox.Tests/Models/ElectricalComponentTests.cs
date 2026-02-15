using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Tests.Models;

public class ElectricalComponentTests
{
    [Fact]
    public void ComponentParameters_DefaultValues()
    {
        var parameters = new ComponentParameters();

        Assert.Equal(1.0, parameters.Width);
        Assert.Equal(1.0, parameters.Height);
        Assert.Equal(1.0, parameters.Depth);
        Assert.Equal("Steel", parameters.Material);
        Assert.Equal(0.0, parameters.Elevation);
        Assert.Equal("#808080", parameters.Color);
    }

    [Fact]
    public void Component_HasUniqueId()
    {
        var comp1 = new ConduitComponent();
        var comp2 = new BoxComponent();

        Assert.NotEqual(comp1.Id, comp2.Id);
    }

    [Fact]
    public void Component_DefaultPosition_IsOrigin()
    {
        var comp = new ConduitComponent();

        Assert.Equal(new Point3D(0, 0, 0), comp.Position);
    }

    [Fact]
    public void Component_DefaultScale_IsOne()
    {
        var comp = new ConduitComponent();

        Assert.Equal(new Vector3D(1, 1, 1), comp.Scale);
    }

    [Fact]
    public void Component_DefaultRotation_IsZero()
    {
        var comp = new ConduitComponent();

        Assert.Equal(new Vector3D(0, 0, 0), comp.Rotation);
    }

    [Fact]
    public void Component_Constraints_EmptyByDefault()
    {
        var comp = new ConduitComponent();

        Assert.Empty(comp.Constraints);
    }

    [Fact]
    public void Component_Position_CanBeSet()
    {
        var comp = new ConduitComponent
        {
            Position = new Point3D(5, 10, 15)
        };

        Assert.Equal(new Point3D(5, 10, 15), comp.Position);
    }

    [Fact]
    public void ComponentType_HasExpectedValues()
    {
        Assert.Equal(0, (int)ComponentType.Conduit);
        Assert.Equal(1, (int)ComponentType.Box);
        Assert.Equal(2, (int)ComponentType.Panel);
        Assert.Equal(3, (int)ComponentType.Support);
    }
}
