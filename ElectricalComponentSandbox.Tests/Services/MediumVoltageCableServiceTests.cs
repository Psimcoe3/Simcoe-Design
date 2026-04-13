using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MediumVoltageCableServiceTests
{
    private static MediumVoltageCableService.MvCableSpec DefaultSpec() => new()
    {
        VoltageClass = MediumVoltageCableService.VoltageClass.V15kV,
        Level = MediumVoltageCableService.InsulationLevel.Level100,
        Insulation = MediumVoltageCableService.InsulationType.XLPE,
        Material = MediumVoltageCableService.ConductorMaterial.Copper,
        SystemVoltageKV = 13.8,
        Phases = 3,
    };

    // ── Insulation Spec ──────────────────────────────────────────────────────

    [Fact]
    public void GetInsulationSpec_15kV_Level100_CorrectValues()
    {
        var spec = MediumVoltageCableService.GetInsulationSpec(
            MediumVoltageCableService.VoltageClass.V15kV,
            MediumVoltageCableService.InsulationLevel.Level100);

        Assert.Equal(175, spec.InsulationThicknessMils);
        Assert.Equal(110, spec.BasicInsulationLevelKV);
        Assert.Equal(36, spec.AcWithstandKV);
    }

    [Fact]
    public void GetInsulationSpec_Level133_ThickerInsulation()
    {
        var l100 = MediumVoltageCableService.GetInsulationSpec(
            MediumVoltageCableService.VoltageClass.V15kV,
            MediumVoltageCableService.InsulationLevel.Level100);
        var l133 = MediumVoltageCableService.GetInsulationSpec(
            MediumVoltageCableService.VoltageClass.V15kV,
            MediumVoltageCableService.InsulationLevel.Level133);

        Assert.True(l133.InsulationThicknessMils > l100.InsulationThicknessMils);
    }

    [Fact]
    public void GetInsulationSpec_HigherVoltage_HigherBIL()
    {
        var v5 = MediumVoltageCableService.GetInsulationSpec(
            MediumVoltageCableService.VoltageClass.V5kV,
            MediumVoltageCableService.InsulationLevel.Level100);
        var v35 = MediumVoltageCableService.GetInsulationSpec(
            MediumVoltageCableService.VoltageClass.V35kV,
            MediumVoltageCableService.InsulationLevel.Level100);

        Assert.True(v35.BasicInsulationLevelKV > v5.BasicInsulationLevelKV);
    }

    // ── Ampacity Selection ───────────────────────────────────────────────────

    [Fact]
    public void SelectByAmpacity_200A_ReturnsAdequateSize()
    {
        var result = MediumVoltageCableService.SelectByAmpacity(200, DefaultSpec());

        Assert.True(result.IsAdequate);
        Assert.True(result.AmpacityAmps >= 200);
        Assert.Equal("2/0", result.CableSize);
    }

    [Fact]
    public void SelectByAmpacity_Aluminum_LargerSizeNeeded()
    {
        var alSpec = DefaultSpec() with { Material = MediumVoltageCableService.ConductorMaterial.Aluminum };

        var cuResult = MediumVoltageCableService.SelectByAmpacity(200, DefaultSpec());
        var alResult = MediumVoltageCableService.SelectByAmpacity(200, alSpec);

        // Aluminum needs larger conductor for same ampacity
        Assert.True(GetSizeIndex(alResult.CableSize) >= GetSizeIndex(cuResult.CableSize));
    }

    [Fact]
    public void SelectByAmpacity_ExceedsTable_NotAdequate()
    {
        var result = MediumVoltageCableService.SelectByAmpacity(999, DefaultSpec());

        Assert.False(result.IsAdequate);
        Assert.Equal("1000", result.CableSize);
    }

    [Fact]
    public void SelectByAmpacity_UtilizationPercent_InRange()
    {
        var result = MediumVoltageCableService.SelectByAmpacity(300, DefaultSpec());

        Assert.True(result.UtilizationPercent > 0 && result.UtilizationPercent <= 100);
    }

    // ── Short-Circuit Withstand ──────────────────────────────────────────────

    [Fact]
    public void CheckShortCircuitWithstand_AdequateSize_IsAdequate()
    {
        var result = MediumVoltageCableService.CheckShortCircuitWithstand(
            "500", 10000, 0.5);

        Assert.True(result.IsAdequate);
        Assert.True(result.WithstandAmps > 10000);
    }

    [Fact]
    public void CheckShortCircuitWithstand_LongerDuration_LowerWithstand()
    {
        var r1 = MediumVoltageCableService.CheckShortCircuitWithstand("350", 10000, 0.25);
        var r2 = MediumVoltageCableService.CheckShortCircuitWithstand("350", 10000, 1.0);

        Assert.True(r1.WithstandAmps > r2.WithstandAmps);
    }

    [Fact]
    public void CheckShortCircuitWithstand_Aluminum_LowerWithstand()
    {
        var cu = MediumVoltageCableService.CheckShortCircuitWithstand(
            "500", 10000, 0.5, MediumVoltageCableService.ConductorMaterial.Copper);
        var al = MediumVoltageCableService.CheckShortCircuitWithstand(
            "500", 10000, 0.5, MediumVoltageCableService.ConductorMaterial.Aluminum);

        Assert.True(cu.WithstandAmps > al.WithstandAmps);
    }

    // ── Complete Cable Sizing ────────────────────────────────────────────────

    [Fact]
    public void SizeCable_NormalLoad_MeetsAllCriteria()
    {
        var result = MediumVoltageCableService.SizeCable(
            250, 10000, 0.5, DefaultSpec());

        Assert.True(result.MeetsAllCriteria);
        Assert.False(string.IsNullOrEmpty(result.RecommendedSize));
        Assert.True(result.Ampacity >= 250);
        Assert.NotNull(result.Insulation);
    }

    [Fact]
    public void SizeCable_HighFault_MayUpsize()
    {
        var lowFault = MediumVoltageCableService.SizeCable(180, 5000, 0.5, DefaultSpec());
        var highFault = MediumVoltageCableService.SizeCable(180, 25000, 0.5, DefaultSpec());

        Assert.True(GetSizeIndex(highFault.RecommendedSize) >= GetSizeIndex(lowFault.RecommendedSize));
    }

    [Fact]
    public void SizeCable_IncludesInsulationSpec()
    {
        var result = MediumVoltageCableService.SizeCable(200, 10000, 0.5, DefaultSpec());

        Assert.Equal(MediumVoltageCableService.VoltageClass.V15kV, result.Insulation.VoltageClass);
        Assert.True(result.Insulation.BasicInsulationLevelKV > 0);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static int GetSizeIndex(string size)
    {
        string[] order = { "1/0", "2/0", "3/0", "4/0", "250", "350", "500", "750", "1000" };
        return System.Array.IndexOf(order, size);
    }
}
