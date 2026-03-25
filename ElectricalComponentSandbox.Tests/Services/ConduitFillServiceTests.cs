using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ConduitFillServiceTests
{
    private readonly ConduitFillService _sut = new();

    [Fact]
    public void GetMaxFillPercent_OneWire_53Percent()
    {
        Assert.Equal(53, _sut.GetMaxFillPercent(1));
    }

    [Fact]
    public void GetMaxFillPercent_TwoWires_31Percent()
    {
        Assert.Equal(31, _sut.GetMaxFillPercent(2));
    }

    [Fact]
    public void GetMaxFillPercent_ThreeOrMore_40Percent()
    {
        Assert.Equal(40, _sut.GetMaxFillPercent(3));
        Assert.Equal(40, _sut.GetMaxFillPercent(10));
    }

    [Fact]
    public void GetConduitArea_EMT_HalfInch()
    {
        double area = _sut.GetConduitArea("1/2", ConduitMaterialType.EMT);
        Assert.True(area > 0.3 && area < 0.31);
    }

    [Fact]
    public void GetWireArea_12AWG_Reasonable()
    {
        double area = _sut.GetWireArea("12");
        Assert.True(area > 0.01 && area < 0.02);
    }

    [Fact]
    public void CalculateFill_ThreeWiresInHalfInch_ReturnsResult()
    {
        var result = _sut.CalculateFill("1/2", ConduitMaterialType.EMT,
            new[] { "12", "12", "12" });
        Assert.Equal(3, result.ConductorCount);
        Assert.True(result.FillPercent > 0);
        Assert.Equal(40, result.MaxAllowedFillPercent);
    }

    [Fact]
    public void CalculateFill_Overloaded_ExceedsCode()
    {
        // 10 #6 wires in 1/2" EMT should exceed fill
        var result = _sut.CalculateFill("1/2", ConduitMaterialType.EMT,
            Enumerable.Repeat("6", 10).ToList());
        Assert.True(result.ExceedsCode);
    }

    [Fact]
    public void RecommendConduitSize_ThreeTwelves_ReturnsValidSize()
    {
        var size = _sut.RecommendConduitSize(ConduitMaterialType.EMT,
            new[] { "12", "12", "12" });
        Assert.NotNull(size);
        Assert.NotEmpty(size);
    }

    [Fact]
    public void CalculateFill_PVC_UsesPvcAreas()
    {
        var emt = _sut.CalculateFill("1", ConduitMaterialType.EMT, new[] { "12" });
        var pvc = _sut.CalculateFill("1", ConduitMaterialType.PVC, new[] { "12" });
        // PVC has smaller area, so fill should be higher
        Assert.True(pvc.FillPercent > emt.FillPercent);
    }
}
