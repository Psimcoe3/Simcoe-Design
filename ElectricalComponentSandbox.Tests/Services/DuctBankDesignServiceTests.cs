using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DuctBankDesignServiceTests
{
    [Theory]
    [InlineData(DuctBankDesignService.SoilType.Concrete, 55.0)]
    [InlineData(DuctBankDesignService.SoilType.Sand, 120.0)]
    [InlineData(DuctBankDesignService.SoilType.Clay, 90.0)]
    [InlineData(DuctBankDesignService.SoilType.Loam, 80.0)]
    public void GetTypicalRhoK_ReturnsSoilSpecificValue(DuctBankDesignService.SoilType soil, double expected)
    {
        Assert.Equal(expected, DuctBankDesignService.GetTypicalRhoK(soil));
    }

    [Theory]
    [InlineData(DuctBankDesignService.DuctMaterial.PVC, 650.0)]
    [InlineData(DuctBankDesignService.DuctMaterial.SteelRigid, 50.0)]
    public void GetDuctThermalResistivity_ReturnsMaterialSpecificValue(
        DuctBankDesignService.DuctMaterial material, double expected)
    {
        Assert.Equal(expected, DuctBankDesignService.GetDuctThermalResistivity(material));
    }

    [Fact]
    public void GetMinBurialDepth_PvcUnder600V_Returns18()
    {
        double result = DuctBankDesignService.GetMinBurialDepthInches(
            DuctBankDesignService.DuctMaterial.PVC, 480);
        Assert.Equal(18.0, result);
    }

    [Fact]
    public void GetMinBurialDepth_SteelRigidUnder600V_Returns6()
    {
        double result = DuctBankDesignService.GetMinBurialDepthInches(
            DuctBankDesignService.DuctMaterial.SteelRigid, 480);
        Assert.Equal(6.0, result);
    }

    [Fact]
    public void GetMinBurialDepth_Over600V_Returns24()
    {
        double result = DuctBankDesignService.GetMinBurialDepthInches(
            DuctBankDesignService.DuctMaterial.PVC, 15000);
        Assert.Equal(24.0, result);
    }

    [Fact]
    public void CalculateMutualHeatingFactor_SingleDuct_ReturnsOne()
    {
        var bank = new DuctBankDesignService.BankGeometry { Rows = 1, Columns = 1 };
        double result = DuctBankDesignService.CalculateMutualHeatingFactor(bank, 1);
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void CalculateMutualHeatingFactor_MultipleOccupied_GreaterThanOne()
    {
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 3, Columns = 3, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };
        double result = DuctBankDesignService.CalculateMutualHeatingFactor(bank, 6);
        Assert.True(result > 1.0);
    }

    [Fact]
    public void CalculateMutualHeatingFactor_TighterSpacing_IncreasesHeating()
    {
        var bankWide = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 3, CenterSpacingInches = 10.0, BurialDepthInches = 30,
        };
        var bankTight = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 3, CenterSpacingInches = 5.0, BurialDepthInches = 30,
        };
        double wide = DuctBankDesignService.CalculateMutualHeatingFactor(bankWide, 4);
        double tight = DuctBankDesignService.CalculateMutualHeatingFactor(bankTight, 4);
        Assert.True(tight > wide);
    }

    [Fact]
    public void CalculateDeratingFactor_StandardConditions_NearUnity()
    {
        var env = new DuctBankDesignService.ThermalEnvironment
        {
            Soil = DuctBankDesignService.SoilType.Clay,
            AmbientTempC = 20,
            SoilThermalResistivityRhoK = 90,
            LoadFactorPercent = 100,
        };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4.0, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 1, Columns = 1, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };

        double result = DuctBankDesignService.CalculateDeratingFactor(env, duct, bank, 1);

        // Single duct, reference conditions → derating near 0.85-1.0 (PVC wall penalty applies)
        Assert.True(result >= 0.75 && result <= 1.0);
    }

    [Fact]
    public void CalculateDeratingFactor_HighRhoK_LowerDerating()
    {
        var envLow = new DuctBankDesignService.ThermalEnvironment
        {
            SoilThermalResistivityRhoK = 60, AmbientTempC = 20, LoadFactorPercent = 100,
        };
        var envHigh = new DuctBankDesignService.ThermalEnvironment
        {
            SoilThermalResistivityRhoK = 200, AmbientTempC = 20, LoadFactorPercent = 100,
        };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry { Rows = 1, Columns = 1 };

        double low = DuctBankDesignService.CalculateDeratingFactor(envLow, duct, bank, 1);
        double high = DuctBankDesignService.CalculateDeratingFactor(envHigh, duct, bank, 1);

        Assert.True(high < low);
    }

    [Fact]
    public void CalculateDeratedAmpacity_ReducesBaseAmpacity()
    {
        var env = new DuctBankDesignService.ThermalEnvironment
        {
            SoilThermalResistivityRhoK = 120, AmbientTempC = 25, LoadFactorPercent = 100,
        };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 3, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };

        var result = DuctBankDesignService.CalculateDeratedAmpacity(400, env, duct, bank, 4);

        Assert.True(result.AdjustedAmpacity < 400);
        Assert.True(result.AdjustedAmpacity > 100);
        Assert.True(result.DeratingFactor < 1.0);
        Assert.True(result.MutualHeatingFactor > 1.0);
    }

    [Fact]
    public void CalculateDeratedAmpacity_PartialLoadFactor_ImprovesFactor()
    {
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry { Rows = 2, Columns = 2 };

        var envFull = new DuctBankDesignService.ThermalEnvironment
        {
            SoilThermalResistivityRhoK = 90, AmbientTempC = 20, LoadFactorPercent = 100,
        };
        var envPartial = new DuctBankDesignService.ThermalEnvironment
        {
            SoilThermalResistivityRhoK = 90, AmbientTempC = 20, LoadFactorPercent = 50,
        };

        var full = DuctBankDesignService.CalculateDeratedAmpacity(400, envFull, duct, bank, 4);
        var partial = DuctBankDesignService.CalculateDeratedAmpacity(400, envPartial, duct, bank, 4);

        Assert.True(partial.AdjustedAmpacity > full.AdjustedAmpacity);
    }

    [Fact]
    public void EvaluateBank_ShallowBurial_FlagsMeetsDepthFalse()
    {
        var env = new DuctBankDesignService.ThermalEnvironment { SoilThermalResistivityRhoK = 90 };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 3, CenterSpacingInches = 7.5, BurialDepthInches = 12,
        };

        var result = DuctBankDesignService.EvaluateBank(env, duct, bank, 4, 480);

        Assert.False(result.MeetsNecDepth);
        Assert.Contains(result.Recommendations, r => r.Contains("Burial depth"));
    }

    [Fact]
    public void EvaluateBank_NoSpares_RecommendsAdding()
    {
        var env = new DuctBankDesignService.ThermalEnvironment { SoilThermalResistivityRhoK = 90 };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 1, Columns = 4, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };

        var result = DuctBankDesignService.EvaluateBank(env, duct, bank, 4, 480);

        Assert.Equal(0, result.SpareDucts);
        Assert.Contains(result.Recommendations, r => r.Contains("spare"));
    }

    [Fact]
    public void EvaluateBank_AdequateDesign_NoDepthWarning()
    {
        var env = new DuctBankDesignService.ThermalEnvironment { SoilThermalResistivityRhoK = 90 };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 3, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };

        var result = DuctBankDesignService.EvaluateBank(env, duct, bank, 3, 480);

        Assert.True(result.MeetsNecDepth);
        Assert.Equal(3, result.SpareDucts);
        Assert.Equal(50.0, result.SparePercent);
    }

    [Fact]
    public void EvaluateBank_HighRhoK_RecommendsThermalBackfill()
    {
        var env = new DuctBankDesignService.ThermalEnvironment { SoilThermalResistivityRhoK = 150 };
        var duct = new DuctBankDesignService.DuctSpec
        {
            InnerDiameterInches = 4, OuterDiameterInches = 4.5,
            Material = DuctBankDesignService.DuctMaterial.PVC,
        };
        var bank = new DuctBankDesignService.BankGeometry
        {
            Rows = 2, Columns = 2, CenterSpacingInches = 7.5, BurialDepthInches = 30,
        };

        var result = DuctBankDesignService.EvaluateBank(env, duct, bank, 2, 480);

        Assert.Contains(result.Recommendations, r => r.Contains("thermal backfill"));
    }
}
