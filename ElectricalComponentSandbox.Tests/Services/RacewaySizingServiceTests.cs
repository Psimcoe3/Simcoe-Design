using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RacewaySizingServiceTests
{
    // ── Wire Area ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("12", 0.0133)]
    [InlineData("4/0", 0.3237)]
    [InlineData("500", 0.7073)]
    public void GetWireArea_KnownSizes(string size, double expected)
    {
        double area = RacewaySizingService.GetWireArea(size);
        Assert.Equal(expected, area);
    }

    [Fact]
    public void GetWireArea_UnknownSize_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            RacewaySizingService.GetWireArea("999"));
    }

    // ── Wireway Fill ─────────────────────────────────────────────────────────

    [Fact]
    public void WirewayFill_SmallLoad_Compliant()
    {
        var conductors = new List<(string, int)>
        {
            ("12", 12),  // 12 × 0.0133 = 0.1596 sqin
        };
        var result = RacewaySizingService.CalculateWirewayFill(conductors, 4, 4);
        Assert.True(result.IsCompliant);
        Assert.True(result.FillPercent < 20);
    }

    [Fact]
    public void WirewayFill_LargeLoad_NonCompliant()
    {
        // Pack lots of large wire into small wireway
        var conductors = new List<(string, int)>
        {
            ("500", 20),  // 20 × 0.7073 = 14.146 sqin → 4×4=16 sqin → 88%
        };
        var result = RacewaySizingService.CalculateWirewayFill(conductors, 4, 4);
        Assert.False(result.IsCompliant);
        Assert.True(result.FillPercent > 20);
    }

    [Fact]
    public void WirewayFill_RecommendsLargerSize()
    {
        var conductors = new List<(string, int)>
        {
            ("4/0", 12),  // 12 × 0.3237 = 3.8844 sqin → need ≥ 19.4 sqin
        };
        var result = RacewaySizingService.CalculateWirewayFill(conductors, 2.5, 2.5);
        Assert.False(result.IsCompliant);
        Assert.True(result.RecommendedSize.Width >= 4);
    }

    [Fact]
    public void WirewayFill_20PercentMax()
    {
        var conductors = new List<(string, int)> { ("12", 1) };
        var result = RacewaySizingService.CalculateWirewayFill(conductors, 4, 4);
        Assert.Equal(20.0, result.MaxFillPercent);
    }

    // ── Pull Box Sizing ──────────────────────────────────────────────────────

    [Fact]
    public void PullBox_StraightPull_8xLargest()
    {
        var result = RacewaySizingService.SizePullBox(
            RacewaySizingService.BoxType.Pull, 3);
        Assert.Equal(24, result.MinLengthInches); // 8 × 3 = 24
        Assert.Contains("314.28(A)(1)", result.NecReference);
    }

    [Fact]
    public void PullBox_AnglePull_6xPlusOthers()
    {
        var result = RacewaySizingService.SizePullBox(
            RacewaySizingService.BoxType.AnglePull, 4,
            new[] { 2.0, 1.5 });
        // 6 × 4 + 2 + 1.5 = 27.5
        Assert.Equal(27.5, result.MinLengthInches);
        Assert.Contains("314.28(A)(2)", result.NecReference);
    }

    [Fact]
    public void PullBox_NoOtherConduits()
    {
        var result = RacewaySizingService.SizePullBox(
            RacewaySizingService.BoxType.AnglePull, 2);
        Assert.Equal(12, result.MinLengthInches); // 6 × 2 = 12
    }

    [Fact]
    public void PullBox_HasMinimumDepth()
    {
        var result = RacewaySizingService.SizePullBox(
            RacewaySizingService.BoxType.Pull, 1);
        Assert.True(result.MinDepthInches >= 4);
    }

    // ── Gutter Space ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("12", 1, 1.5)]
    [InlineData("4/0", 1, 4.0)]
    [InlineData("500", 1, 7.0)]
    public void GutterDepth_OneWire(string wireSize, int wiresPerTerm, double expected)
    {
        double depth = RacewaySizingService.GetMinGutterDepth(wireSize, wiresPerTerm);
        Assert.Equal(expected, depth);
    }

    [Fact]
    public void GutterDepth_TwoWires_Larger()
    {
        double one = RacewaySizingService.GetMinGutterDepth("4/0", 1);
        double two = RacewaySizingService.GetMinGutterDepth("4/0", 2);
        Assert.True(two > one);
        Assert.Equal(6.0, two); // 4.0 × 1.5 = 6.0
    }

    // ── Conductor Count ──────────────────────────────────────────────────────

    [Fact]
    public void ConductorCount_Under20_NoDeratingRequired()
    {
        var (withinLimit, derating) = RacewaySizingService.CheckConductorCount(15);
        Assert.True(withinLimit);
        Assert.False(derating);
    }

    [Fact]
    public void ConductorCount_25_DeratingRequired()
    {
        var (withinLimit, derating) = RacewaySizingService.CheckConductorCount(25);
        Assert.True(withinLimit);
        Assert.True(derating);
    }

    [Fact]
    public void ConductorCount_35_ExceedsLimit()
    {
        var (withinLimit, _) = RacewaySizingService.CheckConductorCount(35);
        Assert.False(withinLimit);
    }
}
