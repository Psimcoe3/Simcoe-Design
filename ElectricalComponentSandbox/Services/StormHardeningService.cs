using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Evaluates storm hardening measures by comparing annual outage impact and cost before and after improvements.
/// </summary>
public static class StormHardeningService
{
    public record HardeningMeasure
    {
        public string Name { get; init; } = string.Empty;
        public double ImplementationCost { get; init; }
        public double FailureRateReductionPercent { get; init; }
        public double DurationReductionPercent { get; init; }
    }

    public record HardeningAssessment
    {
        public string MeasureName { get; init; } = string.Empty;
        public double BaseAnnualEventCount { get; init; }
        public double ReducedAnnualEventCount { get; init; }
        public double HardenedDurationMinutes { get; init; }
        public double AvoidedAnnualCustomerMinutes { get; init; }
        public double AvoidedAnnualUnservedEnergyMWh { get; init; }
        public double AvoidedAnnualCost { get; init; }
        public double BenefitCostRatio { get; init; }
        public double SimplePaybackYears { get; init; }
    }

    public static OutageImpactService.OutageScenario CreateHardenedScenario(
        OutageImpactService.OutageScenario baseScenario,
        HardeningMeasure measure)
    {
        double hardenedDuration = baseScenario.DurationMinutes * (1 - NormalizePercent(measure.DurationReductionPercent));
        return new OutageImpactService.OutageScenario
        {
            Name = $"{baseScenario.Name} - {measure.Name}",
            DurationMinutes = Math.Round(hardenedDuration, 2),
            Segments = baseScenario.Segments.ToList(),
        };
    }

    public static HardeningAssessment EvaluateMeasure(
        OutageImpactService.OutageScenario baseScenario,
        double annualEventCount,
        HardeningMeasure measure,
        IReadOnlyDictionary<OutageImpactService.CustomerClass, OutageCostService.InterruptionCostRate>? customRates = null)
    {
        if (annualEventCount < 0)
            throw new ArgumentOutOfRangeException(nameof(annualEventCount), "Annual event count must be non-negative.");

        var baseImpact = OutageImpactService.AnalyzeScenario(baseScenario);
        var hardenedScenario = CreateHardenedScenario(baseScenario, measure);
        var hardenedImpact = OutageImpactService.AnalyzeScenario(hardenedScenario);

        var baseCost = OutageCostService.EstimateScenarioCost(baseScenario, customRates);
        var hardenedCost = OutageCostService.EstimateScenarioCost(hardenedScenario, customRates);

        double reducedEventCount = annualEventCount * (1 - NormalizePercent(measure.FailureRateReductionPercent));
        double baseAnnualCustomerMinutes = baseImpact.CustomerMinutesInterrupted * annualEventCount;
        double hardenedAnnualCustomerMinutes = hardenedImpact.CustomerMinutesInterrupted * reducedEventCount;
        double baseAnnualUnservedEnergy = baseImpact.UnservedEnergyMWh * annualEventCount;
        double hardenedAnnualUnservedEnergy = hardenedImpact.UnservedEnergyMWh * reducedEventCount;
        double baseAnnualCost = baseCost.TotalCost * annualEventCount;
        double hardenedAnnualCost = hardenedCost.TotalCost * reducedEventCount;

        double avoidedCost = Math.Round(baseAnnualCost - hardenedAnnualCost, 2);
        double ratio = measure.ImplementationCost > 0 ? avoidedCost / measure.ImplementationCost : 0;
        double payback = avoidedCost > 0 ? measure.ImplementationCost / avoidedCost : double.PositiveInfinity;

        return new HardeningAssessment
        {
            MeasureName = measure.Name,
            BaseAnnualEventCount = annualEventCount,
            ReducedAnnualEventCount = Math.Round(reducedEventCount, 2),
            HardenedDurationMinutes = hardenedScenario.DurationMinutes,
            AvoidedAnnualCustomerMinutes = Math.Round(baseAnnualCustomerMinutes - hardenedAnnualCustomerMinutes, 2),
            AvoidedAnnualUnservedEnergyMWh = Math.Round(baseAnnualUnservedEnergy - hardenedAnnualUnservedEnergy, 4),
            AvoidedAnnualCost = avoidedCost,
            BenefitCostRatio = Math.Round(ratio, 4),
            SimplePaybackYears = double.IsPositiveInfinity(payback) ? payback : Math.Round(payback, 2),
        };
    }

    public static List<HardeningAssessment> RankMeasures(
        OutageImpactService.OutageScenario baseScenario,
        double annualEventCount,
        IEnumerable<HardeningMeasure> measures,
        IReadOnlyDictionary<OutageImpactService.CustomerClass, OutageCostService.InterruptionCostRate>? customRates = null)
    {
        return (measures ?? Array.Empty<HardeningMeasure>())
            .Select(measure => EvaluateMeasure(baseScenario, annualEventCount, measure, customRates))
            .OrderByDescending(assessment => assessment.BenefitCostRatio)
            .ThenByDescending(assessment => assessment.AvoidedAnnualCost)
            .ToList();
    }

    private static double NormalizePercent(double percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent inputs must be between 0 and 100.");

        return percent / 100.0;
    }
}