using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.HarmonicAnalysisService;

namespace ElectricalComponentSandbox.Tests.Services;

public class HarmonicAnalysisServiceTests
{
    // ── THD Calculation ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateTHD_EmptySpectrum_ReturnsZero()
    {
        Assert.Equal(0, HarmonicAnalysisService.CalculateTHD(new Dictionary<int, double>()));
    }

    [Fact]
    public void CalculateTHD_SingleHarmonic()
    {
        // 5th harmonic at 20% → THD = 20%
        var h = new Dictionary<int, double> { [5] = 20.0 };
        Assert.Equal(20.0, HarmonicAnalysisService.CalculateTHD(h), 1);
    }

    [Fact]
    public void CalculateTHD_MultipleHarmonics()
    {
        // √(20² + 14.3²) = √(400 + 204.49) = √604.49 = 24.59%
        var h = new Dictionary<int, double> { [5] = 20.0, [7] = 14.3 };
        var thd = HarmonicAnalysisService.CalculateTHD(h);
        Assert.InRange(thd, 24.5, 24.7);
    }

    [Fact]
    public void CalculateTHD_SixPulseVFD_Typical()
    {
        var spectrum = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.SixPulseVFD);
        var thd = HarmonicAnalysisService.CalculateTHD(spectrum);
        // 6-pulse VFD typically 25-30% THD
        Assert.InRange(thd, 25, 35);
    }

    // ── K-Factor ─────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateKFactor_NoHarmonics_ReturnsOne()
    {
        Assert.Equal(1.0, HarmonicAnalysisService.CalculateKFactor(new Dictionary<int, double>()));
    }

    [Fact]
    public void CalculateKFactor_HighHarmonics_HighK()
    {
        var computer = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.SinglePhaseComputer);
        var kFactor = HarmonicAnalysisService.CalculateKFactor(computer);
        // Single-phase computers with high 3rd harmonic → K > 4
        Assert.True(kFactor > 4, $"Computer K-factor should be > 4, got {kFactor}");
    }

    [Fact]
    public void CalculateKFactor_VFD_ModerateK()
    {
        var vfd = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.SixPulseVFD);
        var kFactor = HarmonicAnalysisService.CalculateKFactor(vfd);
        // 6-pulse VFD → K typically 4-10
        Assert.True(kFactor > 3 && kFactor < 20, $"VFD K-factor out of range: {kFactor}");
    }

    // ── RMS Current ──────────────────────────────────────────────────────────

    [Fact]
    public void CalculateRMSCurrent_NoHarmonics_EqualsFundamental()
    {
        var rms = HarmonicAnalysisService.CalculateRMSCurrent(100, new Dictionary<int, double>());
        Assert.Equal(100, rms);
    }

    [Fact]
    public void CalculateRMSCurrent_WithHarmonics_HigherThanFundamental()
    {
        var h = new Dictionary<int, double> { [3] = 80.0, [5] = 60.0 };
        var rms = HarmonicAnalysisService.CalculateRMSCurrent(100, h);
        Assert.True(rms > 100, "RMS with harmonics must exceed fundamental");
    }

    [Fact]
    public void CalculateRMSCurrent_Formula()
    {
        // I_rms = 100 × √(1 + 0.2² + 0.1²) = 100 × √(1.05) ≈ 102.5
        var h = new Dictionary<int, double> { [5] = 20.0, [7] = 10.0 };
        var rms = HarmonicAnalysisService.CalculateRMSCurrent(100, h);
        Assert.InRange(rms, 102, 103);
    }

    // ── Neutral Current ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateNeutralCurrent_NoTriplens_Zero()
    {
        // Only 5th and 7th → no triplen → neutral = 0
        var h = new Dictionary<int, double> { [5] = 20.0, [7] = 14.0 };
        var neutral = HarmonicAnalysisService.CalculateNeutralCurrent(100, h);
        Assert.Equal(0, neutral);
    }

    [Fact]
    public void CalculateNeutralCurrent_HighThirdHarmonic()
    {
        // 3rd at 80% → neutral = 3 × 100 × 0.80 = 240A
        var h = new Dictionary<int, double> { [3] = 80.0 };
        var neutral = HarmonicAnalysisService.CalculateNeutralCurrent(100, h);
        Assert.Equal(240, neutral);
    }

    [Fact]
    public void CalculateNeutralCurrent_MultipleTriplens()
    {
        // 3rd at 80%, 9th at 20% → neutral = 3 × √(80² + 20²) = 3 × √6800 = 3 × 82.46 ≈ 247.4
        var h = new Dictionary<int, double> { [3] = 80.0, [9] = 20.0 };
        var neutral = HarmonicAnalysisService.CalculateNeutralCurrent(100, h);
        Assert.InRange(neutral, 247, 248);
    }

    [Fact]
    public void CalculateNeutralCurrent_Computers_ExceedsPhase()
    {
        // Single-phase computers have massive triplen content → neutral > phase
        var h = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.SinglePhaseComputer);
        var neutral = HarmonicAnalysisService.CalculateNeutralCurrent(100, h);
        Assert.True(neutral > 100, "Computer load neutral should exceed phase current");
    }

    // ── Typical Spectra ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(NonlinearLoadType.SixPulseVFD)]
    [InlineData(NonlinearLoadType.TwelvePulseVFD)]
    [InlineData(NonlinearLoadType.SinglePhaseComputer)]
    [InlineData(NonlinearLoadType.LED_Lighting)]
    [InlineData(NonlinearLoadType.UPS)]
    [InlineData(NonlinearLoadType.Welder)]
    public void GetTypicalSpectrum_ReturnsNonEmpty(NonlinearLoadType loadType)
    {
        var spectrum = HarmonicAnalysisService.GetTypicalSpectrum(loadType);
        Assert.NotEmpty(spectrum);
    }

    [Fact]
    public void GetTypicalSpectrum_TwelvePulse_NoFifth()
    {
        // 12-pulse VFD cancels 5th and 7th harmonics
        var spectrum = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.TwelvePulseVFD);
        Assert.False(spectrum.ContainsKey(5));
        Assert.False(spectrum.ContainsKey(7));
    }

    // ── Full Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_CleanLoad_NoViolations()
    {
        var spectrum = new HarmonicSpectrum
        {
            Id = "L1", FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double> { [5] = 2.0, [7] = 1.5 },
        };
        var result = HarmonicAnalysisService.Analyze(spectrum);
        Assert.False(result.ExceedsTHDLimit);
        Assert.False(result.ExceedsIndividualLimit);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Analyze_ComputerLoad_Violations()
    {
        var h = HarmonicAnalysisService.GetTypicalSpectrum(NonlinearLoadType.SinglePhaseComputer);
        var spectrum = new HarmonicSpectrum
        {
            Id = "PC", FundamentalAmps = 50,
            HarmonicPercents = h,
        };
        var result = HarmonicAnalysisService.Analyze(spectrum);
        Assert.True(result.ExceedsTHDLimit);
        Assert.True(result.ExceedsIndividualLimit);
        Assert.NotEmpty(result.Violations);
        Assert.True(result.KFactor > 1);
    }

    [Fact]
    public void Analyze_NeutralToPhaseRatio()
    {
        var h = new Dictionary<int, double> { [3] = 80.0 };
        var spectrum = new HarmonicSpectrum { FundamentalAmps = 100, HarmonicPercents = h };
        var result = HarmonicAnalysisService.Analyze(spectrum);
        // neutral = 240, phase = 100 → ratio = 2.4
        Assert.Equal(2.4, result.NeutralToPhaseRatio);
    }

    [Fact]
    public void Analyze_WorstHarmonicIdentified()
    {
        var h = new Dictionary<int, double> { [3] = 10.0, [5] = 25.0, [7] = 15.0 };
        var spectrum = new HarmonicSpectrum { FundamentalAmps = 100, HarmonicPercents = h };
        var result = HarmonicAnalysisService.Analyze(spectrum);
        Assert.Equal(5, result.WorstHarmonicOrder);
        Assert.Equal(25.0, result.WorstIndividualHarmonicPercent);
    }

    // ── Bus Combination ──────────────────────────────────────────────────────

    [Fact]
    public void CombineAtBus_SingleLoad_SameAsOriginal()
    {
        var load = new HarmonicSpectrum
        {
            Id = "L1", FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double> { [5] = 20.0 },
        };
        var combined = HarmonicAnalysisService.CombineAtBus(new[] { load });
        Assert.Equal(100, combined.FundamentalAmps);
        Assert.Equal(20.0, combined.HarmonicPercents[5]);
    }

    [Fact]
    public void CombineAtBus_TwoEqualLoads_AmpsDouble_PercentSame()
    {
        var load1 = new HarmonicSpectrum
        {
            FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double> { [5] = 20.0 },
        };
        var load2 = new HarmonicSpectrum
        {
            FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double> { [5] = 20.0 },
        };
        var combined = HarmonicAnalysisService.CombineAtBus(new[] { load1, load2 });
        Assert.Equal(200, combined.FundamentalAmps);
        Assert.Equal(20.0, combined.HarmonicPercents[5]);
    }

    [Fact]
    public void CombineAtBus_DifferentLoads_WeightedAverage()
    {
        var vfd = new HarmonicSpectrum
        {
            FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double> { [5] = 20.0, [7] = 14.0 },
        };
        var linear = new HarmonicSpectrum
        {
            FundamentalAmps = 100,
            HarmonicPercents = new Dictionary<int, double>(), // clean load
        };
        var combined = HarmonicAnalysisService.CombineAtBus(new[] { vfd, linear });
        Assert.Equal(200, combined.FundamentalAmps);
        // 5th: 100×20% = 20A / 200 total = 10%
        Assert.Equal(10.0, combined.HarmonicPercents[5]);
    }

    [Fact]
    public void CombineAtBus_Empty_ReturnsZero()
    {
        var combined = HarmonicAnalysisService.CombineAtBus(Array.Empty<HarmonicSpectrum>());
        Assert.Equal(0, combined.FundamentalAmps);
    }
}
