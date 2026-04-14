using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FaultIndicatorServiceTests
{
    [Fact]
    public void GetDefaultSettings_Underground_RequiresVoltageLoss()
    {
        var result = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Underground);

        Assert.True(result.RequiresVoltageLoss);
        Assert.True(result.PickupAmps > 0);
    }

    [Fact]
    public void GetDefaultSettings_Directional_IsForwardOnly()
    {
        var result = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Directional);

        Assert.True(result.DirectionalForwardOnly);
    }

    [Fact]
    public void DetectFault_AbovePickupAndDuration_ReturnsTrue()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);

        bool result = FaultIndicatorService.DetectFault(settings, 350, 2);

        Assert.True(result);
    }

    [Fact]
    public void DetectFault_BelowPickup_ReturnsFalse()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);

        bool result = FaultIndicatorService.DetectFault(settings, 150, 2);

        Assert.False(result);
    }

    [Fact]
    public void DetectFault_UndergroundWithoutVoltageLoss_ReturnsFalse()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Underground);

        bool result = FaultIndicatorService.DetectFault(settings, 500, 3, voltagePercent: 92);

        Assert.False(result);
    }

    [Fact]
    public void DetectFault_DirectionalReverseFault_ReturnsFalse()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Directional);

        bool result = FaultIndicatorService.DetectFault(settings, 500, 3, isForwardFault: false);

        Assert.False(result);
    }

    [Fact]
    public void CanReset_RequiresLowCurrentAndDelay()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);

        Assert.False(FaultIndicatorService.CanReset(settings, 30, 2));
        Assert.False(FaultIndicatorService.CanReset(settings, 10, 0.2));
        Assert.True(FaultIndicatorService.CanReset(settings, 10, 1.2));
    }

    [Fact]
    public void AssessIndicator_NewFault_LatchesIndicator()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);
        var result = FaultIndicatorService.AssessIndicator(settings, 350, 2, wasLatched: false, lineCurrentAmps: 120, hoursSinceFault: 0.1);

        Assert.True(result.PickedUp);
        Assert.True(result.Latched);
    }

    [Fact]
    public void AssessIndicator_LatchedIndicator_StaysLatchedUntilReset()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);
        var result = FaultIndicatorService.AssessIndicator(settings, 0, 0, wasLatched: true, lineCurrentAmps: 35, hoursSinceFault: 0.5);

        Assert.False(result.PickedUp);
        Assert.True(result.Latched);
        Assert.False(result.CanReset);
    }

    [Fact]
    public void AssessIndicator_ResetConditions_ClearLatch()
    {
        var settings = FaultIndicatorService.GetDefaultSettings(FaultIndicatorService.IndicatorType.Overhead);
        var result = FaultIndicatorService.AssessIndicator(settings, 0, 0, wasLatched: true, lineCurrentAmps: 10, hoursSinceFault: 1.2);

        Assert.True(result.CanReset);
        Assert.False(result.Latched);
    }
}