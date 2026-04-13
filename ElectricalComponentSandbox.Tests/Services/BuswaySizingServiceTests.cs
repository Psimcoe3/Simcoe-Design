using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class BuswaySizingServiceTests
{
    // ── Rating Selection ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(350, false, 400)]
    [InlineData(800, false, 800)]
    [InlineData(1100, false, 1200)]
    public void SelectRating_CorrectSize(double load, bool continuous, int expected)
    {
        int rating = BuswaySizingService.SelectRating(load, continuous);
        Assert.Equal(expected, rating);
    }

    [Fact]
    public void SelectRating_ContinuousLoad_125Percent()
    {
        // 800A continuous → 800 × 1.25 = 1000A → selects 1000
        int rating = BuswaySizingService.SelectRating(800, continuousLoad: true);
        Assert.Equal(1000, rating);
    }

    [Fact]
    public void SelectRating_ExceedsMax_ReturnsLargest()
    {
        int rating = BuswaySizingService.SelectRating(10000);
        Assert.Equal(5000, rating);
    }

    // ── Impedance ────────────────────────────────────────────────────────────

    [Fact]
    public void Impedance_AluminumHigherThanCopper()
    {
        double cu = BuswaySizingService.GetImpedancePerFoot(1000, BuswaySizingService.BuswayMaterial.Copper);
        double al = BuswaySizingService.GetImpedancePerFoot(1000, BuswaySizingService.BuswayMaterial.Aluminum);
        Assert.True(al > cu);
    }

    [Fact]
    public void Impedance_LargerRating_LowerImpedance()
    {
        double small = BuswaySizingService.GetImpedancePerFoot(400, BuswaySizingService.BuswayMaterial.Copper);
        double large = BuswaySizingService.GetImpedancePerFoot(2500, BuswaySizingService.BuswayMaterial.Copper);
        Assert.True(large < small);
    }

    // ── Voltage Drop ─────────────────────────────────────────────────────────

    [Fact]
    public void VoltageDrop_ShortRun_Low()
    {
        double vd = BuswaySizingService.CalculateVoltageDrop(800, 50, 1000);
        Assert.True(vd > 0);
        Assert.True(vd < 10); // Short run should have minimal VD
    }

    [Fact]
    public void VoltageDrop_LongerRun_Higher()
    {
        double short50 = BuswaySizingService.CalculateVoltageDrop(800, 50, 1000);
        double long200 = BuswaySizingService.CalculateVoltageDrop(800, 200, 1000);
        Assert.True(long200 > short50);
    }

    // ── Full Specification ───────────────────────────────────────────────────

    [Fact]
    public void Specify_BasicFeeder()
    {
        var spec = BuswaySizingService.Specify(800, 480, 100);
        Assert.Equal(BuswaySizingService.BuswayType.Feeder, spec.Type);
        Assert.Equal(800, spec.RatingAmps);
        Assert.True(spec.VoltageDrop > 0);
        Assert.True(spec.VoltageDropPercent < 5);
    }

    [Fact]
    public void Specify_LongRun_HigherVD()
    {
        var short100 = BuswaySizingService.Specify(1200, 480, 100);
        var long500 = BuswaySizingService.Specify(1200, 480, 500);
        Assert.True(long500.VoltageDropPercent > short100.VoltageDropPercent);
    }

    [Fact]
    public void Specify_PlugInType()
    {
        var spec = BuswaySizingService.Specify(600, 208, 80,
            busType: BuswaySizingService.BuswayType.PlugIn);
        Assert.Equal(BuswaySizingService.BuswayType.PlugIn, spec.Type);
    }

    // ── Tap Rules ────────────────────────────────────────────────────────────

    [Fact]
    public void TapRule_10ft_Compliant()
    {
        // 1200A busway, 200A tap (16.7%) within 10 ft → ≥ 10% → OK
        var result = BuswaySizingService.EvaluateTapRule(1200, 200, 8);
        Assert.True(result.IsCompliant);
        Assert.Contains("10-ft", result.NecReference);
    }

    [Fact]
    public void TapRule_10ft_TooSmall()
    {
        // 1200A busway, 50A tap (4.2%) within 10 ft → < 10% → fail
        var result = BuswaySizingService.EvaluateTapRule(1200, 50, 8);
        Assert.False(result.IsCompliant);
    }

    [Fact]
    public void TapRule_25ft_Compliant()
    {
        // 1200A busway, 500A tap (41.7%) within 25 ft → ≥ 33.3% → OK
        var result = BuswaySizingService.EvaluateTapRule(1200, 500, 20);
        Assert.True(result.IsCompliant);
        Assert.Contains("25-ft", result.NecReference);
    }

    [Fact]
    public void TapRule_25ft_TooSmall()
    {
        // 1200A busway, 300A tap (25%) within 25 ft → < 33.3% → fail
        var result = BuswaySizingService.EvaluateTapRule(1200, 300, 15);
        Assert.False(result.IsCompliant);
    }

    [Fact]
    public void TapRule_Over25ft_NotCompliant()
    {
        var result = BuswaySizingService.EvaluateTapRule(1200, 800, 30);
        Assert.False(result.IsCompliant);
        Assert.Contains("point of supply", result.Reason);
    }

    // ── Altitude Derating ────────────────────────────────────────────────────

    [Fact]
    public void AltitudeDerating_SeaLevel_NoDerating()
    {
        double factor = BuswaySizingService.GetAltitudeDerating(0);
        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void AltitudeDerating_5000ft_SomeDerating()
    {
        double factor = BuswaySizingService.GetAltitudeDerating(5000);
        Assert.True(factor < 1.0);
        Assert.True(factor > 0.9);
    }

    [Fact]
    public void AltitudeDerating_MinimumFloor()
    {
        double factor = BuswaySizingService.GetAltitudeDerating(100000);
        Assert.Equal(0.5, factor);
    }
}
