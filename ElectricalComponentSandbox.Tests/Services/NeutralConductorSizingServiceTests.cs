using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class NeutralConductorSizingServiceTests
{
    // ── Wire Sizing ──────────────────────────────────────────────────────────

    [Fact]
    public void GetMinWireSize_SmallLoad_Returns14()
    {
        Assert.Equal("14", NeutralConductorSizingService.GetMinWireSize(15));
    }

    [Fact]
    public void GetMinWireSize_LargeLoad_ReturnsLargerSize()
    {
        string size = NeutralConductorSizingService.GetMinWireSize(400);
        Assert.Equal("600", size); // 500kcmil = 380A < 400A → next is 600 at 420A
    }

    // ── NEC 220.61 Neutral Load ──────────────────────────────────────────────

    [Fact]
    public void NeutralLoad_Under200A_NoReduction()
    {
        var result = NeutralConductorSizingService.CalculateNeutralLoad(150);
        Assert.Equal(150, result.TotalNeutralLoadAmps);
    }

    [Fact]
    public void NeutralLoad_Over200A_70PercentReduction()
    {
        var result = NeutralConductorSizingService.CalculateNeutralLoad(300);
        // 200 + (100 × 0.70) = 270
        Assert.Equal(270, result.TotalNeutralLoadAmps);
    }

    [Fact]
    public void NeutralLoad_NoReduction_FullAmount()
    {
        var result = NeutralConductorSizingService.CalculateNeutralLoad(300, false);
        Assert.Equal(300, result.TotalNeutralLoadAmps);
    }

    // ── Harmonic Neutral Current ─────────────────────────────────────────────

    [Fact]
    public void HarmonicNeutral_ZeroHarmonics_ZeroCurrent()
    {
        double neutral = NeutralConductorSizingService.EstimateHarmonicNeutralCurrent(100, 0);
        Assert.Equal(0, neutral);
    }

    [Fact]
    public void HarmonicNeutral_33PercentThird_NeutralEqualsPhase()
    {
        // 3 × 100 × 0.33 ≈ 99
        double neutral = NeutralConductorSizingService.EstimateHarmonicNeutralCurrent(100, 33);
        Assert.Equal(99, neutral, 1);
    }

    [Fact]
    public void HarmonicNeutral_HighHarmonics_ExceedsPhase()
    {
        double neutral = NeutralConductorSizingService.EstimateHarmonicNeutralCurrent(100, 50);
        Assert.True(neutral > 100); // 3 × 100 × 0.50 = 150
    }

    // ── Neutral Sizing ───────────────────────────────────────────────────────

    [Fact]
    public void SizeNeutral_SinglePhase2Wire_EqualsPhase()
    {
        var result = NeutralConductorSizingService.SizeNeutral(
            100, NeutralConductorSizingService.SystemType.SinglePhase2Wire);
        Assert.Equal(100, result.NeutralCurrentAmps);
        Assert.Equal(1.0, result.NeutralToPhaseRatio, 4);
    }

    [Fact]
    public void SizeNeutral_3Phase_Linear_Reduced()
    {
        var result = NeutralConductorSizingService.SizeNeutral(
            100, NeutralConductorSizingService.SystemType.ThreePhase4Wire,
            NeutralConductorSizingService.LoadType.Linear);
        Assert.True(result.NeutralCurrentAmps < 100); // 70%
        Assert.False(result.IsOversizedNeutral);
    }

    [Fact]
    public void SizeNeutral_3Phase_NonlinearHeavy_Oversized()
    {
        var result = NeutralConductorSizingService.SizeNeutral(
            100, NeutralConductorSizingService.SystemType.ThreePhase4Wire,
            NeutralConductorSizingService.LoadType.NonlinearHeavy, 50);
        Assert.True(result.NeutralCurrentAmps >= 100);
        Assert.True(result.IsOversizedNeutral);
    }

    [Fact]
    public void SizeNeutral_NonlinearLight_NoReduction()
    {
        var result = NeutralConductorSizingService.SizeNeutral(
            100, NeutralConductorSizingService.SystemType.ThreePhase4Wire,
            NeutralConductorSizingService.LoadType.NonlinearLight);
        Assert.Equal(100, result.NeutralCurrentAmps);
    }

    // ── Shared Neutral ───────────────────────────────────────────────────────

    [Fact]
    public void SharedNeutral_Balanced3Phase_SmallNeutral()
    {
        var result = NeutralConductorSizingService.SizeSharedNeutral(
            new List<double> { 80, 80, 80 });
        Assert.True(result.NeutralCurrentAmps <= 80);
    }

    [Fact]
    public void SharedNeutral_Unbalanced3Phase_LargerNeutral()
    {
        var result = NeutralConductorSizingService.SizeSharedNeutral(
            new List<double> { 80, 40, 60 });
        Assert.True(result.NeutralCurrentAmps >= 40); // At least the unbalance
    }

    [Fact]
    public void SharedNeutral_1Phase3Wire_FullPhase()
    {
        var result = NeutralConductorSizingService.SizeSharedNeutral(
            new List<double> { 80, 50 });
        Assert.Equal(80, result.NeutralCurrentAmps); // Max phase
    }

    [Fact]
    public void SharedNeutral_NonlinearHeavy_FullPhase()
    {
        var result = NeutralConductorSizingService.SizeSharedNeutral(
            new List<double> { 80, 60, 70 },
            NeutralConductorSizingService.LoadType.NonlinearHeavy);
        Assert.Equal(80, result.NeutralCurrentAmps); // Max phase
    }
}
