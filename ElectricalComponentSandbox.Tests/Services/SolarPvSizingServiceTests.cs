using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SolarPvSizingServiceTests
{
    private static SolarPvSizingService.ModuleSpec DefaultModule() => new()
    {
        WattsSTC = 400,
        VocVolts = 49.5,
        VmpVolts = 41.7,
        IscAmps = 10.4,
        ImpAmps = 9.6,
        TempCoeffVocPerC = -0.003,
        TempCoeffIscPerC = 0.0005,
    };

    private static SolarPvSizingService.InverterSpec DefaultInverter() => new()
    {
        MaxDcVoltage = 600,
        MpptMinVoltage = 300,
        MpptMaxVoltage = 500,
        MaxDcCurrentAmps = 32,
        RatedAcWatts = 10000,
        MaxDcWatts = 13000,
        Type = SolarPvSizingService.InverterType.StringInverter,
    };

    // ── Temperature Correction ───────────────────────────────────────────────

    [Fact]
    public void VocCorrection_ColdTemp_HigherVoltage()
    {
        double corrected = SolarPvSizingService.CorrectVocForTemp(49.5, -0.003, -10);
        Assert.True(corrected > 49.5); // Cold → higher Voc
    }

    [Fact]
    public void VocCorrection_HotTemp_LowerVoltage()
    {
        double corrected = SolarPvSizingService.CorrectVocForTemp(49.5, -0.003, 45);
        Assert.True(corrected < 49.5); // Hot → lower Voc
    }

    [Fact]
    public void VmpCorrection_HotCell_LowerVoltage()
    {
        double corrected = SolarPvSizingService.CorrectVmpForTemp(41.7, -0.003, 65);
        Assert.True(corrected < 41.7);
    }

    [Fact]
    public void IscCorrection_Includes125Factor()
    {
        double corrected = SolarPvSizingService.CorrectIscForTemp(10.4, 0.0005, 25);
        // At STC temp, correction factor = 1.0, but × 1.25 per NEC
        Assert.Equal(10.4 * 1.25, corrected, 2);
    }

    [Fact]
    public void IscCorrection_HotTemp_Higher()
    {
        double stc = SolarPvSizingService.CorrectIscForTemp(10.4, 0.0005, 25);
        double hot = SolarPvSizingService.CorrectIscForTemp(10.4, 0.0005, 65);
        Assert.True(hot > stc);
    }

    // ── String Sizing ────────────────────────────────────────────────────────

    [Fact]
    public void StringSizing_ValidRange()
    {
        var result = SolarPvSizingService.SizeString(DefaultModule(), DefaultInverter());
        Assert.True(result.IsValid);
        Assert.True(result.MinModulesPerString > 0);
        Assert.True(result.MaxModulesPerString >= result.MinModulesPerString);
        Assert.True(result.RecommendedModulesPerString >= result.MinModulesPerString);
        Assert.True(result.RecommendedModulesPerString <= result.MaxModulesPerString);
    }

    [Fact]
    public void StringSizing_MaxVocWithinInverterLimit()
    {
        var mod = DefaultModule();
        var inv = DefaultInverter();
        var result = SolarPvSizingService.SizeString(mod, inv);
        double maxStringVoc = result.CorrectedVocMax * result.MaxModulesPerString;
        Assert.True(maxStringVoc <= inv.MaxDcVoltage);
    }

    [Fact]
    public void StringSizing_NarrowInverter_MayBeInvalid()
    {
        var inv = new SolarPvSizingService.InverterSpec
        {
            MaxDcVoltage = 100, MpptMinVoltage = 95, MpptMaxVoltage = 100,
            MaxDcCurrentAmps = 32, RatedAcWatts = 5000, MaxDcWatts = 6000,
        };
        var result = SolarPvSizingService.SizeString(DefaultModule(), inv);
        Assert.False(result.IsValid);
    }

    // ── System Sizing ────────────────────────────────────────────────────────

    [Fact]
    public void SystemSizing_MeetsTarget()
    {
        var result = SolarPvSizingService.SizeSystem(10, DefaultModule(), DefaultInverter());
        Assert.True(result.SystemDcKw >= 10); // Should meet or exceed
        Assert.True(result.TotalModules > 0);
        Assert.True(result.StringsCount > 0);
    }

    [Fact]
    public void SystemSizing_AnnualProduction_Positive()
    {
        var result = SolarPvSizingService.SizeSystem(10, DefaultModule(), DefaultInverter());
        Assert.True(result.AnnualProductionKwh > 0);
    }

    [Fact]
    public void SystemSizing_ConductorMinAmps_Positive()
    {
        var result = SolarPvSizingService.SizeSystem(10, DefaultModule(), DefaultInverter());
        Assert.True(result.ConductorMinAmps > 0);
    }

    [Fact]
    public void SystemSizing_FullStrings()
    {
        var result = SolarPvSizingService.SizeSystem(10, DefaultModule(), DefaultInverter());
        Assert.Equal(result.StringsCount * result.ModulesPerString, result.TotalModules);
    }

    // ── DC/AC Ratio ──────────────────────────────────────────────────────────

    [Fact]
    public void DcAcRatio_UnderSized()
    {
        var (ok, _) = SolarPvSizingService.EvaluateDcAcRatio(0.7);
        Assert.False(ok);
    }

    [Fact]
    public void DcAcRatio_Optimal()
    {
        var (ok, assessment) = SolarPvSizingService.EvaluateDcAcRatio(1.25);
        Assert.True(ok);
        Assert.Contains("Optimal", assessment);
    }

    [Fact]
    public void DcAcRatio_Oversized()
    {
        var (ok, _) = SolarPvSizingService.EvaluateDcAcRatio(1.8);
        Assert.False(ok);
    }
}
