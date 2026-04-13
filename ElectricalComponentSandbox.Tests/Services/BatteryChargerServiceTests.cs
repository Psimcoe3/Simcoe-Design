using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class BatteryChargerServiceTests
{
    [Fact]
    public void CalculateFloatCurrent_LargerBattery_HigherCurrent()
    {
        double small = BatteryChargerService.CalculateFloatCurrent(100, BatterySizingService.BatteryChemistry.LeadAcidVRLA);
        double large = BatteryChargerService.CalculateFloatCurrent(200, BatterySizingService.BatteryChemistry.LeadAcidVRLA);

        Assert.True(large > small);
    }

    [Fact]
    public void CalculateFloatCurrent_FloodedExceedsVrla()
    {
        double vrla = BatteryChargerService.CalculateFloatCurrent(200, BatterySizingService.BatteryChemistry.LeadAcidVRLA);
        double flooded = BatteryChargerService.CalculateFloatCurrent(200, BatterySizingService.BatteryChemistry.LeadAcidFlooded);

        Assert.True(flooded > vrla);
    }

    [Fact]
    public void CalculateRechargeCurrent_ShorterRechargeTime_NeedsMoreCurrent()
    {
        double slow = BatteryChargerService.CalculateRechargeCurrent(200, 12);
        double fast = BatteryChargerService.CalculateRechargeCurrent(200, 6);

        Assert.True(fast > slow);
    }

    [Fact]
    public void CalculateRechargeCurrent_DeeperDischarge_NeedsMoreCurrent()
    {
        double shallow = BatteryChargerService.CalculateRechargeCurrent(200, 8, depthOfDischarge: 0.5);
        double deep = BatteryChargerService.CalculateRechargeCurrent(200, 8, depthOfDischarge: 0.8);

        Assert.True(deep > shallow);
    }

    [Fact]
    public void SizeCharger_IncludesLoadFloatAndRechargeCurrent()
    {
        var result = BatteryChargerService.SizeCharger(20, 200, BatterySizingService.BatteryChemistry.LeadAcidVRLA, rechargeHours: 8);

        Assert.True(result.RequiredCurrentAmps > 20);
        Assert.Equal(result.DcLoadAmps + result.FloatCurrentAmps + result.RechargeCurrentAmps, result.RequiredCurrentAmps, 2);
    }

    [Fact]
    public void SizeCharger_RoundsToStandardSize()
    {
        var result = BatteryChargerService.SizeCharger(20, 200, BatterySizingService.BatteryChemistry.LeadAcidVRLA, rechargeHours: 8);

        Assert.Equal(50, result.SelectedChargerAmps);
    }

    [Fact]
    public void SizeCharger_FloodedBattery_SupportsHigherEqualizeVoltage()
    {
        var result = BatteryChargerService.SizeCharger(20, 200, BatterySizingService.BatteryChemistry.LeadAcidFlooded, nominalDcVoltage: 125);

        Assert.True(result.SupportsEqualize);
        Assert.True(result.EqualizeVoltage > result.FloatVoltage);
    }

    [Fact]
    public void SizeCharger_LithiumIon_DoesNotUseEqualize()
    {
        var result = BatteryChargerService.SizeCharger(20, 200, BatterySizingService.BatteryChemistry.LithiumIon, nominalDcVoltage: 125);

        Assert.False(result.SupportsEqualize);
        Assert.Equal(result.FloatVoltage, result.EqualizeVoltage);
    }

    [Fact]
    public void SizeCharger_LargeCurrent_RecommendsRedundantTopology()
    {
        var result = BatteryChargerService.SizeCharger(80, 400, BatterySizingService.BatteryChemistry.LeadAcidFlooded, rechargeHours: 6);

        Assert.Equal(BatteryChargerService.ChargerTopology.RedundantNPlus1, result.RecommendedTopology);
    }

    [Fact]
    public void SizeCharger_OutputKW_IsPositive()
    {
        var result = BatteryChargerService.SizeCharger(20, 200, BatterySizingService.BatteryChemistry.LeadAcidVRLA);

        Assert.True(result.OutputKW > 0);
    }

    [Fact]
    public void EstimateRechargeTime_LargerCharger_FasterRecharge()
    {
        double slow = BatteryChargerService.EstimateRechargeTimeHours(40, 20, 200);
        double fast = BatteryChargerService.EstimateRechargeTimeHours(60, 20, 200);

        Assert.True(fast < slow);
    }

    [Fact]
    public void EstimateRechargeTime_NoNetChargingCurrent_IsInfinite()
    {
        double result = BatteryChargerService.EstimateRechargeTimeHours(20, 20, 200);

        Assert.True(double.IsPositiveInfinity(result));
    }

    [Fact]
    public void EstimateRechargeTime_KnownValues_AreReasonable()
    {
        double result = BatteryChargerService.EstimateRechargeTimeHours(50, 10, 200);

        Assert.InRange(result, 4, 5);
    }
}