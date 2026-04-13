using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class EnergyStorageServiceTests
{
    private static EnergyStorageService.BessInput DefaultInput() => new()
    {
        TargetEnergyKWh = 500,
        TargetPowerKW = 250,
        Chemistry = EnergyStorageService.BatteryChemistry.LithiumIon,
        Application = EnergyStorageService.ApplicationMode.PeakShaving,
        MinSocPercent = 10,
        MaxSocPercent = 90,
        SystemVoltage = 480,
        SystemPhases = 3,
    };

    // ── Round-Trip Efficiency ────────────────────────────────────────────────

    [Theory]
    [InlineData(EnergyStorageService.BatteryChemistry.LithiumIon, 0.90)]
    [InlineData(EnergyStorageService.BatteryChemistry.LithiumIronPhosphate, 0.92)]
    [InlineData(EnergyStorageService.BatteryChemistry.LeadAcid, 0.80)]
    [InlineData(EnergyStorageService.BatteryChemistry.FlowVanadium, 0.75)]
    public void GetRoundTripEfficiency_ReturnsExpected(EnergyStorageService.BatteryChemistry chem, double expected)
    {
        Assert.Equal(expected, EnergyStorageService.GetRoundTripEfficiency(chem));
    }

    [Fact]
    public void GetTypicalCycleLife_LFP_HigherThanLeadAcid()
    {
        int lfp = EnergyStorageService.GetTypicalCycleLife(EnergyStorageService.BatteryChemistry.LithiumIronPhosphate);
        int la = EnergyStorageService.GetTypicalCycleLife(EnergyStorageService.BatteryChemistry.LeadAcid);
        Assert.True(lfp > la);
    }

    // ── BESS Sizing ─────────────────────────────────────────────────────────

    [Fact]
    public void SizeBess_GrossExceedsUsable()
    {
        var result = EnergyStorageService.SizeBess(DefaultInput());

        Assert.Equal(500, result.UsableEnergyKWh);
        // 80% SOC window → gross = 500 / 0.8 = 625
        Assert.True(result.GrossEnergyKWh > result.UsableEnergyKWh);
        Assert.Equal(625, result.GrossEnergyKWh, 1);
    }

    [Fact]
    public void SizeBess_CRate_Correct()
    {
        var result = EnergyStorageService.SizeBess(DefaultInput());

        // C-rate = 250 kW / 625 kWh = 0.4C
        Assert.Equal(0.4, result.CRate, 2);
    }

    [Fact]
    public void SizeBess_DischargeDuration_Correct()
    {
        var result = EnergyStorageService.SizeBess(DefaultInput());

        // 500 kWh / 250 kW = 2 hours
        Assert.Equal(2.0, result.DischargeDurationHours, 1);
    }

    [Fact]
    public void SizeBess_MaxCurrent_ThreePhase()
    {
        var result = EnergyStorageService.SizeBess(DefaultInput());

        // 250kW / (480 × √3) ≈ 300.7A
        Assert.True(result.MaxCurrentAmps > 300 && result.MaxCurrentAmps < 302);
    }

    [Fact]
    public void SizeBess_NarrowSocWindow_IncreaseGross()
    {
        var input = DefaultInput() with { MinSocPercent = 20, MaxSocPercent = 80 };
        var result = EnergyStorageService.SizeBess(input);

        // 60% window → 500 / 0.6 = 833.33
        Assert.True(result.GrossEnergyKWh > 830);
    }

    // ── Interconnection ──────────────────────────────────────────────────────

    [Fact]
    public void SizeInterconnection_ContinuousLoad_125Percent()
    {
        var result = EnergyStorageService.SizeInterconnection(DefaultInput());

        Assert.True(result.ContinuousAmps > result.MaxAcAmps);
        Assert.Equal(result.MaxAcAmps * 1.25, result.ContinuousAmps, 1);
    }

    [Fact]
    public void SizeInterconnection_BreakerAdequate()
    {
        var result = EnergyStorageService.SizeInterconnection(DefaultInput());

        Assert.True(result.SelectedBreakerAmps >= result.ContinuousAmps);
    }

    [Fact]
    public void SizeInterconnection_RequiresDisconnect()
    {
        var result = EnergyStorageService.SizeInterconnection(DefaultInput());
        Assert.True(result.RequiresDisconnect); // NEC 706.15
    }

    [Fact]
    public void SizeInterconnection_HasWireSize()
    {
        var result = EnergyStorageService.SizeInterconnection(DefaultInput());
        Assert.False(string.IsNullOrEmpty(result.MinWireSize));
    }

    // ── Peak Shaving ─────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzePeakShaving_ShavesCorrectAmount()
    {
        var result = EnergyStorageService.AnalyzePeakShaving(1000, 800, 3.0);

        Assert.Equal(200, result.ShavingKW);
        // 200 kW × 3 hr × 0.75 = 450 kWh
        Assert.Equal(450, result.RequiredEnergyKWh, 1);
    }

    [Fact]
    public void AnalyzePeakShaving_TargetAbovePeak_ZeroShaving()
    {
        var result = EnergyStorageService.AnalyzePeakShaving(500, 600, 2.0);

        Assert.Equal(0, result.ShavingKW);
        Assert.Equal(0, result.RequiredEnergyKWh);
    }

    [Fact]
    public void AnalyzePeakShaving_EstimatesAnnualCycles()
    {
        var result = EnergyStorageService.AnalyzePeakShaving(1000, 800, 3.0);
        Assert.Equal(260, result.EstimatedCycles); // ~weekdays/year
    }
}
