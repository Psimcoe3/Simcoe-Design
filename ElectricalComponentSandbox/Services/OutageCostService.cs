using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Estimates financial outage impact using customer interruption cost and unserved energy cost.
/// </summary>
public static class OutageCostService
{
    public record InterruptionCostRate
    {
        public double DollarsPerCustomerHour { get; init; }
        public double DollarsPerUnservedKWh { get; init; }
        public double CriticalFacilityMultiplier { get; init; } = 3.0;
    }

    public record SegmentCostDetail
    {
        public OutageImpactService.CustomerClass CustomerClass { get; init; }
        public int CustomerCount { get; init; }
        public double CustomerInterruptionCost { get; init; }
        public double EnergyNotServedCost { get; init; }
        public double TotalCost { get; init; }
        public bool IsCriticalFacility { get; init; }
    }

    public record OutageCostAnalysis
    {
        public string ScenarioName { get; init; } = string.Empty;
        public double CustomerInterruptionCost { get; init; }
        public double EnergyNotServedCost { get; init; }
        public double TotalCost { get; init; }
        public string HighestCostClass { get; init; } = string.Empty;
        public OutageImpactService.OutageImpactSummary ImpactSummary { get; init; } = new();
        public List<SegmentCostDetail> SegmentDetails { get; init; } = new();
    }

    public static InterruptionCostRate GetDefaultRate(OutageImpactService.CustomerClass customerClass)
    {
        return customerClass switch
        {
            OutageImpactService.CustomerClass.Commercial => new InterruptionCostRate { DollarsPerCustomerHour = 150, DollarsPerUnservedKWh = 6 },
            OutageImpactService.CustomerClass.Industrial => new InterruptionCostRate { DollarsPerCustomerHour = 450, DollarsPerUnservedKWh = 12 },
            OutageImpactService.CustomerClass.Critical => new InterruptionCostRate { DollarsPerCustomerHour = 900, DollarsPerUnservedKWh = 18, CriticalFacilityMultiplier = 4.0 },
            _ => new InterruptionCostRate { DollarsPerCustomerHour = 15, DollarsPerUnservedKWh = 2 },
        };
    }

    public static double CalculateCustomerInterruptionCost(
        int customerCount,
        double durationMinutes,
        InterruptionCostRate rate,
        bool isCriticalFacility)
    {
        if (customerCount < 0 || durationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(customerCount), "Customer count and outage duration must be non-negative.");

        double hours = durationMinutes / 60.0;
        double multiplier = isCriticalFacility ? rate.CriticalFacilityMultiplier : 1.0;
        return Math.Round(customerCount * hours * rate.DollarsPerCustomerHour * multiplier, 2);
    }

    public static OutageCostAnalysis EstimateScenarioCost(
        OutageImpactService.OutageScenario scenario,
        IReadOnlyDictionary<OutageImpactService.CustomerClass, InterruptionCostRate>? customRates = null)
    {
        var impactSummary = OutageImpactService.AnalyzeScenario(scenario);
        var details = new List<SegmentCostDetail>();

        foreach (var segment in scenario.Segments ?? Enumerable.Empty<OutageImpactService.AffectedSegment>())
        {
            InterruptionCostRate rate = customRates is not null && customRates.TryGetValue(segment.CustomerClass, out var customRate)
                ? customRate
                : GetDefaultRate(segment.CustomerClass);

            double customerCost = CalculateCustomerInterruptionCost(
                segment.CustomerCount,
                scenario.DurationMinutes,
                rate,
                segment.IsCriticalFacility);
            double segmentDemandKw = segment.CustomerCount * segment.AverageDemandKw;
            double energyKwh = Math.Round(segmentDemandKw * (scenario.DurationMinutes / 60.0), 2);
            double energyCost = Math.Round(energyKwh * rate.DollarsPerUnservedKWh, 2);

            details.Add(new SegmentCostDetail
            {
                CustomerClass = segment.CustomerClass,
                CustomerCount = segment.CustomerCount,
                CustomerInterruptionCost = customerCost,
                EnergyNotServedCost = energyCost,
                TotalCost = Math.Round(customerCost + energyCost, 2),
                IsCriticalFacility = segment.IsCriticalFacility,
            });
        }

        double customerInterruptionCost = details.Sum(detail => detail.CustomerInterruptionCost);
        double energyNotServedCost = details.Sum(detail => detail.EnergyNotServedCost);
        var highestCostDetail = details.OrderByDescending(detail => detail.TotalCost).FirstOrDefault();

        return new OutageCostAnalysis
        {
            ScenarioName = scenario.Name,
            CustomerInterruptionCost = Math.Round(customerInterruptionCost, 2),
            EnergyNotServedCost = Math.Round(energyNotServedCost, 2),
            TotalCost = Math.Round(customerInterruptionCost + energyNotServedCost, 2),
            HighestCostClass = highestCostDetail?.CustomerClass.ToString() ?? string.Empty,
            ImpactSummary = impactSummary,
            SegmentDetails = details,
        };
    }
}