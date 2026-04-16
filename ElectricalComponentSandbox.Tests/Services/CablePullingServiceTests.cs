using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CablePullingServiceTests
{
    [Theory]
    [InlineData(CablePullingService.ConduitMaterial.PVC, 0.35)]
    [InlineData(CablePullingService.ConduitMaterial.SteelRigid, 0.50)]
    [InlineData(CablePullingService.ConduitMaterial.Aluminum, 0.40)]
    [InlineData(CablePullingService.ConduitMaterial.HDPE, 0.35)]
    public void GetFrictionCoefficient_ReturnsMaterialSpecificValue(
        CablePullingService.ConduitMaterial material, double expected)
    {
        Assert.Equal(expected, CablePullingService.GetFrictionCoefficient(material));
    }

    [Theory]
    [InlineData(CablePullingService.CableType.SingleConductor, 600.0)]
    [InlineData(CablePullingService.CableType.TriplexAssembly, 1000.0)]
    [InlineData(CablePullingService.CableType.MulticonductorJacketed, 1500.0)]
    public void GetMaxSidewallPressure_ReturnsCableTypeLimit(
        CablePullingService.CableType type, double expected)
    {
        Assert.Equal(expected, CablePullingService.GetMaxSidewallPressure(type));
    }

    [Fact]
    public void CalculateJamRatio_ThreeCablesInDangerZone_FlagsRisk()
    {
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 3.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 1.0 };

        var result = CablePullingService.CalculateJamRatio(conduit, cable, 3);

        Assert.Equal(3.0, result.Ratio);
        Assert.True(result.IsJamRisk);
        Assert.Contains("danger zone", result.Warning);
    }

    [Fact]
    public void CalculateJamRatio_ThreeCablesOutsideDangerZone_NoRisk()
    {
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 4.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 1.0 };

        var result = CablePullingService.CalculateJamRatio(conduit, cable, 3);

        Assert.Equal(4.0, result.Ratio);
        Assert.False(result.IsJamRisk);
    }

    [Fact]
    public void CalculateJamRatio_SingleCable_NeverJamRisk()
    {
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 3.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 1.0 };

        var result = CablePullingService.CalculateJamRatio(conduit, cable, 1);

        Assert.False(result.IsJamRisk);
    }

    [Fact]
    public void CalculateClearance_SingleCable_Uses53PercentFill()
    {
        // 1" cable in 2" conduit: area = π(0.5²)/π(1²) = 25% fill → passes 53%
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 2.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 1.0 };

        var result = CablePullingService.CalculateClearance(conduit, cable, 1);

        Assert.Equal(25.0, result.FillPercent);
        Assert.True(result.MeetsNecFill);
    }

    [Fact]
    public void CalculateClearance_ThreeCables_Uses40PercentFill()
    {
        // 3 × 0.5" cables in 1" conduit: fill = 3×π(0.25²)/π(0.5²) = 75% → exceeds 40%
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 1.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 0.5 };

        var result = CablePullingService.CalculateClearance(conduit, cable, 3);

        Assert.True(result.FillPercent > 40.0);
        Assert.False(result.MeetsNecFill);
    }

    [Fact]
    public void CalculateClearance_TwoCables_Uses31PercentFill()
    {
        // 2 × 0.5" cables in 2" conduit: fill = 2×π(0.25²)/π(1²) = 12.5% → passes 31%
        var conduit = new CablePullingService.ConduitSpec { InnerDiameterInches = 2.0 };
        var cable = new CablePullingService.CableSpec { OuterDiameterInches = 0.5 };

        var result = CablePullingService.CalculateClearance(conduit, cable, 2);

        Assert.True(result.FillPercent < 31.0);
        Assert.True(result.MeetsNecFill);
    }

    [Fact]
    public void CalculateStraightTension_AccumulatesLinearly()
    {
        // T = 0 + 0.5 × 2.0 × 3 × 200 = 600 lbs
        double result = CablePullingService.CalculateStraightTension(0, 0.5, 2.0, 3, 200);

        Assert.Equal(600.0, result, 1);
    }

    [Fact]
    public void CalculateBendTension_AppliesCapstanEquation()
    {
        // T_out = 100 × e^(0.5 × π/2) ≈ 100 × 2.193 = 219.3
        double result = CablePullingService.CalculateBendTension(100, 0.5, 90.0);

        Assert.True(result > 200.0);
        Assert.True(result < 230.0);
    }

    [Fact]
    public void CalculateBendTension_ZeroAngle_ReturnsTensionUnchanged()
    {
        double result = CablePullingService.CalculateBendTension(500, 0.5, 0);

        Assert.Equal(500.0, result);
    }

    [Fact]
    public void CalculateSidewallPressure_TensionDividedByRadius()
    {
        double result = CablePullingService.CalculateSidewallPressure(1200, 2.0);

        Assert.Equal(600.0, result);
    }

    [Fact]
    public void CalculatePullTension_StraightRun_ReturnsLinearAccumulation()
    {
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 4.0,
            Material = CablePullingService.ConduitMaterial.PVC,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 1.0,
            WeightPerFootLbs = 1.5,
            MaxTensionLbs = 5000,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var sections = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 200, BendAngleDegrees = 0 },
        };

        var result = CablePullingService.CalculatePullTension(conduit, cable, 3, sections);

        // T = 0.35 × 1.5 × 3 × 200 = 315
        Assert.True(result.TotalTensionLbs > 300 && result.TotalTensionLbs < 330);
        Assert.True(result.TensionAcceptable);
        Assert.Equal("Within limits", result.GoverningConstraint);
    }

    [Fact]
    public void CalculatePullTension_WithBend_IncreasesOverStraight()
    {
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 4.0,
            Material = CablePullingService.ConduitMaterial.PVC,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 1.0,
            WeightPerFootLbs = 1.5,
            MaxTensionLbs = 5000,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var straightOnly = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 200, BendAngleDegrees = 0 },
        };
        var withBend = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 100, BendAngleDegrees = 0 },
            new CablePullingService.PullSection { LengthFeet = 0, BendAngleDegrees = 90 },
            new CablePullingService.PullSection { LengthFeet = 100, BendAngleDegrees = 0 },
        };

        var resultStraight = CablePullingService.CalculatePullTension(conduit, cable, 3, straightOnly);
        var resultBend = CablePullingService.CalculatePullTension(conduit, cable, 3, withBend);

        Assert.True(resultBend.TotalTensionLbs > resultStraight.TotalTensionLbs);
    }

    [Fact]
    public void CalculatePullTension_ExceedsMax_FlagsTensionFailure()
    {
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 2.0,
            Material = CablePullingService.ConduitMaterial.SteelRigid,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 0.75,
            WeightPerFootLbs = 3.0,
            MaxTensionLbs = 200,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var sections = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 500, BendAngleDegrees = 0 },
            new CablePullingService.PullSection { LengthFeet = 0, BendAngleDegrees = 90 },
            new CablePullingService.PullSection { LengthFeet = 300, BendAngleDegrees = 0 },
        };

        var result = CablePullingService.CalculatePullTension(conduit, cable, 3, sections);

        Assert.False(result.TensionAcceptable);
        Assert.Contains("tension", result.GoverningConstraint, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzePull_AllGood_OverallAcceptable()
    {
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 4.0,
            Material = CablePullingService.ConduitMaterial.PVC,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 0.75,
            WeightPerFootLbs = 0.5,
            MaxTensionLbs = 5000,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var sections = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 100, BendAngleDegrees = 0 },
        };

        var result = CablePullingService.AnalyzePull(conduit, cable, 3, sections);

        Assert.True(result.OverallAcceptable);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AnalyzePull_JamRisk_AddsWarningAndFails()
    {
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 3.0,
            Material = CablePullingService.ConduitMaterial.PVC,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 1.0,
            WeightPerFootLbs = 0.5,
            MaxTensionLbs = 5000,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var sections = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 50, BendAngleDegrees = 0 },
        };

        var result = CablePullingService.AnalyzePull(conduit, cable, 3, sections);

        Assert.False(result.OverallAcceptable);
        Assert.True(result.JamRatio.IsJamRisk);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void AnalyzePull_FillExceeded_AddsWarningAndFails()
    {
        // 3 × 1" cables in 1.5" conduit → very high fill
        var conduit = new CablePullingService.ConduitSpec
        {
            InnerDiameterInches = 1.5,
            Material = CablePullingService.ConduitMaterial.PVC,
        };
        var cable = new CablePullingService.CableSpec
        {
            OuterDiameterInches = 0.8,
            WeightPerFootLbs = 0.5,
            MaxTensionLbs = 5000,
            Type = CablePullingService.CableType.SingleConductor,
        };
        var sections = new[]
        {
            new CablePullingService.PullSection { LengthFeet = 50, BendAngleDegrees = 0 },
        };

        var result = CablePullingService.AnalyzePull(conduit, cable, 3, sections);

        Assert.False(result.OverallAcceptable);
        Assert.False(result.Clearance.MeetsNecFill);
        Assert.Contains(result.Warnings, w => w.Contains("fill"));
    }
}
