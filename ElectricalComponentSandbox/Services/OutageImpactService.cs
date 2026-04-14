using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Quantifies outage impact using customers affected, critical-customer exposure, and unserved energy.
/// </summary>
public static class OutageImpactService
{
    public enum CustomerClass
    {
        Residential,
        Commercial,
        Industrial,
        Critical,
    }

    public record AffectedSegment
    {
        public CustomerClass CustomerClass { get; init; }
        public int CustomerCount { get; init; }
        public double AverageDemandKw { get; init; }
        public bool IsCriticalFacility { get; init; }
    }

    public record OutageScenario
    {
        public string Name { get; init; } = string.Empty;
        public double DurationMinutes { get; init; }
        public List<AffectedSegment> Segments { get; init; } = new();
    }

    public record OutageImpactSummary
    {
        public string ScenarioName { get; init; } = string.Empty;
        public int TotalCustomersAffected { get; init; }
        public int CriticalCustomersAffected { get; init; }
        public double CustomerMinutesInterrupted { get; init; }
        public double CriticalCustomerMinutesInterrupted { get; init; }
        public double UnservedEnergyMWh { get; init; }
        public double WeightedImpactScore { get; init; }
        public bool IsMomentary { get; init; }
    }

    public static double CalculateCustomerMinutesInterrupted(int customersAffected, double durationMinutes)
    {
        if (customersAffected < 0 || durationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(customersAffected), "Outage inputs must be non-negative.");

        return Math.Round(customersAffected * durationMinutes, 2);
    }

    public static double CalculateUnservedEnergyMWh(double averageDemandKw, double durationMinutes)
    {
        if (averageDemandKw < 0 || durationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(averageDemandKw), "Demand and duration must be non-negative.");

        return Math.Round(averageDemandKw * (durationMinutes / 60.0) / 1000.0, 4);
    }

    public static double GetImpactWeight(CustomerClass customerClass, bool isCriticalFacility)
    {
        if (isCriticalFacility)
            return 5.0;

        return customerClass switch
        {
            CustomerClass.Industrial => 3.0,
            CustomerClass.Commercial => 2.0,
            CustomerClass.Critical => 4.0,
            _ => 1.0,
        };
    }

    public static OutageImpactSummary AnalyzeScenario(OutageScenario scenario)
    {
        double customerMinutes = 0;
        double criticalCustomerMinutes = 0;
        double unservedEnergyMwh = 0;
        double weightedImpact = 0;
        int totalCustomers = 0;
        int criticalCustomers = 0;

        foreach (var segment in scenario.Segments ?? Enumerable.Empty<AffectedSegment>())
        {
            totalCustomers += segment.CustomerCount;
            if (segment.IsCriticalFacility || segment.CustomerClass == CustomerClass.Critical)
                criticalCustomers += segment.CustomerCount;

            double segmentMinutes = CalculateCustomerMinutesInterrupted(segment.CustomerCount, scenario.DurationMinutes);
            customerMinutes += segmentMinutes;

            if (segment.IsCriticalFacility || segment.CustomerClass == CustomerClass.Critical)
                criticalCustomerMinutes += segmentMinutes;

            double segmentDemandKw = segment.CustomerCount * segment.AverageDemandKw;
            unservedEnergyMwh += CalculateUnservedEnergyMWh(segmentDemandKw, scenario.DurationMinutes);
            weightedImpact += segmentMinutes * GetImpactWeight(segment.CustomerClass, segment.IsCriticalFacility);
        }

        return new OutageImpactSummary
        {
            ScenarioName = scenario.Name,
            TotalCustomersAffected = totalCustomers,
            CriticalCustomersAffected = criticalCustomers,
            CustomerMinutesInterrupted = Math.Round(customerMinutes, 2),
            CriticalCustomerMinutesInterrupted = Math.Round(criticalCustomerMinutes, 2),
            UnservedEnergyMWh = Math.Round(unservedEnergyMwh, 4),
            WeightedImpactScore = Math.Round(weightedImpact, 2),
            IsMomentary = scenario.DurationMinutes <= 5,
        };
    }
}