using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for ConduitType fitting selection by angle.
/// </summary>
public class ConduitTypeTests
{
    [Theory]
    [InlineData(90, FittingType.Elbow90)]
    [InlineData(85, FittingType.Elbow90)]
    [InlineData(95, FittingType.Elbow90)]
    [InlineData(45, FittingType.Elbow45)]
    [InlineData(40, FittingType.Elbow45)]
    [InlineData(0, FittingType.Coupling)]
    [InlineData(3, FittingType.Coupling)]
    [InlineData(175, FittingType.Coupling)]
    public void SelectFitting_ReturnsCorrectType(double angleDeg, FittingType expected)
    {
        var ct = new ConduitType();
        var result = ct.SelectFitting(angleDeg);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value);
    }

    [Fact]
    public void SelectFitting_UnmatchedAngle_ReturnsNull()
    {
        var ct = new ConduitType();
        // 60° doesn't match any default rule
        var result = ct.SelectFitting(60);
        Assert.Null(result);
    }

    [Fact]
    public void DefaultRoutingPreferences_HasEntries()
    {
        var ct = new ConduitType();
        Assert.True(ct.RoutingPreferences.Count > 0);
    }

    [Fact]
    public void DefaultType_IsEMT()
    {
        var ct = new ConduitType();
        Assert.Equal(ConduitMaterialType.EMT, ct.Standard);
    }
}
