using System;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class StormHardeningServiceTests
{
    private static OutageImpactService.OutageScenario CreateBaseScenario() => new()
    {
        Name = "Storm Circuit",
        DurationMinutes = 180,
        Segments =
        {
            new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 300, AverageDemandKw = 1.5 },
            new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Commercial, CustomerCount = 20, AverageDemandKw = 15 },
        },
    };

    [Fact]
    public void CreateHardenedScenario_ReducesDuration()
    {
        var scenario = StormHardeningService.CreateHardenedScenario(
            CreateBaseScenario(),
            new StormHardeningService.HardeningMeasure { Name = "Tree Trim", DurationReductionPercent = 25 });

        Assert.Equal(135, scenario.DurationMinutes);
    }

    [Fact]
    public void EvaluateMeasure_ReducesAnnualEventCountWhenFailureRateDrops()
    {
        var result = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            4,
            new StormHardeningService.HardeningMeasure { Name = "Covered Conductor", FailureRateReductionPercent = 50, DurationReductionPercent = 10, ImplementationCost = 100000 });

        Assert.Equal(2, result.ReducedAnnualEventCount);
    }

    [Fact]
    public void EvaluateMeasure_ComputesAvoidedCustomerMinutes()
    {
        var result = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            3,
            new StormHardeningService.HardeningMeasure { Name = "Spacer Cable", DurationReductionPercent = 20, ImplementationCost = 25000 });

        Assert.True(result.AvoidedAnnualCustomerMinutes > 0);
        Assert.True(result.HardenedDurationMinutes < 180);
    }

    [Fact]
    public void EvaluateMeasure_ComputesAvoidedAnnualCost()
    {
        var result = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            2,
            new StormHardeningService.HardeningMeasure { Name = "Pole Upgrade", FailureRateReductionPercent = 25, DurationReductionPercent = 20, ImplementationCost = 50000 });

        Assert.True(result.AvoidedAnnualCost > 0);
    }

    [Fact]
    public void EvaluateMeasure_ZeroBenefitHasInfinitePayback()
    {
        var result = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            2,
            new StormHardeningService.HardeningMeasure { Name = "No-op", FailureRateReductionPercent = 0, DurationReductionPercent = 0, ImplementationCost = 5000 });

        Assert.True(double.IsPositiveInfinity(result.SimplePaybackYears));
        Assert.Equal(0, result.AvoidedAnnualCost);
    }

    [Fact]
    public void RankMeasures_SortsByBenefitCostRatio()
    {
        var ranked = StormHardeningService.RankMeasures(
            CreateBaseScenario(),
            3,
            new[]
            {
                new StormHardeningService.HardeningMeasure { Name = "Expensive", FailureRateReductionPercent = 40, DurationReductionPercent = 25, ImplementationCost = 500000 },
                new StormHardeningService.HardeningMeasure { Name = "Efficient", FailureRateReductionPercent = 20, DurationReductionPercent = 15, ImplementationCost = 10000 },
            });

        Assert.Equal("Efficient", ranked[0].MeasureName);
    }

    [Fact]
    public void EvaluateMeasure_AvoidedEnergyTracksDurationReduction()
    {
        var shorter = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            1,
            new StormHardeningService.HardeningMeasure { Name = "Small", DurationReductionPercent = 10, ImplementationCost = 10000 });
        var larger = StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            1,
            new StormHardeningService.HardeningMeasure { Name = "Large", DurationReductionPercent = 40, ImplementationCost = 10000 });

        Assert.True(larger.AvoidedAnnualUnservedEnergyMWh > shorter.AvoidedAnnualUnservedEnergyMWh);
    }

    [Fact]
    public void EvaluateMeasure_InvalidPercentThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StormHardeningService.EvaluateMeasure(
            CreateBaseScenario(),
            1,
            new StormHardeningService.HardeningMeasure { Name = "Bad", FailureRateReductionPercent = 120, ImplementationCost = 1000 }));
    }
}