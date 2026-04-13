using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class EmergencyLightingServiceTests
{
    // ── Illumination Requirements ────────────────────────────────────────────

    [Theory]
    [InlineData(EmergencyLightingService.EgressPathType.Corridor)]
    [InlineData(EmergencyLightingService.EgressPathType.Stairway)]
    [InlineData(EmergencyLightingService.EgressPathType.Exit)]
    public void RequiredIllumination_AtLeast1Fc(EmergencyLightingService.EgressPathType pt)
    {
        double fc = EmergencyLightingService.GetRequiredIllumination(pt);
        Assert.True(fc >= 1.0);
    }

    [Theory]
    [InlineData(EmergencyLightingService.EgressPathType.Corridor)]
    [InlineData(EmergencyLightingService.EgressPathType.OpenFloor)]
    public void MinIllumination_0_1Fc(EmergencyLightingService.EgressPathType pt)
    {
        double fc = EmergencyLightingService.GetMinimumIllumination(pt);
        Assert.Equal(0.1, fc);
    }

    [Fact]
    public void UniformityRatio_Max40()
    {
        double ratio = EmergencyLightingService.GetMaxUniformityRatio(
            EmergencyLightingService.EgressPathType.Corridor);
        Assert.Equal(40.0, ratio);
    }

    // ── Duration ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EmergencyLightingService.EmergencyPowerSource.BatteryUnit)]
    [InlineData(EmergencyLightingService.EmergencyPowerSource.Generator)]
    [InlineData(EmergencyLightingService.EmergencyPowerSource.CentralBattery)]
    public void Duration_90Minutes(EmergencyLightingService.EmergencyPowerSource src)
    {
        double dur = EmergencyLightingService.GetRequiredDurationMinutes(src);
        Assert.Equal(90, dur);
    }

    // ── Unit Estimation ──────────────────────────────────────────────────────

    [Fact]
    public void EstimateCorridor_OnePer50ft()
    {
        var seg = new EmergencyLightingService.EgressSegment
        {
            PathType = EmergencyLightingService.EgressPathType.Corridor,
            LengthFeet = 120, WidthFeet = 8
        };
        int units = EmergencyLightingService.EstimateMinimumUnits(seg);
        Assert.Equal(3, units); // Ceiling(120/50) = 3
    }

    [Fact]
    public void EstimateStairway_OnePer30ft()
    {
        var seg = new EmergencyLightingService.EgressSegment
        {
            PathType = EmergencyLightingService.EgressPathType.Stairway,
            LengthFeet = 45, WidthFeet = 5
        };
        int units = EmergencyLightingService.EstimateMinimumUnits(seg);
        Assert.Equal(2, units); // Ceiling(45/30) = 2
    }

    [Fact]
    public void EstimateExitDoor_AtLeast1()
    {
        var seg = new EmergencyLightingService.EgressSegment
        {
            PathType = EmergencyLightingService.EgressPathType.Exit,
            LengthFeet = 3, WidthFeet = 3, AreaSqFt = 9
        };
        Assert.Equal(1, EmergencyLightingService.EstimateMinimumUnits(seg));
    }

    [Fact]
    public void EstimateOpenFloor_Per1000Sqft()
    {
        var seg = new EmergencyLightingService.EgressSegment
        {
            PathType = EmergencyLightingService.EgressPathType.OpenFloor,
            AreaSqFt = 3500
        };
        int units = EmergencyLightingService.EstimateMinimumUnits(seg);
        Assert.Equal(4, units); // Ceiling(3500/1000) = 4
    }

    // ── Battery Sizing ───────────────────────────────────────────────────────

    [Fact]
    public void BatterySizing_Defaults()
    {
        var result = EmergencyLightingService.SizeBatteryUnits(5);
        Assert.Equal(5, result.UnitCount);
        Assert.Equal(36.0, result.TotalWatts);  // 5 × 7.2
        Assert.Equal(90, result.RequiredDurationMinutes);
        Assert.True(result.BatteryAH > 0);
    }

    [Fact]
    public void BatterySizing_AgingFactor()
    {
        var noAging = EmergencyLightingService.SizeBatteryUnits(1, agingFactor: 1.0);
        var withAging = EmergencyLightingService.SizeBatteryUnits(1, agingFactor: 1.25);
        Assert.True(withAging.BatteryAH > noAging.BatteryAH);
    }

    // ── Full Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_MultipleSegments()
    {
        var segments = new List<EmergencyLightingService.EgressSegment>
        {
            new() { Id = "C1", Name = "Main Corridor", PathType = EmergencyLightingService.EgressPathType.Corridor, LengthFeet = 100, WidthFeet = 8 },
            new() { Id = "S1", Name = "Stair A", PathType = EmergencyLightingService.EgressPathType.Stairway, LengthFeet = 30, WidthFeet = 4 },
            new() { Id = "E1", Name = "Exit A", PathType = EmergencyLightingService.EgressPathType.Exit, LengthFeet = 3, WidthFeet = 3, AreaSqFt = 9 },
        };

        var result = EmergencyLightingService.Analyze(segments);

        Assert.Equal(3, result.Segments.Count);
        Assert.True(result.TotalUnitsRequired > 0);
        Assert.True(result.TotalAreaSqFt > 0);
        Assert.Equal(90, result.RequiredDurationMinutes);
        Assert.NotNull(result.BatterySizing);
    }

    [Fact]
    public void Analyze_GeneratorSource_NoBattery()
    {
        var segments = new List<EmergencyLightingService.EgressSegment>
        {
            new() { Id = "C1", Name = "Hall", PathType = EmergencyLightingService.EgressPathType.Corridor, LengthFeet = 50, WidthFeet = 6 },
        };

        var result = EmergencyLightingService.Analyze(segments,
            EmergencyLightingService.EmergencyPowerSource.Generator);

        Assert.Null(result.BatterySizing);
        Assert.True(result.TotalUnitsRequired >= 1);
    }

    [Fact]
    public void Analyze_SegmentResults_HaveCorrectRequirements()
    {
        var segments = new List<EmergencyLightingService.EgressSegment>
        {
            new() { Id = "S1", Name = "Stair", PathType = EmergencyLightingService.EgressPathType.Stairway, LengthFeet = 30, WidthFeet = 4 },
        };

        var result = EmergencyLightingService.Analyze(segments);
        var seg = result.Segments[0];

        Assert.Equal(1.0, seg.RequiredFc);
        Assert.Equal(0.1, seg.MinFc);
        Assert.Equal(40.0, seg.MaxUniformityRatio);
    }
}
