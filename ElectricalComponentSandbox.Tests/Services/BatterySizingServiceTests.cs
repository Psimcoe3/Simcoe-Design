using ElectricalComponentSandbox.Services;
using Xunit;
using static ElectricalComponentSandbox.Services.BatterySizingService;

namespace ElectricalComponentSandbox.Tests.Services;

public class BatterySizingServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BatterySizingInput DefaultInput(double loadKW = 50, double runtimeMin = 15) => new()
    {
        LoadKW = loadKW,
        PowerFactor = 0.9,
        TargetRuntimeMinutes = runtimeMin,
        GrowthFactor = 1.2,
        Chemistry = BatteryChemistry.LeadAcidVRLA,
        Topology = UpsTopology.Online,
        NominalDCVoltage = 480,
        EndCellVoltage = 1.75,
        AmbientTempC = 25,
        AgingFactor = 1.25,
        DesignMargin = 1.1,
    };

    // ── UPS kVA ──────────────────────────────────────────────────────────────

    [Fact]
    public void Size_UpsKVA_RoundedToStandard()
    {
        var result = Size(DefaultInput(loadKW: 50));

        // 50 * 1.2 / 0.9 = 66.7 kVA → rounds to 75 kVA standard
        Assert.Equal(75, result.UpsKVA);
    }

    [Fact]
    public void Size_UpsKW_IncludesGrowthFactor()
    {
        var result = Size(DefaultInput(loadKW: 50));

        Assert.Equal(60, result.UpsKW); // 50 * 1.2
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2.5, 3)]
    [InlineData(9, 10)]
    [InlineData(45, 50)]
    [InlineData(70, 75)]
    [InlineData(90, 100)]
    public void RoundToStandardKVA_Values(double input, double expected)
    {
        Assert.Equal(expected, BatterySizingService.RoundToStandardKVA(input));
    }

    // ── Cell Count ───────────────────────────────────────────────────────────

    [Fact]
    public void Size_VRLA_CellCount()
    {
        var result = Size(DefaultInput());

        // 480V / 2.0V per cell = 240 cells
        Assert.Equal(240, result.CellCount);
    }

    [Fact]
    public void Size_LithiumIon_CellCount()
    {
        var input = DefaultInput() with { Chemistry = BatteryChemistry.LithiumIon };
        var result = Size(input);

        // 480 / 3.6 = 133.3 → 134 cells
        Assert.Equal(134, result.CellCount);
    }

    [Fact]
    public void Size_NiCd_CellCount()
    {
        var input = DefaultInput() with { Chemistry = BatteryChemistry.NickelCadmium };
        var result = Size(input);

        // 480 / 1.2 = 400 cells
        Assert.Equal(400, result.CellCount);
    }

    // ── Battery AH ───────────────────────────────────────────────────────────

    [Fact]
    public void Size_BatteryAH_PositiveAndRounded()
    {
        var result = Size(DefaultInput(loadKW: 50, runtimeMin: 15));

        Assert.True(result.BatteryAH > 0);
        Assert.Equal(0, result.BatteryAH % 10); // rounded to nearest 10
    }

    [Fact]
    public void Size_LongerRuntime_MoreAH()
    {
        var r15 = Size(DefaultInput(loadKW: 50, runtimeMin: 15));
        var r30 = Size(DefaultInput(loadKW: 50, runtimeMin: 30));

        Assert.True(r30.BatteryAH > r15.BatteryAH);
    }

    [Fact]
    public void Size_LargerLoad_MoreAH()
    {
        var r50 = Size(DefaultInput(loadKW: 50));
        var r100 = Size(DefaultInput(loadKW: 100));

        Assert.True(r100.BatteryAH > r50.BatteryAH);
    }

    // ── Temperature Derate ───────────────────────────────────────────────────

    [Fact]
    public void Size_At25C_NoTempDerate()
    {
        var result = Size(DefaultInput() with { AmbientTempC = 25 });

        Assert.Equal(1.0, result.TemperatureDerateFactor);
    }

    [Fact]
    public void Size_Below25C_IncreasesCapacity()
    {
        var result = Size(DefaultInput() with { AmbientTempC = 15 });

        Assert.True(result.TemperatureDerateFactor > 1.0);
    }

    [Fact]
    public void Size_Below25C_Note()
    {
        // At 25°C, no temp derate note should appear
        var result = Size(DefaultInput() with { AmbientTempC = 25 });
        Assert.DoesNotContain(result.Notes, n => n.Contains("Temperature derate"));
    }

    // ── Energy ───────────────────────────────────────────────────────────────

    [Fact]
    public void Size_TotalEnergyKWH_Calculated()
    {
        var result = Size(DefaultInput());

        // TotalKWH = AH * DCV / 1000
        double expected = result.BatteryAH * 480 / 1000.0;
        Assert.Equal(expected, result.TotalEnergyKWH, 1);
    }

    // ── Weight ───────────────────────────────────────────────────────────────

    [Fact]
    public void Size_Weight_Positive()
    {
        var result = Size(DefaultInput());

        Assert.True(result.EstimatedWeightLbs > 0);
    }

    [Fact]
    public void Size_LithiumIon_LighterThanLeadAcid()
    {
        var vrla = Size(DefaultInput() with { Chemistry = BatteryChemistry.LeadAcidVRLA });
        var liion = Size(DefaultInput() with { Chemistry = BatteryChemistry.LithiumIon });

        // Li-ion has fewer cells and lower weight per AH
        Assert.True(liion.EstimatedWeightLbs < vrla.EstimatedWeightLbs);
    }

    // ── CalculateRuntime ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateRuntime_KnownValues()
    {
        // 100 AH at 480VDC, 10kW load, 0.92 efficiency
        // DC amps = 10000 / (480 * 0.92) = 22.65A
        // Hours = 100 / 22.65 = 4.415 → 264.9 min
        double runtime = BatterySizingService.CalculateRuntime(100, 10, 480, 0.92);

        Assert.InRange(runtime, 260, 270);
    }

    [Fact]
    public void CalculateRuntime_ZeroLoad_ReturnsZero()
    {
        double runtime = BatterySizingService.CalculateRuntime(100, 0, 480);

        Assert.Equal(0, runtime);
    }

    [Fact]
    public void CalculateRuntime_MoreAH_LongerRuntime()
    {
        double r100 = BatterySizingService.CalculateRuntime(100, 10, 480);
        double r200 = BatterySizingService.CalculateRuntime(200, 10, 480);

        Assert.True(r200 > r100);
        Assert.InRange(r200 / r100, 1.9, 2.1); // should be ~2x
    }

    // ── Topology ─────────────────────────────────────────────────────────────

    [Fact]
    public void Size_LineInteractive_MoreEfficient()
    {
        var online = Size(DefaultInput() with { Topology = UpsTopology.Online });
        var lineInt = Size(DefaultInput() with { Topology = UpsTopology.LineInteractive });

        // Line-interactive is more efficient, needs less battery
        Assert.True(lineInt.BatteryAH <= online.BatteryAH);
    }

    // ── Notes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Size_LongRuntime_GeneratesNote()
    {
        var result = Size(DefaultInput() with { TargetRuntimeMinutes = 120 });

        Assert.Contains(result.Notes, n => n.Contains("generator backup"));
    }

    // ── Small UPS ────────────────────────────────────────────────────────────

    [Fact]
    public void Size_SmallLoad_SelectsSmallUPS()
    {
        var result = Size(DefaultInput(loadKW: 0.5, runtimeMin: 10));

        Assert.True(result.UpsKVA <= 3);
        Assert.True(result.BatteryAH > 0);
    }
}
