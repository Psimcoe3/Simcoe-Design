using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class VoltageRegulationServiceTests
{
    // ── Voltage Assessment ───────────────────────────────────────────────────

    [Fact]
    public void EvaluateVoltage_Nominal_WithinRangeA()
    {
        var result = VoltageRegulationService.EvaluateVoltage(480, 480);

        Assert.Equal(0, result.DeviationPercent);
        Assert.True(result.WithinRangeA);
        Assert.True(result.WithinRangeB);
    }

    [Fact]
    public void EvaluateVoltage_HighVoltage_RangeA()
    {
        // 480 + 5% = 504
        var result = VoltageRegulationService.EvaluateVoltage(480, 504);

        Assert.True(result.WithinRangeA);
    }

    [Fact]
    public void EvaluateVoltage_LowVoltage_OutsideRangeA()
    {
        // 480 - 8% = 441.6 → outside Range A (−5%) but within Range B (−8.33%)
        var result = VoltageRegulationService.EvaluateVoltage(480, 441.6);

        Assert.False(result.WithinRangeA);
        Assert.True(result.WithinRangeB);
    }

    [Fact]
    public void EvaluateVoltage_VeryLow_OutsideBothRanges()
    {
        // 480 − 10% = 432 → outside both ranges
        var result = VoltageRegulationService.EvaluateVoltage(480, 432);

        Assert.False(result.WithinRangeA);
        Assert.False(result.WithinRangeB);
    }

    // ── Tap Changer ──────────────────────────────────────────────────────────

    [Fact]
    public void SelectTapPosition_NLTC_Nominal_TapZero()
    {
        var result = VoltageRegulationService.SelectTapPosition(480, 480);

        Assert.Equal(0, result.TapPosition);
        Assert.Equal(480, result.OutputVoltage);
    }

    [Fact]
    public void SelectTapPosition_NLTC_NeedsBoosted()
    {
        // Need ~2.5% boost → tap +1
        var result = VoltageRegulationService.SelectTapPosition(480, 492);

        Assert.Equal(1, result.TapPosition);
        Assert.True(result.OutputVoltage > 480);
    }

    [Fact]
    public void SelectTapPosition_NLTC_ClampedToMaxTaps()
    {
        // Need huge boost → clamped to ±2
        var result = VoltageRegulationService.SelectTapPosition(480, 600);

        Assert.Equal(2, result.TapPosition); // Clamped
    }

    [Fact]
    public void SelectTapPosition_OLTC_FinerSteps()
    {
        var result = VoltageRegulationService.SelectTapPosition(
            480, 485, VoltageRegulationService.RegulationType.OnLoadTapChanger);

        Assert.Equal(0.625, result.TapStepPercent);
        Assert.True(result.TapPosition >= 0);
    }

    // ── Regulator Sizing ─────────────────────────────────────────────────────

    [Fact]
    public void SizeRegulator_CalculatesKVA()
    {
        // 1000 kVA × 10% = 100 kVA required
        var result = VoltageRegulationService.SizeRegulator(1000, 13.8);

        Assert.Equal(100, result.RequiredKVA);
        Assert.True(result.SelectedKVA >= 100);
    }

    [Fact]
    public void SizeRegulator_LoadAmps_Positive()
    {
        var result = VoltageRegulationService.SizeRegulator(500, 4.16);

        Assert.True(result.LoadAmps > 0);
    }

    [Fact]
    public void SizeRegulator_Steps_32For10Percent()
    {
        var result = VoltageRegulationService.SizeRegulator(500, 4.16, 10);

        Assert.Equal(32, result.NumberOfSteps);
    }

    // ── Voltage Study ────────────────────────────────────────────────────────

    [Fact]
    public void PerformVoltageStudy_SmallDrop_NoRegulationNeeded()
    {
        var result = VoltageRegulationService.PerformVoltageStudy(480, 2, 480);

        Assert.False(result.NeedsRegulation);
        Assert.True(result.LoadVoltage > 460);
    }

    [Fact]
    public void PerformVoltageStudy_LargeDrop_NeedsRegulation()
    {
        var result = VoltageRegulationService.PerformVoltageStudy(480, 8, 480);

        Assert.True(result.NeedsRegulation);
    }

    [Fact]
    public void PerformVoltageStudy_VeryLargeDrop_RecommendsSVR()
    {
        var result = VoltageRegulationService.PerformVoltageStudy(480, 15, 480);

        Assert.True(result.NeedsRegulation);
        Assert.Equal(VoltageRegulationService.RegulationType.StepVoltageRegulator,
            result.RecommendedType);
    }
}
