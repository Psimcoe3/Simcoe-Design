using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for XYZ struct operations.
/// </summary>
public class XYZTests
{
    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        var v = new XYZ(3, 4, 0);
        Assert.Equal(5.0, v.Length, 6);
    }

    [Fact]
    public void Normalize_ReturnsUnitVector()
    {
        var v = new XYZ(3, 4, 0).Normalize();
        Assert.Equal(1.0, v.Length, 6);
    }

    [Fact]
    public void Normalize_ZeroVector_ReturnsZero()
    {
        var v = XYZ.Zero.Normalize();
        Assert.Equal(0, v.Length);
    }

    [Fact]
    public void DistanceTo_ReturnsCorrectValue()
    {
        var a = new XYZ(0, 0, 0);
        var b = new XYZ(1, 0, 0);
        Assert.Equal(1.0, a.DistanceTo(b), 6);
    }

    [Fact]
    public void DotProduct_OrthogonalVectors_ReturnsZero()
    {
        var a = XYZ.BasisX;
        var b = XYZ.BasisY;
        Assert.Equal(0.0, a.DotProduct(b), 6);
    }

    [Fact]
    public void CrossProduct_XcrossY_ReturnsZ()
    {
        var result = XYZ.BasisX.CrossProduct(XYZ.BasisY);
        Assert.True(result.IsAlmostEqualTo(XYZ.BasisZ));
    }

    [Fact]
    public void AngleBetween_PerpendicularVectors_Returns90Degrees()
    {
        double angle = XYZ.AngleBetween(XYZ.BasisX, XYZ.BasisY);
        Assert.Equal(Math.PI / 2, angle, 6);
    }

    [Fact]
    public void AngleBetween_ParallelVectors_ReturnsZero()
    {
        double angle = XYZ.AngleBetween(XYZ.BasisX, XYZ.BasisX);
        Assert.Equal(0, angle, 6);
    }

    [Fact]
    public void AngleBetween_OppositeVectors_Returns180Degrees()
    {
        double angle = XYZ.AngleBetween(XYZ.BasisX, -XYZ.BasisX);
        Assert.Equal(Math.PI, angle, 6);
    }

    [Fact]
    public void Addition_Works()
    {
        var result = new XYZ(1, 2, 3) + new XYZ(4, 5, 6);
        Assert.True(result.IsAlmostEqualTo(new XYZ(5, 7, 9)));
    }

    [Fact]
    public void ScalarMultiplication_Works()
    {
        var result = new XYZ(1, 2, 3) * 2;
        Assert.True(result.IsAlmostEqualTo(new XYZ(2, 4, 6)));
    }

    [Fact]
    public void IsAlmostEqualTo_WithinTolerance_ReturnsTrue()
    {
        var a = new XYZ(1.0, 2.0, 3.0);
        var b = new XYZ(1.0 + 1e-10, 2.0, 3.0);
        Assert.True(a.IsAlmostEqualTo(b));
    }
}
