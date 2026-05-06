using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ConduitSlopeServiceTests
{
    // ── RatioToPercent ───────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 10.0, 10.0)]
    [InlineData(0.25, 10.0, 2.5)]
    [InlineData(0.0,  5.0,  0.0)]
    public void RatioToPercent_ConvertsCorrectly(double rise, double run, double expected)
    {
        Assert.Equal(expected, ConduitSlopeService.RatioToPercent(rise, run), precision: 6);
    }

    [Fact]
    public void RatioToPercent_ZeroRun_ReturnsZero()
    {
        Assert.Equal(0, ConduitSlopeService.RatioToPercent(1.0, 0));
    }

    // ── PercentToRatio ───────────────────────────────────────────────────

    [Fact]
    public void PercentToRatio_100Percent_Returns1()
    {
        Assert.Equal(1.0, ConduitSlopeService.PercentToRatio(100.0), precision: 6);
    }

    [Fact]
    public void PercentToRatio_50Percent_Returns0Point5()
    {
        Assert.Equal(0.5, ConduitSlopeService.PercentToRatio(50.0), precision: 6);
    }

    // ── PercentToDegrees ─────────────────────────────────────────────────

    [Theory]
    [InlineData(100.0, 45.0)]  // tan(45°) = 1 → 100%
    [InlineData(0.0,    0.0)]
    public void PercentToDegrees_KnownAngles(double slopePercent, double expectedDegrees)
    {
        Assert.Equal(expectedDegrees, ConduitSlopeService.PercentToDegrees(slopePercent), precision: 3);
    }

    // ── DegreesToPercent ─────────────────────────────────────────────────

    [Fact]
    public void DegreesToPercent_45Degrees_Returns100()
    {
        Assert.Equal(100.0, ConduitSlopeService.DegreesToPercent(45.0), precision: 2);
    }

    [Fact]
    public void DegreesToPercent_ZeroDegrees_ReturnsZero()
    {
        Assert.Equal(0.0, ConduitSlopeService.DegreesToPercent(0.0), precision: 6);
    }

    // ── ComputeElevationChange ───────────────────────────────────────────

    [Theory]
    [InlineData(10.0, 1.0,  0.1)]     // 10 ft at 1% = 0.1 ft rise
    [InlineData(10.0, 0.0,  0.0)]
    [InlineData(50.0, 2.0,  1.0)]
    public void ComputeElevationChange_ReturnsExpected(
        double length, double slope, double expectedElev)
    {
        var result = ConduitSlopeService.ComputeElevationChange(length, slope);
        Assert.Equal(expectedElev, result, precision: 6);
    }

    // ── ComputeSlopePercent ──────────────────────────────────────────────

    [Fact]
    public void ComputeSlopePercent_KnownValues_ReturnsCorrect()
    {
        // 1 ft rise over 10 ft run = 10%
        Assert.Equal(10.0, ConduitSlopeService.ComputeSlopePercent(1.0, 10.0), precision: 6);
    }

    [Fact]
    public void ComputeSlopePercent_ZeroLength_ReturnsZero()
    {
        Assert.Equal(0, ConduitSlopeService.ComputeSlopePercent(1.0, 0));
    }

    // ── ComputeActualLength ──────────────────────────────────────────────

    [Fact]
    public void ComputeActualLength_FlatRun_EqualsHorizontal()
    {
        var result = ConduitSlopeService.ComputeActualLength(20.0, 0.0);
        Assert.Equal(20.0, result, precision: 6);
    }

    [Fact]
    public void ComputeActualLength_SlopedRun_IsLongerThanHorizontal()
    {
        // 10 ft horizontal at 100% slope → hypotenuse = sqrt(100+100) = 14.14 ft
        var result = ConduitSlopeService.ComputeActualLength(10.0, 100.0);
        Assert.Equal(Math.Sqrt(200.0), result, precision: 6);
    }

    // ── MeetsDrainageMinimum ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.2,  true)]
    [InlineData(0.5,  true)]
    [InlineData(0.1,  false)]
    [InlineData(0.0,  false)]
    [InlineData(-0.3, true)]   // negative (downhill) still ≥ 0.2 in absolute value
    public void MeetsDrainageMinimum_ReturnsExpected(double slope, bool expected)
    {
        Assert.Equal(expected, ConduitSlopeService.MeetsDrainageMinimum(slope));
    }

    // ── Validate ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoRequirements_ReturnsNull()
    {
        var msg = ConduitSlopeService.Validate(0.0, requiresDrainage: false);
        Assert.Null(msg);
    }

    [Fact]
    public void Validate_DrainageRequired_BelowMin_ReturnsMessage()
    {
        var msg = ConduitSlopeService.Validate(0.1, requiresDrainage: true);
        Assert.NotNull(msg);
        Assert.Contains("0.10%", msg);
        Assert.Contains("0.2%", msg);
    }

    [Fact]
    public void Validate_DrainageRequired_AtMin_ReturnsNull()
    {
        var msg = ConduitSlopeService.Validate(0.2, requiresDrainage: true);
        Assert.Null(msg);
    }
}
