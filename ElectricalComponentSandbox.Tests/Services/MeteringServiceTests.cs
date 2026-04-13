using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MeteringServiceTests
{
    // ── CT Primary Selection ─────────────────────────────────────────────────

    [Theory]
    [InlineData(150, 200)]   // 150/0.8 = 187.5 → next standard = 200
    [InlineData(400, 500)]   // 400/0.8 = 500 → 500
    [InlineData(800, 1000)]  // 800/0.8 = 1000 → 1000
    [InlineData(1500, 2000)] // 1500/0.8 = 1875 → 2000
    public void SelectCtPrimary_CorrectSize(double loadAmps, int expectedMin)
    {
        int primary = MeteringService.SelectCtPrimary(loadAmps);
        Assert.True(primary >= expectedMin);
        Assert.Contains(primary, MeteringService.StandardCtPrimaries);
    }

    [Fact]
    public void SelectCtPrimary_VeryLargeLoad_ReturnsMax()
    {
        int primary = MeteringService.SelectCtPrimary(10000);
        Assert.Equal(6000, primary);
    }

    // ── CT Specification ─────────────────────────────────────────────────────

    [Fact]
    public void SizeCt_BasicMetering()
    {
        var ct = MeteringService.SizeCt(800);
        Assert.Equal(5, ct.SecondaryAmps);
        Assert.True(ct.PrimaryAmps >= 800);
        Assert.True(ct.Ratio > 1);
        Assert.True(ct.IsAdequate);
        Assert.True(ct.UtilizationPercent > 0);
    }

    [Fact]
    public void SizeCt_BurdenExceeded()
    {
        var ct = MeteringService.SizeCt(800, burdenVA: 20, maxBurdenVA: 12.5);
        Assert.False(ct.IsAdequate);
    }

    [Fact]
    public void SizeCt_LowUtilization_StillReasonable()
    {
        // 10A load → selects 50A CT → 20% utilization
        var ct = MeteringService.SizeCt(10);
        Assert.Equal(50, ct.PrimaryAmps);
        Assert.True(ct.UtilizationPercent <= 25);
    }

    // ── PT Specification ─────────────────────────────────────────────────────

    [Fact]
    public void SizePt_480V()
    {
        var pt = MeteringService.SizePt(480);
        Assert.Equal(480, pt.PrimaryVoltage);
        Assert.Equal(120, pt.SecondaryVoltage);
        Assert.Equal(4.0, pt.Ratio);
    }

    [Fact]
    public void SizePt_4160V()
    {
        var pt = MeteringService.SizePt(4160);
        Assert.Equal(4160, pt.PrimaryVoltage);
        Assert.True(pt.Ratio > 30);
    }

    // ── Full Metering Specification ──────────────────────────────────────────

    [Fact]
    public void Specify_200A_SelfContained()
    {
        var spec = MeteringService.SpecifyMetering(200, 208);
        Assert.Equal(MeteringService.MeterType.SelfContained, spec.Type);
        Assert.Equal(MeteringService.MeterClass.Class200, spec.Class);
        Assert.Null(spec.CtSpec);
        Assert.Equal(0, spec.CtQuantity);
        Assert.False(spec.RequiresPt);
    }

    [Fact]
    public void Specify_320A_SelfContained()
    {
        var spec = MeteringService.SpecifyMetering(320, 480);
        Assert.Equal(MeteringService.MeterType.SelfContained, spec.Type);
        Assert.Equal(MeteringService.MeterClass.Class320, spec.Class);
    }

    [Fact]
    public void Specify_1200A_TransformerRated()
    {
        var spec = MeteringService.SpecifyMetering(1200, 480, threePhase: true);
        Assert.Equal(MeteringService.MeterType.TransformerRated, spec.Type);
        Assert.Equal(MeteringService.MeterClass.Class20, spec.Class);
        Assert.NotNull(spec.CtSpec);
        Assert.Equal(3, spec.CtQuantity);
        Assert.False(spec.RequiresPt);
    }

    [Fact]
    public void Specify_MediumVoltage_RequiresPt()
    {
        var spec = MeteringService.SpecifyMetering(400, 4160);
        Assert.True(spec.RequiresPt);
        Assert.NotNull(spec.PtSpec);
    }

    [Fact]
    public void Specify_SinglePhase_2CTs()
    {
        var spec = MeteringService.SpecifyMetering(600, 240, threePhase: false);
        Assert.Equal(2, spec.CtQuantity);
    }

    // ── Lead Burden ──────────────────────────────────────────────────────────

    [Fact]
    public void LeadBurden_ZeroLength()
    {
        double burden = MeteringService.CalculateLeadBurden(0);
        Assert.Equal(0, burden);
    }

    [Fact]
    public void LeadBurden_50ft_10AWG()
    {
        // 2 × 50ft × 1.02Ω/1000ft = 0.102Ω; 5A² × 0.102 = 2.55 VA
        double burden = MeteringService.CalculateLeadBurden(50);
        Assert.Equal(2.55, burden);
    }

    [Fact]
    public void LeadBurden_LongerRun_HigherBurden()
    {
        double b50 = MeteringService.CalculateLeadBurden(50);
        double b100 = MeteringService.CalculateLeadBurden(100);
        Assert.True(b100 > b50);
    }
}
