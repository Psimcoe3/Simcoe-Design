using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class GroundFaultProtectionServiceTests
{
    // ── NEC 230.95 Applicability ─────────────────────────────────────────────

    [Fact]
    public void Requirement_480Y277_1000A_Required()
    {
        var result = GroundFaultProtectionService.CheckRequirement(
            1000, GroundFaultProtectionService.SystemVoltageClass.V480Y277);
        Assert.True(result.Required);
        Assert.Contains("230.95", result.Reason);
    }

    [Fact]
    public void Requirement_480Y277_2000A_Required()
    {
        var result = GroundFaultProtectionService.CheckRequirement(
            2000, GroundFaultProtectionService.SystemVoltageClass.V480Y277);
        Assert.True(result.Required);
    }

    [Fact]
    public void Requirement_480Y277_800A_NotRequired()
    {
        var result = GroundFaultProtectionService.CheckRequirement(
            800, GroundFaultProtectionService.SystemVoltageClass.V480Y277);
        Assert.False(result.Required);
        Assert.Contains("below 1000A", result.Reason);
    }

    [Fact]
    public void Requirement_208Y120_1200A_NotRequired()
    {
        var result = GroundFaultProtectionService.CheckRequirement(
            1200, GroundFaultProtectionService.SystemVoltageClass.V208Y120);
        Assert.False(result.Required);
    }

    [Fact]
    public void Requirement_Other_NotRequired()
    {
        var result = GroundFaultProtectionService.CheckRequirement(
            2000, GroundFaultProtectionService.SystemVoltageClass.Other);
        Assert.False(result.Required);
    }

    // ── GFPE Sizing ──────────────────────────────────────────────────────────

    [Fact]
    public void SizeMain_MaxPickup1200A()
    {
        var result = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Main, 2000);
        Assert.Equal(1200, result.MaxPickupAmps);
        Assert.True(result.RecommendedPickupAmps <= 1200);
    }

    [Fact]
    public void SizeMain_DelayAtMost1s()
    {
        var result = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Main, 1600);
        Assert.True(result.MaxDelaySeconds <= 1.0);
        Assert.True(result.RecommendedDelaySeconds <= result.MaxDelaySeconds);
    }

    [Fact]
    public void SizeFeeder_LowerPickupThanMain()
    {
        var main = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Main, 2000);
        var feeder = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Feeder, 800);
        Assert.True(feeder.RecommendedPickupAmps < main.RecommendedPickupAmps);
    }

    [Fact]
    public void SizeFeeder_ShorterDelayThanMain()
    {
        var main = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Main, 2000);
        var feeder = GroundFaultProtectionService.SizeDevice(
            GroundFaultProtectionService.GfpeLevel.Feeder, 800);
        Assert.True(feeder.RecommendedDelaySeconds < main.RecommendedDelaySeconds);
    }

    // ── Two-Level Coordination ───────────────────────────────────────────────

    [Fact]
    public void Coordination_WellCoordinated()
    {
        var main = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "M1", Level = GroundFaultProtectionService.GfpeLevel.Main,
            PickupAmps = 1200, TripDelaySeconds = 0.5, EquipmentAmps = 2000,
        };
        var feeder = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "F1", Level = GroundFaultProtectionService.GfpeLevel.Feeder,
            PickupAmps = 400, TripDelaySeconds = 0.1, EquipmentAmps = 800,
        };
        var result = GroundFaultProtectionService.EvaluateCoordination(main, feeder);
        Assert.True(result.IsCoordinated);
        Assert.Empty(result.Violations);
        Assert.True(result.PickupRatio > 1.0);
    }

    [Fact]
    public void Coordination_PickupViolation()
    {
        var main = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "M1", Level = GroundFaultProtectionService.GfpeLevel.Main,
            PickupAmps = 300, TripDelaySeconds = 0.5, EquipmentAmps = 2000,
        };
        var feeder = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "F1", Level = GroundFaultProtectionService.GfpeLevel.Feeder,
            PickupAmps = 400, TripDelaySeconds = 0.1, EquipmentAmps = 800,
        };
        var result = GroundFaultProtectionService.EvaluateCoordination(main, feeder);
        Assert.False(result.IsCoordinated);
        Assert.Contains(result.Violations, v => v.Contains("pickup"));
    }

    [Fact]
    public void Coordination_DelayMarginViolation()
    {
        var main = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "M1", Level = GroundFaultProtectionService.GfpeLevel.Main,
            PickupAmps = 1200, TripDelaySeconds = 0.15, EquipmentAmps = 2000,
        };
        var feeder = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "F1", Level = GroundFaultProtectionService.GfpeLevel.Feeder,
            PickupAmps = 400, TripDelaySeconds = 0.1, EquipmentAmps = 800,
        };
        var result = GroundFaultProtectionService.EvaluateCoordination(main, feeder);
        Assert.False(result.IsCoordinated);
        Assert.Contains(result.Violations, v => v.Contains("margin"));
    }

    [Fact]
    public void Coordination_MainDelayExceeds1s()
    {
        var main = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "M1", Level = GroundFaultProtectionService.GfpeLevel.Main,
            PickupAmps = 1200, TripDelaySeconds = 1.5, EquipmentAmps = 2000,
        };
        var feeder = new GroundFaultProtectionService.GfpeDevice
        {
            Id = "F1", Level = GroundFaultProtectionService.GfpeLevel.Feeder,
            PickupAmps = 400, TripDelaySeconds = 0.1, EquipmentAmps = 800,
        };
        var result = GroundFaultProtectionService.EvaluateCoordination(main, feeder);
        Assert.False(result.IsCoordinated);
        Assert.Contains(result.Violations, v => v.Contains("230.95"));
    }

    // ── Zone-Selective Interlocking ──────────────────────────────────────────

    [Fact]
    public void Zsi_HighDelay_Recommended()
    {
        var result = GroundFaultProtectionService.AnalyzeZsi(0.5);
        Assert.True(result.ZsiRecommended);
        Assert.True(result.ArcEnergyReductionPercent > 80);
    }

    [Fact]
    public void Zsi_LowDelay_NotRecommended()
    {
        var result = GroundFaultProtectionService.AnalyzeZsi(0.1);
        Assert.False(result.ZsiRecommended);
    }

    [Fact]
    public void Zsi_ReductionCalculation()
    {
        var result = GroundFaultProtectionService.AnalyzeZsi(1.0, 0.05);
        // (1 - 0.05/1.0) × 100 = 95%
        Assert.Equal(95.0, result.ArcEnergyReductionPercent);
    }

    // ── Batch Sizing ─────────────────────────────────────────────────────────

    [Fact]
    public void SizeAll_OnlyRequired()
    {
        var services = new List<(string, double, GroundFaultProtectionService.SystemVoltageClass)>
        {
            ("S1", 2000, GroundFaultProtectionService.SystemVoltageClass.V480Y277),
            ("S2", 800, GroundFaultProtectionService.SystemVoltageClass.V480Y277),
            ("S3", 1200, GroundFaultProtectionService.SystemVoltageClass.V208Y120),
        };
        var results = GroundFaultProtectionService.SizeAll(services);
        Assert.Single(results); // Only S1 qualifies
    }

    [Fact]
    public void SizeAll_Empty()
    {
        var results = GroundFaultProtectionService.SizeAll(
            new List<(string, double, GroundFaultProtectionService.SystemVoltageClass)>());
        Assert.Empty(results);
    }
}
