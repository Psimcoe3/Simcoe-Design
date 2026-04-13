using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class EquipmentThermalAnalysisServiceTests
{
    // ── Heat Dissipation ─────────────────────────────────────────────────────

    [Fact]
    public void HeatDissipation_FromEfficiency()
    {
        var src = new EquipmentThermalAnalysisService.HeatSource
        {
            Id = "T1", RatedKW = 100, EfficiencyPercent = 97, LoadFactor = 1.0,
        };
        double heat = EquipmentThermalAnalysisService.CalculateHeatDissipation(src);
        Assert.Equal(3000, heat, 0); // 100kW × 3% loss = 3kW = 3000W
    }

    [Fact]
    public void HeatDissipation_WithLoadFactor()
    {
        var src = new EquipmentThermalAnalysisService.HeatSource
        {
            Id = "T1", RatedKW = 100, EfficiencyPercent = 97, LoadFactor = 0.5,
        };
        double heat = EquipmentThermalAnalysisService.CalculateHeatDissipation(src);
        Assert.Equal(1500, heat, 0); // 100kW × 0.5 × 3% = 1.5kW
    }

    [Fact]
    public void HeatDissipation_ExplicitOverride()
    {
        var src = new EquipmentThermalAnalysisService.HeatSource
        {
            Id = "T1", RatedKW = 100, HeatDissipationWatts = 5000, LoadFactor = 0.8,
        };
        double heat = EquipmentThermalAnalysisService.CalculateHeatDissipation(src);
        Assert.Equal(4000, heat, 0); // 5000 × 0.8
    }

    // ── Transformer Heat ─────────────────────────────────────────────────────

    [Fact]
    public void TransformerHeat_75kVA_FullLoad()
    {
        var result = EquipmentThermalAnalysisService.CalculateTransformerHeat(75, 1.0);
        Assert.True(result.NoLoadLossWatts > 0);
        Assert.True(result.LoadLossWatts > 0);
        Assert.Equal(result.NoLoadLossWatts + result.LoadLossWatts, result.TotalLossWatts);
        Assert.True(result.TotalLossBtuH > result.TotalLossWatts); // BTU > Watts
    }

    [Fact]
    public void TransformerHeat_HalfLoad_LessLoss()
    {
        var full = EquipmentThermalAnalysisService.CalculateTransformerHeat(75, 1.0);
        var half = EquipmentThermalAnalysisService.CalculateTransformerHeat(75, 0.5);
        Assert.True(half.TotalLossWatts < full.TotalLossWatts);
        // No-load same at both loads
        Assert.Equal(full.NoLoadLossWatts, half.NoLoadLossWatts);
    }

    [Fact]
    public void TransformerHeat_LoadLoss_SquareLaw()
    {
        var full = EquipmentThermalAnalysisService.CalculateTransformerHeat(75, 1.0);
        var half = EquipmentThermalAnalysisService.CalculateTransformerHeat(75, 0.5);
        // Load loss at half = 1/4 of full load loss
        Assert.Equal(full.LoadLossWatts / 4.0, half.LoadLossWatts, 0);
    }

    // ── Room Thermal Analysis ────────────────────────────────────────────────

    [Fact]
    public void AnalyzeRoom_SingleTransformer()
    {
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "T1", Name = "TX-1", Category = EquipmentThermalAnalysisService.EquipmentCategory.Transformer,
                     RatedKW = 75, EfficiencyPercent = 97 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources, roomVolumeCuFt: 2000);
        Assert.Equal(1, result.Sources.Count);
        Assert.True(result.TotalHeatWatts > 0);
        Assert.True(result.TotalHeatBtuH > 0);
        Assert.True(result.AmbientRiseDegF >= 0);
    }

    [Fact]
    public void AnalyzeRoom_MultipleEquipment()
    {
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "T1", Name = "TX-1", RatedKW = 75, EfficiencyPercent = 97 },
            new() { Id = "V1", Name = "VFD-1", RatedKW = 50, EfficiencyPercent = 95 },
            new() { Id = "U1", Name = "UPS-1", RatedKW = 30, EfficiencyPercent = 92 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources);
        Assert.Equal(3, result.Sources.Count);
        double expectedWatts = 75000 * 0.03 + 50000 * 0.05 + 30000 * 0.08;
        Assert.Equal(expectedWatts, result.TotalHeatWatts, 0);
    }

    [Fact]
    public void AnalyzeRoom_CoolingTons()
    {
        // Large electrical room: 10kW total heat
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "T1", HeatDissipationWatts = 10000, LoadFactor = 1.0 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources);
        // 10000W × 3.412 = 34120 BTU/h / 12000 = 2.84 tons
        Assert.InRange(result.RequiredCoolingTons, 2.8, 2.9);
        Assert.True(result.RequiresDedicatedCooling);
    }

    [Fact]
    public void AnalyzeRoom_SmallLoad_NoDedicatedCooling()
    {
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "P1", HeatDissipationWatts = 500, LoadFactor = 1.0 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources);
        Assert.False(result.RequiresDedicatedCooling); // < 1 ton
    }

    [Fact]
    public void AnalyzeRoom_TempRise()
    {
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "T1", HeatDissipationWatts = 5000, LoadFactor = 1.0 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources, 1000, 75, 2.0);
        Assert.True(result.EstimatedRoomTempF > 75);
        Assert.True(result.AmbientRiseDegF > 0);
    }

    [Fact]
    public void AnalyzeRoom_EmptySources()
    {
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(
            new List<EquipmentThermalAnalysisService.HeatSource>());
        Assert.Equal(0, result.TotalHeatWatts);
        Assert.Equal(0, result.RequiredCoolingTons);
    }

    // ── Ambient Correction Factor ────────────────────────────────────────────

    [Theory]
    [InlineData(25, 1.00)]
    [InlineData(30, 1.00)]
    [InlineData(35, 0.94)]
    [InlineData(40, 0.88)]
    [InlineData(50, 0.75)]
    [InlineData(60, 0.58)]
    [InlineData(65, 0.00)]
    public void AmbientCorrectionFactor_75C(double tempC, double expected)
    {
        double factor = EquipmentThermalAnalysisService.AmbientCorrectionFactor75C(tempC);
        Assert.Equal(expected, factor);
    }

    // ── Utility Conversions ──────────────────────────────────────────────────

    [Fact]
    public void FahrenheitToCelsius()
    {
        Assert.Equal(0, EquipmentThermalAnalysisService.FahrenheitToCelsius(32), 1);
        Assert.Equal(100, EquipmentThermalAnalysisService.FahrenheitToCelsius(212), 1);
        Assert.Equal(37.8, EquipmentThermalAnalysisService.FahrenheitToCelsius(100), 1);
    }

    [Fact]
    public void WattsToBtu()
    {
        double btu = EquipmentThermalAnalysisService.WattsToBtu(1000);
        Assert.Equal(3412, btu, 0);
    }

    [Fact]
    public void BtuToTons()
    {
        double tons = EquipmentThermalAnalysisService.BtuToTons(24000);
        Assert.Equal(2.0, tons);
    }

    // ── BTU consistency ──────────────────────────────────────────────────────

    [Fact]
    public void RoomResult_BtuMatchesWatts()
    {
        var sources = new List<EquipmentThermalAnalysisService.HeatSource>
        {
            new() { Id = "T1", HeatDissipationWatts = 3000, LoadFactor = 1.0 },
        };
        var result = EquipmentThermalAnalysisService.AnalyzeRoom(sources);
        Assert.Equal(result.TotalHeatWatts * EquipmentThermalAnalysisService.BtuPerWatt,
                     result.TotalHeatBtuH, 0);
    }
}
