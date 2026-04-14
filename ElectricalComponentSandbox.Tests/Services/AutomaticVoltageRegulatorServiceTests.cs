using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class AutomaticVoltageRegulatorServiceTests
{
    [Fact]
    public void ApplyReactiveDroop_ZeroReactiveLoad_NoChange()
    {
        double result = AutomaticVoltageRegulatorService.ApplyReactiveDroop(1.0, 0);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ApplyReactiveDroop_LaggingReactiveLoad_ReducesSetpoint()
    {
        double result = AutomaticVoltageRegulatorService.ApplyReactiveDroop(1.0, 50, 5);

        Assert.True(result < 1.0);
    }

    [Fact]
    public void ApplyReactiveDroop_HigherDroopPercent_MoreReduction()
    {
        double low = AutomaticVoltageRegulatorService.ApplyReactiveDroop(1.0, 50, 3);
        double high = AutomaticVoltageRegulatorService.ApplyReactiveDroop(1.0, 50, 5);

        Assert.True(high < low);
    }

    [Fact]
    public void CalculateLineDropCompensation_HigherCurrent_IncreasesCompensation()
    {
        double low = AutomaticVoltageRegulatorService.CalculateLineDropCompensation(100, 0.01, 0.02);
        double high = AutomaticVoltageRegulatorService.CalculateLineDropCompensation(200, 0.01, 0.02);

        Assert.True(high > low);
    }

    [Fact]
    public void CalculateLineDropCompensation_HigherReactance_IncreasesCompensation()
    {
        double low = AutomaticVoltageRegulatorService.CalculateLineDropCompensation(100, 0.01, 0.01, 0.8);
        double high = AutomaticVoltageRegulatorService.CalculateLineDropCompensation(100, 0.01, 0.03, 0.8);

        Assert.True(high > low);
    }

    [Fact]
    public void CalculateFieldCommand_LowMeasuredVoltage_RaisesCommand()
    {
        double result = AutomaticVoltageRegulatorService.CalculateFieldCommand(0.97, 1.0);

        Assert.True(result > 50);
    }

    [Fact]
    public void CalculateFieldCommand_HighMeasuredVoltage_LowersCommand()
    {
        double result = AutomaticVoltageRegulatorService.CalculateFieldCommand(1.03, 1.0);

        Assert.True(result < 50);
    }

    [Fact]
    public void CalculateFieldCommand_ClampsAtCeiling()
    {
        double result = AutomaticVoltageRegulatorService.CalculateFieldCommand(0.7, 1.0, ceilingPercent: 100);

        Assert.Equal(100, result);
    }

    [Fact]
    public void CalculateFieldCommand_ClampsAtFloor()
    {
        double result = AutomaticVoltageRegulatorService.CalculateFieldCommand(1.3, 1.0, floorPercent: 0, ceilingPercent: 100);

        Assert.Equal(0, result);
    }

    [Fact]
    public void EvaluateResponse_ReactiveDroop_UsesAdjustedSetpoint()
    {
        var result = AutomaticVoltageRegulatorService.EvaluateResponse(0.99, 1.0,
            AutomaticVoltageRegulatorService.AvrMode.ReactiveDroop, reactiveLoadPercent: 50, droopPercent: 5);

        Assert.True(result.EffectiveSetpointPu < 1.0);
    }

    [Fact]
    public void EvaluateResponse_Isochronous_KeepsOriginalSetpoint()
    {
        var result = AutomaticVoltageRegulatorService.EvaluateResponse(0.99, 1.0,
            AutomaticVoltageRegulatorService.AvrMode.IsochronousVoltage, reactiveLoadPercent: 50, droopPercent: 5);

        Assert.Equal(1.0, result.EffectiveSetpointPu);
    }

    [Fact]
    public void EvaluateResponse_LowVoltage_CanHitCeiling()
    {
        var result = AutomaticVoltageRegulatorService.EvaluateResponse(0.7, 1.0,
            proportionalGain: 300, ceilingPercent: 100);

        Assert.True(result.AtCeiling);
    }

    [Fact]
    public void EvaluateResponse_HighVoltage_CanHitFloor()
    {
        var result = AutomaticVoltageRegulatorService.EvaluateResponse(1.3, 1.0,
            proportionalGain: 300, floorPercent: 0, ceilingPercent: 100);

        Assert.True(result.AtFloor);
    }
}