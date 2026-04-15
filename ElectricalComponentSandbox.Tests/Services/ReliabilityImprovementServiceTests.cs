using System;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ReliabilityImprovementServiceTests
{
    private static ReliabilityImprovementService.ReliabilityProgram CreateBaselineProgram() => new()
    {
        Name = "Baseline",
        Events =
        {
            new ReliabilityImprovementService.ReliabilityEvent { Name = "Tree Contact", DurationMinutes = 90, CustomersAffected = 500, AnnualOccurrenceCount = 2 },
            new ReliabilityImprovementService.ReliabilityEvent { Name = "Animal Contact", DurationMinutes = 30, CustomersAffected = 200, AnnualOccurrenceCount = 3 },
        },
    };

    [Fact]
    public void BuildAnnualInterruptionSeries_ExpandsOccurrenceCounts()
    {
        var series = ReliabilityImprovementService.BuildAnnualInterruptionSeries(CreateBaselineProgram());

        Assert.Equal(5, series.durations.Count);
        Assert.Equal(5, series.customersAffected.Count);
    }

    [Fact]
    public void CalculateProgramIndices_UsesSharedReliabilityCalculation()
    {
        var result = ReliabilityImprovementService.CalculateProgramIndices(
            new ReliabilityImprovementService.ReliabilityProgram
            {
                Name = "Single",
                Events =
                {
                    new ReliabilityImprovementService.ReliabilityEvent { Name = "Trip", DurationMinutes = 60, CustomersAffected = 500 },
                },
            },
            1000);

        Assert.Equal(30, result.SAIDI);
        Assert.Equal(0.5, result.SAIFI, 4);
    }

    [Fact]
    public void ApplyHardeningMeasure_ReducesDurationAndOccurrencesForNamedEvent()
    {
        var improved = ReliabilityImprovementService.ApplyHardeningMeasure(
            CreateBaselineProgram(),
            "Tree Contact",
            new StormHardeningService.HardeningMeasure { Name = "Tree Trim", FailureRateReductionPercent = 50, DurationReductionPercent = 20 });
        var improvedEvent = improved.Events.Single(evt => evt.Name == "Tree Contact");

        Assert.Equal(72, improvedEvent.DurationMinutes);
        Assert.Equal(1, improvedEvent.AnnualOccurrenceCount);
    }

    [Fact]
    public void ApplyHardeningMeasure_NonMatchingEventLeavesProgramUnchanged()
    {
        var baseline = CreateBaselineProgram();
        var improved = ReliabilityImprovementService.ApplyHardeningMeasure(
            baseline,
            "Lightning",
            new StormHardeningService.HardeningMeasure { Name = "Arrester", FailureRateReductionPercent = 50, DurationReductionPercent = 20 });

        Assert.Equal(baseline.Events.Select(evt => evt.DurationMinutes), improved.Events.Select(evt => evt.DurationMinutes));
        Assert.Equal(baseline.Events.Select(evt => evt.AnnualOccurrenceCount), improved.Events.Select(evt => evt.AnnualOccurrenceCount));
    }

    [Fact]
    public void ComparePrograms_ShowsPositiveReliabilityImprovement()
    {
        var baseline = CreateBaselineProgram();
        var improved = ReliabilityImprovementService.ApplyHardeningMeasure(
            baseline,
            "Tree Contact",
            new StormHardeningService.HardeningMeasure { Name = "Covered Conductor", FailureRateReductionPercent = 50, DurationReductionPercent = 25 });

        var result = ReliabilityImprovementService.ComparePrograms(baseline, improved, 1000);

        Assert.True(result.SaidiReduction > 0);
        Assert.True(result.SaifiReduction > 0);
        Assert.True(result.AvailabilityGain > 0);
    }

    [Fact]
    public void RankPrograms_SortsBestReliabilityFirst()
    {
        var baseline = CreateBaselineProgram();
        var improved = ReliabilityImprovementService.ApplyHardeningMeasure(
            baseline,
            "Tree Contact",
            new StormHardeningService.HardeningMeasure { Name = "Covered Conductor", FailureRateReductionPercent = 50, DurationReductionPercent = 25 });

        var ranked = ReliabilityImprovementService.RankPrograms(new[] { baseline, improved }, 1000);

        Assert.Equal(improved.Name, ranked[0].Name);
    }

    [Fact]
    public void BuildAnnualInterruptionSeries_NegativeOccurrenceThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReliabilityImprovementService.BuildAnnualInterruptionSeries(
            new ReliabilityImprovementService.ReliabilityProgram
            {
                Events = { new ReliabilityImprovementService.ReliabilityEvent { Name = "Bad", DurationMinutes = 10, CustomersAffected = 5, AnnualOccurrenceCount = -1 } },
            }));
    }

    [Fact]
    public void ComparePrograms_CaidiReductionReflectsShorterAverageDuration()
    {
        var baseline = CreateBaselineProgram();
        var improved = ReliabilityImprovementService.ApplyHardeningMeasure(
            baseline,
            "Tree Contact",
            new StormHardeningService.HardeningMeasure { Name = "Fast Restore", FailureRateReductionPercent = 0, DurationReductionPercent = 50 });

        var result = ReliabilityImprovementService.ComparePrograms(baseline, improved, 1000);

        Assert.True(result.CaidiReduction > 0);
    }
}