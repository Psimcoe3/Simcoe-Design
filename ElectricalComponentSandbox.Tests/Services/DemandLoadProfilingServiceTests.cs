using System;
using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DemandLoadProfilingServiceTests
{
    private static List<DemandLoadProfilingService.DemandReading> SampleReadings()
    {
        var readings = new List<DemandLoadProfilingService.DemandReading>();
        var baseTime = new DateTime(2024, 7, 15, 0, 0, 0);
        // 96 intervals = 24 hours × 4 (15-min intervals)
        for (int i = 0; i < 96; i++)
        {
            double hour = i / 4.0;
            // Simulate typical commercial profile: low at night, peak afternoon
            double kw = 50 + 150 * Math.Sin(Math.PI * (hour - 6) / 12);
            if (kw < 20) kw = 20; // Base load floor
            double kvar = kw * 0.3; // ~0.96 PF

            readings.Add(new DemandLoadProfilingService.DemandReading
            {
                Timestamp = baseTime.AddMinutes(i * 15),
                KwDemand = Math.Round(kw, 1),
                KvarDemand = Math.Round(kvar, 1),
            });
        }
        return readings;
    }

    // ── Profile Analysis ─────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeProfile_PeakGreaterThanAverage()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(SampleReadings(), 500);
        Assert.True(result.PeakDemandKw > result.AverageDemandKw);
    }

    [Fact]
    public void AnalyzeProfile_LoadFactorBetween0And1()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(SampleReadings(), 500);
        Assert.True(result.LoadFactor > 0 && result.LoadFactor <= 1.0);
    }

    [Fact]
    public void AnalyzeProfile_TotalEnergy_Positive()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(SampleReadings(), 500);
        Assert.True(result.TotalEnergyKwh > 0);
    }

    [Fact]
    public void AnalyzeProfile_DemandFactor_LessThanOne()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(SampleReadings(), 500);
        Assert.True(result.DemandFactor > 0 && result.DemandFactor <= 1.0);
    }

    [Fact]
    public void AnalyzeProfile_EmptyReadings_DefaultResult()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(
            new List<DemandLoadProfilingService.DemandReading>(), 500);
        Assert.Equal(0, result.PeakDemandKw);
    }

    [Fact]
    public void AnalyzeProfile_PowerFactor_Reasonable()
    {
        var result = DemandLoadProfilingService.AnalyzeProfile(SampleReadings(), 500);
        Assert.True(result.PeakPowerFactor > 0.8 && result.PeakPowerFactor <= 1.0);
    }

    // ── Time-of-Use ──────────────────────────────────────────────────────────

    [Fact]
    public void TimeOfUse_OnPeakHigherThanOffPeak()
    {
        var result = DemandLoadProfilingService.AnalyzeTimeOfUse(SampleReadings());
        Assert.True(result.OnPeakAvgKw > result.OffPeakAvgKw);
    }

    [Fact]
    public void TimeOfUse_RatioGreaterThanOne()
    {
        var result = DemandLoadProfilingService.AnalyzeTimeOfUse(SampleReadings());
        Assert.True(result.OnToOffRatio > 1.0);
    }

    [Fact]
    public void TimeOfUse_EnergyValues_Positive()
    {
        var result = DemandLoadProfilingService.AnalyzeTimeOfUse(SampleReadings());
        Assert.True(result.OnPeakEnergyKwh > 0);
        Assert.True(result.OffPeakEnergyKwh > 0);
    }

    // ── Billing Demand ───────────────────────────────────────────────────────

    [Fact]
    public void BillingDemand_CurrentHigher_NoRatchet()
    {
        var result = DemandLoadProfilingService.CalculateBillingDemand(200, 150);
        Assert.Equal(200, result.BillingDemandKw);
    }

    [Fact]
    public void BillingDemand_RatchetApplied()
    {
        var result = DemandLoadProfilingService.CalculateBillingDemand(100, 250, 0.80);
        // Ratchet = 250 × 0.80 = 200 > 100 → billed at 200
        Assert.Equal(200, result.BillingDemandKw);
    }

    [Fact]
    public void BillingDemand_PoorPF_PenaltyApplied()
    {
        var result = DemandLoadProfilingService.CalculateBillingDemand(
            200, 100, 0.80, 0.75, 0.90);
        Assert.True(result.BillingDemandKw > 200);
        Assert.True(result.PowerFactorPenaltyPercent > 0);
    }

    [Fact]
    public void BillingDemand_GoodPF_NoPenalty()
    {
        var result = DemandLoadProfilingService.CalculateBillingDemand(
            200, 100, 0.80, 0.95, 0.90);
        Assert.Equal(0, result.PowerFactorPenaltyPercent);
    }

    // ── Diversity Factor ─────────────────────────────────────────────────────

    [Fact]
    public void DiversityFactor_GreaterThanOne()
    {
        var peaks = new List<double> { 50, 80, 60, 70 };
        double diversity = DemandLoadProfilingService.CalculateDiversityFactor(peaks, 180);
        Assert.True(diversity > 1.0); // 260/180 ≈ 1.44
    }

    [Fact]
    public void DiversityFactor_ZeroCoincident_ReturnsZero()
    {
        var peaks = new List<double> { 50, 80 };
        double diversity = DemandLoadProfilingService.CalculateDiversityFactor(peaks, 0);
        Assert.Equal(0, diversity);
    }
}
