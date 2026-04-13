using ElectricalComponentSandbox.Services;
using System.Collections.Generic;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class UpsSizingServiceTests
{
    private static List<UpsSizingService.UpsLoad> SampleLoads() => new()
    {
        new() { Name = "Server Rack 1", Watts = 5000, Type = UpsSizingService.LoadType.ITEquipment, IsCritical = true },
        new() { Name = "Server Rack 2", Watts = 5000, Type = UpsSizingService.LoadType.ITEquipment, IsCritical = true },
        new() { Name = "Network Switch", Watts = 500, Type = UpsSizingService.LoadType.ITEquipment, IsCritical = true },
        new() { Name = "Office PCs", Watts = 1500, Type = UpsSizingService.LoadType.MixedLoad, IsCritical = false },
    };

    // ── Power Factor ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(UpsSizingService.LoadType.ITEquipment, 0.95)]
    [InlineData(UpsSizingService.LoadType.LinearLoad, 1.0)]
    [InlineData(UpsSizingService.LoadType.MotorLoad, 0.80)]
    [InlineData(UpsSizingService.LoadType.MixedLoad, 0.85)]
    public void GetTypicalPowerFactor_ReturnsExpected(UpsSizingService.LoadType type, double expected)
    {
        Assert.Equal(expected, UpsSizingService.GetTypicalPowerFactor(type));
    }

    // ── Load Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeLoads_CalculatesTotals()
    {
        var result = UpsSizingService.AnalyzeLoads(SampleLoads());

        Assert.Equal(12000, result.TotalWatts);
        Assert.True(result.TotalVA > result.TotalWatts); // PF < 1.0 for some loads
        Assert.Equal(10500, result.CriticalWatts);
        Assert.Equal(4, result.LoadCount);
    }

    [Fact]
    public void AnalyzeLoads_WeightedPowerFactor_LessThanOne()
    {
        var result = UpsSizingService.AnalyzeLoads(SampleLoads());

        Assert.True(result.WeightedPowerFactor > 0.8 && result.WeightedPowerFactor < 1.0);
    }

    [Fact]
    public void AnalyzeLoads_AllLinear_PFIsOne()
    {
        var loads = new List<UpsSizingService.UpsLoad>
        {
            new() { Watts = 1000, Type = UpsSizingService.LoadType.LinearLoad },
        };
        var result = UpsSizingService.AnalyzeLoads(loads);

        Assert.Equal(1.0, result.WeightedPowerFactor);
    }

    // ── Battery Sizing ───────────────────────────────────────────────────────

    [Fact]
    public void SizeBattery_15min_CorrectKWh()
    {
        var result = UpsSizingService.SizeBattery(10, 15);

        // Raw = 10 × (15/60) = 2.5 kWh
        // Required = 2.5 × 1.25 / 0.80 / 1.0 = 3.90625
        Assert.Equal(3.91, result.RequiredKWh, 2);
        Assert.Equal(1.0, result.TemperatureDerating);
    }

    [Fact]
    public void SizeBattery_HighTemp_Derated()
    {
        var normal = UpsSizingService.SizeBattery(10, 15, 25);
        var hot = UpsSizingService.SizeBattery(10, 15, 40);

        Assert.True(hot.RequiredKWh > normal.RequiredKWh);
        Assert.True(hot.TemperatureDerating < 1.0);
    }

    [Fact]
    public void SizeBattery_LongerRuntime_MoreCapacity()
    {
        var short15 = UpsSizingService.SizeBattery(10, 15);
        var long60 = UpsSizingService.SizeBattery(10, 60);

        Assert.True(long60.RequiredKWh > short15.RequiredKWh);
    }

    // ── UPS Sizing ───────────────────────────────────────────────────────────

    [Fact]
    public void SizeUps_ReturnsStandardFrame()
    {
        var result = UpsSizingService.SizeUps(SampleLoads(), 15);

        Assert.True(result.RecommendedKVA > 0);
        Assert.True(result.UtilizationPercent > 0 && result.UtilizationPercent <= 100);
        Assert.NotNull(result.Battery);
    }

    [Fact]
    public void SizeUps_DoubleConversion_DefaultTopology()
    {
        var result = UpsSizingService.SizeUps(SampleLoads(), 15);

        Assert.Equal(UpsSizingService.UpsTopology.DoubleConversion, result.Topology);
    }

    [Fact]
    public void SizeUps_NPlus1_TwoModules()
    {
        var result = UpsSizingService.SizeUps(SampleLoads(), 15,
            redundancy: UpsSizingService.RedundancyMode.NPlus1);

        Assert.Equal(2, result.ModuleCount);
    }

    [Fact]
    public void SizeUps_2N_TwoModules()
    {
        var result = UpsSizingService.SizeUps(SampleLoads(), 15,
            redundancy: UpsSizingService.RedundancyMode.TwoN);

        Assert.Equal(2, result.ModuleCount);
    }

    [Fact]
    public void SizeUps_UtilizationUnder80Percent()
    {
        var result = UpsSizingService.SizeUps(SampleLoads(), 15);

        // Frame selected at 80% max utilization
        Assert.True(result.UtilizationPercent <= 80);
    }

    // ── Topology Recommendation ──────────────────────────────────────────────

    [Theory]
    [InlineData(100, UpsSizingService.UpsTopology.DoubleConversion)]
    [InlineData(80, UpsSizingService.UpsTopology.DoubleConversion)]
    [InlineData(50, UpsSizingService.UpsTopology.LineInteractive)]
    [InlineData(20, UpsSizingService.UpsTopology.Standby)]
    public void RecommendTopology_ByPercentage(double critPct, UpsSizingService.UpsTopology expected)
    {
        Assert.Equal(expected, UpsSizingService.RecommendTopology(critPct));
    }
}
