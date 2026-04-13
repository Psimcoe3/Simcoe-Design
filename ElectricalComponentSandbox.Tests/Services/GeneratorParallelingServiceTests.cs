using System;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class GeneratorParallelingServiceTests
{
    [Fact]
    public void CheckSynchronizing_WithinWindow_IsAcceptable()
    {
        var result = GeneratorParallelingService.CheckSynchronizing(478, 480, 60.02, 60.00, 4);

        Assert.True(result.IsAcceptable);
        Assert.Null(result.Issue);
    }

    [Fact]
    public void CheckSynchronizing_HighVoltageDifference_Fails()
    {
        var result = GeneratorParallelingService.CheckSynchronizing(520, 480, 60.02, 60.00, 4);

        Assert.False(result.IsAcceptable);
        Assert.Contains("Voltage", result.Issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckSynchronizing_HighFrequencyDifference_Fails()
    {
        var result = GeneratorParallelingService.CheckSynchronizing(480, 480, 60.5, 60.0, 4);

        Assert.False(result.IsAcceptable);
        Assert.Contains("Frequency", result.Issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckSynchronizing_HighPhaseAngle_Fails()
    {
        var result = GeneratorParallelingService.CheckSynchronizing(480, 480, 60.01, 60.0, 18);

        Assert.False(result.IsAcceptable);
        Assert.Contains("Phase", result.Issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecommendMode_UtilityParallel_UsesDroop()
    {
        var mode = GeneratorParallelingService.RecommendMode(true, 3);

        Assert.Equal(GeneratorParallelingService.LoadShareMode.RealPowerDroop, mode);
    }

    [Fact]
    public void RecommendMode_SingleUnit_UsesIsochronous()
    {
        var mode = GeneratorParallelingService.RecommendMode(false, 1);

        Assert.Equal(GeneratorParallelingService.LoadShareMode.Isochronous, mode);
    }

    [Fact]
    public void RecommendMode_LargeStepLoad_UsesIsochronous()
    {
        var mode = GeneratorParallelingService.RecommendMode(false, 2, hasLargeStepLoads: true);

        Assert.Equal(GeneratorParallelingService.LoadShareMode.Isochronous, mode);
    }

    [Fact]
    public void CreateParallelingPlan_NoUnits_ReturnsIssue()
    {
        var result = GeneratorParallelingService.CreateParallelingPlan(Array.Empty<GeneratorParallelingService.GeneratorUnit>(), 100);

        Assert.False(result.IsAdequate);
        Assert.Contains("No available", result.Issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateParallelingPlan_SelectsMinimumUnitsForReserve()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 100 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 100 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G3", RatedKW = 100 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 170, reservePercent: 15);

        Assert.True(result.IsAdequate);
        Assert.Equal(2, result.RequiredUnits);
        Assert.Equal(200, result.OnlineCapacityKW);
    }

    [Fact]
    public void CreateParallelingPlan_NPlusOneRequiresAdditionalUnit()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 100 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 100 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G3", RatedKW = 100 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 150, requireNPlusOne: true);

        Assert.True(result.IsAdequate);
        Assert.True(result.SupportsNPlusOne);
        Assert.Equal(3, result.RequiredUnits);
    }

    [Fact]
    public void CreateParallelingPlan_InsufficientCapacity_Fails()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 75 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 75 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 200, reservePercent: 10);

        Assert.False(result.IsAdequate);
        Assert.Contains("below demand", result.Issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateParallelingPlan_SharesLoadProportionallyByKW()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 100 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 50 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 120, reservePercent: 0);
        var g1 = result.UnitShares.Single(share => share.Id == "G1");
        var g2 = result.UnitShares.Single(share => share.Id == "G2");

        Assert.Equal(80.0, g1.AssignedKW);
        Assert.Equal(40.0, g2.AssignedKW);
    }

    [Fact]
    public void CreateParallelingPlan_SharesReactiveLoadProportionally()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 100, RatedKVAR = 80 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 100, RatedKVAR = 40 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 150, demandKVAR: 42, reservePercent: 0);
        var g1 = result.UnitShares.Single(share => share.Id == "G1");
        var g2 = result.UnitShares.Single(share => share.Id == "G2");

        Assert.Equal(28.0, g1.AssignedKVAR);
        Assert.Equal(14.0, g2.AssignedKVAR);
    }

    [Fact]
    public void CreateParallelingPlan_CalculatesSpinningReserve()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 125 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 125 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 180, reservePercent: 0);

        Assert.Equal(70.0, result.SpinningReserveKW);
    }

    [Fact]
    public void CreateParallelingPlan_FlagsBelowMinimumStableLoad()
    {
        var units = new[]
        {
            new GeneratorParallelingService.GeneratorUnit { Id = "G1", RatedKW = 50, MinStableLoadPercent = 30 },
            new GeneratorParallelingService.GeneratorUnit { Id = "G2", RatedKW = 50, MinStableLoadPercent = 30 },
        };

        var result = GeneratorParallelingService.CreateParallelingPlan(units, 20, reservePercent: 0, requireNPlusOne: true);

        Assert.All(result.UnitShares, share => Assert.True(share.BelowMinimumStableLoad));
    }
}