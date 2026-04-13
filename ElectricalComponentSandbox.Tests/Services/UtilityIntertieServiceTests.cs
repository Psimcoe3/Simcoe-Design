using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class UtilityIntertieServiceTests
{
    [Fact]
    public void CalculateNetExchange_LoadExceedsGeneration_IsImport()
    {
        double result = UtilityIntertieService.CalculateNetExchange(500, 350);

        Assert.Equal(150.0, result);
    }

    [Fact]
    public void CalculateNetExchange_GenerationExceedsLoad_IsExport()
    {
        double result = UtilityIntertieService.CalculateNetExchange(300, 450);

        Assert.Equal(-150.0, result);
    }

    [Fact]
    public void DetermineMode_NearBalance_IsZeroExport()
    {
        var mode = UtilityIntertieService.DetermineMode(500, 497);

        Assert.Equal(UtilityIntertieService.IntertieMode.ZeroExport, mode);
    }

    [Fact]
    public void DetermineMode_Islanded_ReturnsIslanded()
    {
        var mode = UtilityIntertieService.DetermineMode(500, 450, islanded: true);

        Assert.Equal(UtilityIntertieService.IntertieMode.Islanded, mode);
    }

    [Fact]
    public void AssessIntertie_WithinExportLimit_Passes()
    {
        var result = UtilityIntertieService.AssessIntertie(300, 350, exportLimitKW: 75);

        Assert.True(result.IsWithinExportLimit);
        Assert.Equal(50.0, result.ExportKW);
    }

    [Fact]
    public void AssessIntertie_ExceedsExportLimit_Fails()
    {
        var result = UtilityIntertieService.AssessIntertie(300, 450, exportLimitKW: 100);

        Assert.False(result.IsWithinExportLimit);
        Assert.Equal(50.0, result.ExportViolationKW);
    }

    [Fact]
    public void AssessIntertie_ImportCase_HasNoExportViolation()
    {
        var result = UtilityIntertieService.AssessIntertie(500, 300, exportLimitKW: 0);

        Assert.Equal(0, result.ExportKW);
        Assert.True(result.IsWithinExportLimit);
    }

    [Fact]
    public void AssessIntertie_PowerFactor_IsCalculated()
    {
        var result = UtilityIntertieService.AssessIntertie(500, 350, reactivePowerKvar: 50);

        Assert.InRange(result.PowerFactorAtPcc, 0.9, 1.0);
    }

    [Fact]
    public void SizeReversePowerRelay_DefaultPickup_IsFivePercent()
    {
        var result = UtilityIntertieService.SizeReversePowerRelay(1000);

        Assert.Equal(50.0, result.PickupKW);
    }

    [Fact]
    public void SizeReversePowerRelay_HigherPercent_HigherPickup()
    {
        var low = UtilityIntertieService.SizeReversePowerRelay(1000, pickupPercent: 5);
        var high = UtilityIntertieService.SizeReversePowerRelay(1000, pickupPercent: 10);

        Assert.True(high.PickupKW > low.PickupKW);
    }

    [Fact]
    public void SizeReversePowerRelay_TimeDelay_IsPreserved()
    {
        var result = UtilityIntertieService.SizeReversePowerRelay(500, timeDelaySeconds: 1.5);

        Assert.Equal(1.5, result.TimeDelaySeconds);
    }
}