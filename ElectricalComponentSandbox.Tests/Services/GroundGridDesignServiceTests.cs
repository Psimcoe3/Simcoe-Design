using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class GroundGridDesignServiceTests
{
    private static GroundGridDesignService.GridInput DefaultInput() => new()
    {
        LengthM = 30,
        WidthM = 20,
        SoilResistivityOhmM = 100,
        FaultCurrentAmps = 10000,
        FaultDurationSec = 0.5,
        GridDepthM = 0.5,
        SurfaceLayerResistivityOhmM = 3000,
        SurfaceLayerThicknessM = 0.1,
        Material = GroundGridDesignService.ConductorMaterial.CopperSolid,
    };

    // ── Soil Resistivity ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(GroundGridDesignService.SoilType.Wet, 30)]
    [InlineData(GroundGridDesignService.SoilType.Moist, 100)]
    [InlineData(GroundGridDesignService.SoilType.Dry, 1000)]
    [InlineData(GroundGridDesignService.SoilType.Rocky, 3000)]
    public void GetTypicalResistivity_ReturnsExpected(GroundGridDesignService.SoilType type, double expected)
    {
        Assert.Equal(expected, GroundGridDesignService.GetTypicalResistivity(type));
    }

    // ── Grid Resistance ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateGridResistance_ReturnsPositive()
    {
        var input = DefaultInput();
        var result = GroundGridDesignService.CalculateGridResistance(input, 300);

        Assert.True(result.GridResistanceOhms > 0);
        Assert.Equal(600, result.AreaM2);
    }

    [Fact]
    public void CalculateGridResistance_GPR_EqualsIfTimesR()
    {
        var input = DefaultInput();
        var result = GroundGridDesignService.CalculateGridResistance(input, 300);

        double expectedGPR = input.FaultCurrentAmps * result.GridResistanceOhms;
        Assert.Equal(expectedGPR, result.GroundPotentialRiseV, 0);
    }

    [Fact]
    public void CalculateGridResistance_MoreConductor_LowerResistance()
    {
        var input = DefaultInput();
        var r1 = GroundGridDesignService.CalculateGridResistance(input, 200);
        var r2 = GroundGridDesignService.CalculateGridResistance(input, 600);

        Assert.True(r2.GridResistanceOhms < r1.GridResistanceOhms);
    }

    // ── Conductor Sizing ─────────────────────────────────────────────────────

    [Fact]
    public void SizeConductor_10kA_HalfSecond_ReturnsReasonableSize()
    {
        var result = GroundGridDesignService.SizeConductor(10000, 0.5);

        Assert.True(result.MinCrossSectionMm2 > 0);
        Assert.False(string.IsNullOrEmpty(result.RecommendedSize));
    }

    [Fact]
    public void SizeConductor_HigherFault_LargerConductor()
    {
        var r1 = GroundGridDesignService.SizeConductor(5000, 0.5);
        var r2 = GroundGridDesignService.SizeConductor(20000, 0.5);

        Assert.True(r2.MinCrossSectionMm2 > r1.MinCrossSectionMm2);
    }

    [Fact]
    public void SizeConductor_LongerDuration_LargerConductor()
    {
        var r1 = GroundGridDesignService.SizeConductor(10000, 0.25);
        var r2 = GroundGridDesignService.SizeConductor(10000, 1.0);

        Assert.True(r2.MinCrossSectionMm2 > r1.MinCrossSectionMm2);
    }

    // ── Touch & Step Voltage ─────────────────────────────────────────────────

    [Fact]
    public void EvaluateTouchStep_TolerableStepExceedsTolerabbleTouch()
    {
        var input = DefaultInput();
        var result = GroundGridDesignService.EvaluateTouchStep(input, 300, 6, 5000);

        // Step is always more tolerable than touch (larger body resistance path)
        Assert.True(result.TolerableStepVoltageV > result.TolerableTouchVoltageV);
    }

    [Fact]
    public void EvaluateTouchStep_WithCrushedRock_IncreasesTolerable()
    {
        var withRock = DefaultInput();
        var noRock = DefaultInput() with { SurfaceLayerResistivityOhmM = 100 };

        var r1 = GroundGridDesignService.EvaluateTouchStep(withRock, 300, 6, 5000);
        var r2 = GroundGridDesignService.EvaluateTouchStep(noRock, 300, 6, 5000);

        Assert.True(r1.TolerableTouchVoltageV > r2.TolerableTouchVoltageV);
    }

    [Fact]
    public void EvaluateTouchStep_DenseGrid_LowerActualVoltages()
    {
        var input = DefaultInput();
        var sparse = GroundGridDesignService.EvaluateTouchStep(input, 200, 4, 5000);
        var dense = GroundGridDesignService.EvaluateTouchStep(input, 600, 10, 5000);

        Assert.True(dense.ActualTouchVoltageV < sparse.ActualTouchVoltageV);
    }

    // ── Full Grid Design ─────────────────────────────────────────────────────

    [Fact]
    public void DesignGrid_ReturnsPositiveValues()
    {
        var result = GroundGridDesignService.DesignGrid(DefaultInput());

        Assert.Equal(600, result.AreaM2);
        Assert.True(result.TotalConductorLengthM > 0);
        Assert.True(result.ConductorsAlongLength >= 2);
        Assert.True(result.ConductorsAlongWidth >= 2);
        Assert.True(result.GroundRods >= 4);
        Assert.True(result.SpacingM > 0);
    }

    [Fact]
    public void DesignGrid_LowResistivitySoil_LowerResistance()
    {
        var lowR = DefaultInput() with { SoilResistivityOhmM = 50 };
        var highR = DefaultInput() with { SoilResistivityOhmM = 500 };

        var r1 = GroundGridDesignService.DesignGrid(lowR);
        var r2 = GroundGridDesignService.DesignGrid(highR);

        Assert.True(r1.GridResistanceOhms < r2.GridResistanceOhms);
    }

    [Fact]
    public void DesignGrid_HighResistivity_LongerRods()
    {
        var highR = DefaultInput() with { SoilResistivityOhmM = 1000 };
        var lowR = DefaultInput() with { SoilResistivityOhmM = 50 };

        var r1 = GroundGridDesignService.DesignGrid(highR);
        var r2 = GroundGridDesignService.DesignGrid(lowR);

        Assert.True(r1.RodLengthM >= r2.RodLengthM);
    }

    [Fact]
    public void DesignGrid_LargerArea_MoreConductors()
    {
        var small = DefaultInput() with { LengthM = 10, WidthM = 10 };
        var large = DefaultInput() with { LengthM = 50, WidthM = 40 };

        var r1 = GroundGridDesignService.DesignGrid(small);
        var r2 = GroundGridDesignService.DesignGrid(large);

        Assert.True(r2.TotalConductorLengthM > r1.TotalConductorLengthM);
    }
}
