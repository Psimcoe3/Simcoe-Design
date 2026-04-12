using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class TransformerSizingServiceTests
{
    // ── Full-Load Amps ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(75, 480, 3, 90.2)]    // 75kVA 480V 3Φ → 90.2A
    [InlineData(75, 208, 3, 208.2)]   // 75kVA 208V 3Φ → 208.2A
    [InlineData(150, 480, 3, 180.4)]  // 150kVA 480V 3Φ
    [InlineData(1000, 480, 3, 1202.8)] // 1000kVA 480V 3Φ
    [InlineData(25, 240, 1, 104.2)]   // 25kVA 240V 1Φ
    [InlineData(50, 120, 1, 416.7)]   // 50kVA 120V 1Φ
    public void GetFullLoadAmps_CalculatesCorrectly(double kva, double voltage, int phases, double expected)
    {
        var result = TransformerSizingService.GetFullLoadAmps(kva, voltage, phases);
        Assert.Equal(expected, Math.Round(result, 1));
    }

    [Fact]
    public void GetFullLoadAmps_ZeroKVA_ReturnsZero()
    {
        Assert.Equal(0, TransformerSizingService.GetFullLoadAmps(0, 480));
    }

    [Fact]
    public void GetFullLoadAmps_ZeroVoltage_ReturnsZero()
    {
        Assert.Equal(0, TransformerSizingService.GetFullLoadAmps(75, 0));
    }

    // ── kVA Selection ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 80, 75)]     // 50kVA load at 80% → need 62.5 → std 75
    [InlineData(100, 80, 150)]   // 100kVA load → need 125 → std 150
    [InlineData(200, 80, 250)]   // 200kVA load → need 250 → std 250
    [InlineData(37.5, 100, 37.5)] // Exact match at 100% loading
    [InlineData(70, 80, 100)]    // 70kVA → need 87.5 → std 100
    public void SelectTransformerKVA_ReturnsCorrectStandard(double loadKVA, double maxPercent, double expected)
    {
        var result = TransformerSizingService.SelectTransformerKVA(loadKVA, maxPercent);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SelectTransformerKVA_ZeroLoad_ReturnsSmallest()
    {
        var result = TransformerSizingService.SelectTransformerKVA(0);
        Assert.Equal(3, result);
    }

    [Fact]
    public void SelectTransformerKVA_HugeLoad_ReturnsLargest()
    {
        var result = TransformerSizingService.SelectTransformerKVA(10000);
        Assert.Equal(5000, result);
    }

    // ── OCPD Sizing (NEC 450.3(B)) ──────────────────────────────────────────

    [Fact]
    public void SizeOCPD_PrimaryOnly_125Percent()
    {
        // 75kVA, 480V 3Φ: primary FLA ≈ 90.2A → 125% = 112.8 → std 125A
        var result = TransformerSizingService.SizeOCPD(75, 480, 208, 3, false);
        Assert.Equal(125, result.PrimaryOCPDAmps);
        Assert.Equal(0, result.SecondaryOCPDAmps);
    }

    [Fact]
    public void SizeOCPD_WithSecondary_PrimaryAndSecondary()
    {
        // 75kVA, 480V/208V 3Φ: secondary FLA ≈ 208.2A → 125% = 260 → std 300A
        var result = TransformerSizingService.SizeOCPD(75, 480, 208, 3, true);
        Assert.True(result.PrimaryOCPDAmps > 0);
        Assert.True(result.SecondaryOCPDAmps > 0);
    }

    [Fact]
    public void SizeOCPD_SinglePhase()
    {
        // 25kVA, 240V/120V 1Φ: primary FLA = 104.2A → 125% = 130 → std 150A
        var result = TransformerSizingService.SizeOCPD(25, 240, 120, 1, false);
        Assert.True(result.PrimaryOCPDAmps >= 125);
    }

    [Fact]
    public void SizeOCPD_ReturnsCorrectFLA()
    {
        var result = TransformerSizingService.SizeOCPD(75, 480, 208, 3);
        Assert.Equal(90.2, result.PrimaryFLA);
        Assert.Equal(208.2, result.SecondaryFLA);
    }

    [Fact]
    public void SizeOCPD_ReturnsTransformerInfo()
    {
        var result = TransformerSizingService.SizeOCPD(150, 480, 208);
        Assert.Equal(150, result.KVA);
        Assert.Equal(480, result.PrimaryVoltage);
        Assert.Equal(208, result.SecondaryVoltage);
    }

    // ── Losses ───────────────────────────────────────────────────────────────

    [Fact]
    public void EstimateLosses_FullLoad()
    {
        // 75kVA full load, 2% copper at FL, 0.5% core
        var result = TransformerSizingService.EstimateLosses(75, 75);
        Assert.Equal(100.0, result.LoadingPercent);
        Assert.Equal(1.5, result.CopperLossKW);   // 75 × 2% × 1²
        Assert.Equal(0.375, result.CoreLossKW);    // 75 × 0.5%
        Assert.Equal(1.875, result.TotalLossKW);
    }

    [Fact]
    public void EstimateLosses_HalfLoad_CopperReduced()
    {
        var result = TransformerSizingService.EstimateLosses(75, 37.5);
        Assert.Equal(50.0, result.LoadingPercent);
        Assert.Equal(0.375, result.CopperLossKW);  // 75 × 2% × 0.5²
        Assert.Equal(0.375, result.CoreLossKW);     // same core loss
    }

    [Fact]
    public void EstimateLosses_NoLoad_OnlyCoreLoss()
    {
        var result = TransformerSizingService.EstimateLosses(75, 0);
        Assert.Equal(0, result.CopperLossKW);
        Assert.Equal(0.375, result.CoreLossKW);
        Assert.Equal(0, result.EfficiencyPercent); // no output = 0% efficiency
    }

    [Fact]
    public void EstimateLosses_Efficiency_HighLoad()
    {
        var result = TransformerSizingService.EstimateLosses(75, 75);
        // Efficiency = 75 / (75 + 1.875) = 97.56%
        Assert.True(result.EfficiencyPercent > 97 && result.EfficiencyPercent < 98);
    }

    [Fact]
    public void EstimateLosses_CustomLossPercentages()
    {
        var result = TransformerSizingService.EstimateLosses(100, 100,
            copperLossPercentAtFull: 3.0, coreLossPercent: 1.0);
        Assert.Equal(3.0, result.CopperLossKW);
        Assert.Equal(1.0, result.CoreLossKW);
    }

    // ── K-Factor Derating ────────────────────────────────────────────────────

    [Fact]
    public void DerateForHarmonics_KFactor1_NoDerate()
    {
        Assert.Equal(75, TransformerSizingService.DerateForHarmonics(75, 1.0));
    }

    [Fact]
    public void DerateForHarmonics_KRatedIgnored()
    {
        Assert.Equal(75, TransformerSizingService.DerateForHarmonics(75, 4.0, isKRated: true));
    }

    [Theory]
    [InlineData(75, 4.0, 37.5)]    // 75 / √4 = 37.5
    [InlineData(100, 9.0, 33.3)]   // 100 / √9 ≈ 33.3
    [InlineData(150, 13.0, 41.6)]  // 150 / √13 ≈ 41.6
    public void DerateForHarmonics_ReducesCapacity(double rated, double kFactor, double expected)
    {
        var result = TransformerSizingService.DerateForHarmonics(rated, kFactor);
        Assert.Equal(expected, Math.Round(result, 1));
    }

    [Fact]
    public void DerateForHarmonics_BelowKFactor1_NoDerate()
    {
        Assert.Equal(75, TransformerSizingService.DerateForHarmonics(75, 0.5));
    }

    // ── Real-World Sizing Scenarios ──────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialOffice75kVA()
    {
        // 55 kVA office load → select xfmr at 80% loading
        double kva = TransformerSizingService.SelectTransformerKVA(55, 80);
        Assert.Equal(75, kva);

        var ocpd = TransformerSizingService.SizeOCPD(kva, 480, 208, 3, true);
        Assert.True(ocpd.PrimaryOCPDAmps > 0);
        Assert.True(ocpd.SecondaryOCPDAmps > 0);

        var losses = TransformerSizingService.EstimateLosses(kva, 55);
        Assert.True(losses.EfficiencyPercent > 96);
    }

    [Fact]
    public void RealWorld_DataCenter_KFactor()
    {
        // Data center with K-13 harmonic load on non-K-rated 150kVA
        double derated = TransformerSizingService.DerateForHarmonics(150, 13, false);
        Assert.True(derated < 150);

        // Need 100 kVA effective after K-13 derating
        // Effective = rated / √K → rated = effective × √K = 100 × √13 ≈ 360.6 kVA
        double requiredRated = 100 * Math.Sqrt(13);
        double selectedKVA = TransformerSizingService.SelectTransformerKVA(requiredRated, 100);
        Assert.True(selectedKVA >= requiredRated);
    }
}
