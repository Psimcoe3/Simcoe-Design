using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class LightingCalculationServiceTests
{
    // ── Cavity Ratios ────────────────────────────────────────────────────────

    [Fact]
    public void CavityRatios_TypicalOffice()
    {
        var room = new LightingCalculationService.RoomGeometry
        {
            LengthFeet = 30, WidthFeet = 20,
            CeilingHeightFeet = 9, LuminaireMountHeightFeet = 8.5, WorkPlaneHeightFeet = 2.5,
        };
        var cr = LightingCalculationService.CalculateCavityRatios(room);
        // RCR = 5 * (8.5-2.5) * 2*(30+20) / (30*20) = 5 * 6 * 100 / 600 = 5.0
        Assert.Equal(5.0, cr.RoomCavityRatio, 1);
        Assert.True(cr.CeilingCavityRatio >= 0);
        Assert.True(cr.FloorCavityRatio >= 0);
    }

    [Fact]
    public void CavityRatios_ZeroArea_ReturnsZeros()
    {
        var room = new LightingCalculationService.RoomGeometry { LengthFeet = 0, WidthFeet = 10 };
        var cr = LightingCalculationService.CalculateCavityRatios(room);
        Assert.Equal(0, cr.RoomCavityRatio);
    }

    [Fact]
    public void CavityRatios_LargeRoom_LowRCR()
    {
        var room = new LightingCalculationService.RoomGeometry
        {
            LengthFeet = 100, WidthFeet = 100,
            CeilingHeightFeet = 10, LuminaireMountHeightFeet = 9.5, WorkPlaneHeightFeet = 2.5,
        };
        var cr = LightingCalculationService.CalculateCavityRatios(room);
        // Large rooms have low RCR
        Assert.True(cr.RoomCavityRatio < 3.0);
    }

    // ── Zonal Cavity ─────────────────────────────────────────────────────────

    private static LightingCalculationService.RoomGeometry StandardOffice => new()
    {
        LengthFeet = 30, WidthFeet = 20,
        CeilingHeightFeet = 9, LuminaireMountHeightFeet = 8.5, WorkPlaneHeightFeet = 2.5,
    };

    private static LightingCalculationService.LuminaireData StandardLed => new()
    {
        Model = "LED-2x4-40W",
        InitialLumens = 4000,
        Watts = 40,
        CoefficientOfUtilization = 0.65,
        SpacingToMountHeightRatio = 1.2,
    };

    [Fact]
    public void ZonalCavity_StandardOffice_500Lux()
    {
        var result = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        Assert.True(result.RecommendedFixtureCount > 0);
        Assert.True(result.MaintainedIlluminanceLux >= 500);
    }

    [Fact]
    public void ZonalCavity_HighTarget_MoreFixtures()
    {
        var low = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 300);
        var high = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 750);
        Assert.True(high.RecommendedFixtureCount >= low.RecommendedFixtureCount);
    }

    [Fact]
    public void ZonalCavity_WattsPerSqFt_Reasonable()
    {
        var result = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        // Typical LED office: 0.5-2.0 W/ft²
        Assert.InRange(result.WattsPerSquareFoot, 0.3, 3.0);
    }

    [Fact]
    public void ZonalCavity_TotalWatts_MatchesFixtures()
    {
        var result = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        Assert.Equal(result.RecommendedFixtureCount * StandardLed.Watts, result.TotalWatts);
    }

    [Fact]
    public void ZonalCavity_WithDirtyLLF_MoreFixtures()
    {
        var clean = new LightingCalculationService.LightLossFactors
        {
            LampLumenDepreciation = 0.95, LuminaireDirtDepreciation = 0.95,
        };
        var dirty = new LightingCalculationService.LightLossFactors
        {
            LampLumenDepreciation = 0.80, LuminaireDirtDepreciation = 0.70,
        };
        var resultClean = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500, clean);
        var resultDirty = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500, dirty);
        Assert.True(resultDirty.RecommendedFixtureCount >= resultClean.RecommendedFixtureCount);
    }

    [Fact]
    public void ZonalCavity_InvalidRoom_ReturnsNote()
    {
        var room = new LightingCalculationService.RoomGeometry { LengthFeet = 0, WidthFeet = 0 };
        var result = LightingCalculationService.CalculateZonalCavity(room, StandardLed, 500);
        Assert.NotNull(result.Note);
    }

    [Fact]
    public void ZonalCavity_SpacingCompliance()
    {
        var result = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        Assert.True(result.MaxSpacingFeet > 0);
    }

    [Fact]
    public void ZonalCavity_FCAndLux_Consistent()
    {
        var result = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        double expectedFC = result.MaintainedIlluminanceLux / 10.764;
        Assert.Equal(expectedFC, result.MaintainedIlluminanceFC, 0);
    }

    // ── Minimum Fixtures ─────────────────────────────────────────────────────

    [Fact]
    public void MinimumFixtures_MatchesZonalCavity()
    {
        int min = LightingCalculationService.CalculateMinimumFixtures(
            StandardOffice, StandardLed, 500);
        var full = LightingCalculationService.CalculateZonalCavity(
            StandardOffice, StandardLed, 500);
        Assert.Equal(full.RecommendedFixtureCount, min);
    }

    // ── Point-by-Point ───────────────────────────────────────────────────────

    [Fact]
    public void PointIlluminance_DirectlyBelow_Maximum()
    {
        double directly = LightingCalculationService.CalculatePointIlluminance(
            10, 10, 6, 10, 10, 5000);
        double offset = LightingCalculationService.CalculatePointIlluminance(
            10, 10, 6, 15, 10, 5000);
        Assert.True(directly > offset);
    }

    [Fact]
    public void PointIlluminance_InverseSquareFalloff()
    {
        double close = LightingCalculationService.CalculatePointIlluminance(
            0, 0, 4, 0, 0, 1000);
        double far = LightingCalculationService.CalculatePointIlluminance(
            0, 0, 8, 0, 0, 1000);
        // At double height, illuminance ≈ 1/4
        Assert.True(far < close * 0.4);
    }

    [Fact]
    public void IlluminanceGrid_ReturnsCorrectPointCount()
    {
        var fixtures = new List<(double X, double Y)> { (15, 10) };
        var grid = LightingCalculationService.CalculateIlluminanceGrid(
            StandardOffice, fixtures, 3000, gridRows: 4, gridCols: 6);
        Assert.Equal(24, grid.Count);
    }

    [Fact]
    public void IlluminanceGrid_AllPositive()
    {
        var fixtures = new List<(double X, double Y)> { (10, 10), (20, 10) };
        var grid = LightingCalculationService.CalculateIlluminanceGrid(
            StandardOffice, fixtures, 3000);
        Assert.All(grid, p => Assert.True(p.IlluminanceLux > 0));
    }

    [Fact]
    public void IlluminanceGrid_MultipleFixtures_HigherThanSingle()
    {
        var single = LightingCalculationService.CalculateIlluminanceGrid(
            StandardOffice, new List<(double, double)> { (15, 10) }, 3000, 3, 3);
        var dual = LightingCalculationService.CalculateIlluminanceGrid(
            StandardOffice, new List<(double, double)> { (10, 10), (20, 10) }, 3000, 3, 3);
        double avgSingle = single.Average(p => p.IlluminanceLux);
        double avgDual = dual.Average(p => p.IlluminanceLux);
        Assert.True(avgDual > avgSingle);
    }

    // ── Compliance ───────────────────────────────────────────────────────────

    [Fact]
    public void Compliance_Office_500Lux_Passes()
    {
        var result = LightingCalculationService.CheckCompliance(
            LightingCalculationService.OccupancyType.Office, 500);
        Assert.True(result.MeetsMinimum);
        Assert.False(result.ExceedsMaximum);
    }

    [Fact]
    public void Compliance_Office_200Lux_FailsMinimum()
    {
        var result = LightingCalculationService.CheckCompliance(
            LightingCalculationService.OccupancyType.Office, 200);
        Assert.False(result.MeetsMinimum);
    }

    [Fact]
    public void Compliance_Corridor_50Lux_MeetsMinimum()
    {
        var result = LightingCalculationService.CheckCompliance(
            LightingCalculationService.OccupancyType.Corridor, 50);
        Assert.True(result.MeetsMinimum);
    }

    [Fact]
    public void Compliance_Office_1000Lux_ExceedsMax()
    {
        var result = LightingCalculationService.CheckCompliance(
            LightingCalculationService.OccupancyType.Office, 1000);
        Assert.True(result.ExceedsMaximum);
    }

    [Fact]
    public void GetRecommendedIlluminance_AllOccupancies()
    {
        foreach (var occ in Enum.GetValues<LightingCalculationService.OccupancyType>())
        {
            var (min, target, max) = LightingCalculationService.GetRecommendedIlluminance(occ);
            Assert.True(min > 0);
            Assert.True(target >= min);
            Assert.True(max >= target);
        }
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    [Fact]
    public void FootCandlesToLux_Conversion()
    {
        double lux = LightingCalculationService.FootCandlesToLux(50);
        Assert.Equal(538.2, lux, 0);
    }

    [Fact]
    public void LuxToFootCandles_Conversion()
    {
        double fc = LightingCalculationService.LuxToFootCandles(500);
        Assert.Equal(46.5, fc, 0);
    }

    [Fact]
    public void LPD_Calculation()
    {
        double lpd = LightingCalculationService.CalculateLPD(600, 1000);
        Assert.Equal(0.6, lpd, 2);
    }

    [Fact]
    public void LPD_ZeroArea_ReturnsZero()
    {
        double lpd = LightingCalculationService.CalculateLPD(600, 0);
        Assert.Equal(0, lpd);
    }

    [Fact]
    public void LLF_TotalLLF_Product()
    {
        var llf = new LightingCalculationService.LightLossFactors
        {
            LampLumenDepreciation = 0.9,
            LuminaireDirtDepreciation = 0.85,
            RoomSurfaceDepreciation = 0.95,
            BallastFactor = 1.0,
        };
        Assert.Equal(0.9 * 0.85 * 0.95 * 1.0, llf.TotalLLF, 4);
    }

    // ── Large room scenario ──────────────────────────────────────────────────

    [Fact]
    public void ZonalCavity_Warehouse_LargeRoom()
    {
        var room = new LightingCalculationService.RoomGeometry
        {
            LengthFeet = 200, WidthFeet = 100,
            CeilingHeightFeet = 20, LuminaireMountHeightFeet = 18, WorkPlaneHeightFeet = 3,
        };
        var fixture = new LightingCalculationService.LuminaireData
        {
            Model = "HB-150W", InitialLumens = 20000, Watts = 150,
            CoefficientOfUtilization = 0.70, SpacingToMountHeightRatio = 1.5,
        };
        var result = LightingCalculationService.CalculateZonalCavity(room, fixture, 200);
        Assert.True(result.RecommendedFixtureCount > 10);
        Assert.True(result.MaintainedIlluminanceLux >= 200);
    }
}
