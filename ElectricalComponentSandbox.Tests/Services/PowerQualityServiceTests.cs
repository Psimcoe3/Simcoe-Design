using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class PowerQualityServiceTests
{
    // ── IEEE 519 Limits ──────────────────────────────────────────────────────

    [Fact]
    public void Ieee519Limits_LowRatio_TighterLimits()
    {
        var low = PowerQualityService.GetIeee519Limits(10);
        var high = PowerQualityService.GetIeee519Limits(500);
        Assert.True(low.TddPercent < high.TddPercent);
    }

    [Fact]
    public void Ieee519Limits_HighRatio_RelaxedLimits()
    {
        var limits = PowerQualityService.GetIeee519Limits(1500);
        Assert.Equal(20.0, limits.TddPercent);
    }

    // ── Voltage THD ──────────────────────────────────────────────────────────

    [Fact]
    public void VoltageTHD_LowDistortion_Passes()
    {
        var result = PowerQualityService.EvaluateVoltageTHD(3.5, 0.48);
        Assert.True(result.MeetsIeee519);
        Assert.Equal(8.0, result.LimitPercent); // ≤ 1kV limit
    }

    [Fact]
    public void VoltageTHD_HighDistortion_Fails()
    {
        var result = PowerQualityService.EvaluateVoltageTHD(12.0, 0.48);
        Assert.False(result.MeetsIeee519);
    }

    [Fact]
    public void VoltageTHD_MediumVoltage_TighterLimit()
    {
        var result = PowerQualityService.EvaluateVoltageTHD(4.5, 13.8);
        Assert.True(result.MeetsIeee519);
        Assert.Equal(5.0, result.LimitPercent);
    }

    [Fact]
    public void VoltageTHD_HighVoltage_VeryTight()
    {
        var result = PowerQualityService.EvaluateVoltageTHD(2.0, 138);
        Assert.Equal(2.5, result.LimitPercent);
    }

    // ── Current TDD ──────────────────────────────────────────────────────────

    [Fact]
    public void CurrentTDD_WithinLimit_Passes()
    {
        var result = PowerQualityService.EvaluateCurrentTDD(4.0, 15);
        Assert.True(result.MeetsIeee519);
    }

    [Fact]
    public void CurrentTDD_ExceedsLimit_Fails()
    {
        var result = PowerQualityService.EvaluateCurrentTDD(10.0, 15);
        Assert.False(result.MeetsIeee519);
    }

    // ── Voltage Event Classification ─────────────────────────────────────────

    [Fact]
    public void VoltageEvent_DeepSag_ClassifiedCorrectly()
    {
        var evt = PowerQualityService.ClassifyVoltageEvent(0.5, 0.1);
        Assert.Equal(PowerQualityService.VoltageEventType.Sag, evt.Type);
    }

    [Fact]
    public void VoltageEvent_Interruption_Below01()
    {
        var evt = PowerQualityService.ClassifyVoltageEvent(0.05, 2.0);
        Assert.Equal(PowerQualityService.VoltageEventType.Interruption, evt.Type);
    }

    [Fact]
    public void VoltageEvent_Swell_Above11()
    {
        var evt = PowerQualityService.ClassifyVoltageEvent(1.3, 0.5);
        Assert.Equal(PowerQualityService.VoltageEventType.Swell, evt.Type);
    }

    [Fact]
    public void VoltageEvent_SustainedOvervoltage()
    {
        var evt = PowerQualityService.ClassifyVoltageEvent(1.15, 120);
        Assert.Equal(PowerQualityService.VoltageEventType.Overvoltage, evt.Type);
        Assert.Equal(PowerQualityService.VoltageEventDuration.Sustained, evt.Duration);
    }

    [Fact]
    public void VoltageEvent_MomentaryDuration()
    {
        var evt = PowerQualityService.ClassifyVoltageEvent(0.6, 1.5);
        Assert.Equal(PowerQualityService.VoltageEventDuration.Momentary, evt.Duration);
    }

    // ── Reliability Indices ──────────────────────────────────────────────────

    [Fact]
    public void Reliability_NoInterruptions_PerfectASAI()
    {
        var result = PowerQualityService.CalculateReliability(
            new List<double>(), new List<int>(), 1000);
        Assert.Equal(1.0, result.ASAI);
    }

    [Fact]
    public void Reliability_SingleEvent_CorrectSAIDI()
    {
        var result = PowerQualityService.CalculateReliability(
            new List<double> { 60 },     // 60 min outage
            new List<int> { 500 },        // 500 of 1000 customers
            1000);
        Assert.Equal(30.0, result.SAIDI);  // 60*500/1000 = 30
        Assert.Equal(0.5, result.SAIFI, 4); // 500/1000
    }

    [Fact]
    public void Reliability_ASAI_LessThanOne()
    {
        var result = PowerQualityService.CalculateReliability(
            new List<double> { 120 },
            new List<int> { 1000 },
            1000);
        Assert.True(result.ASAI < 1.0);
        Assert.True(result.ASAI > 0.99); // 120 min is tiny fraction of year
    }
}
