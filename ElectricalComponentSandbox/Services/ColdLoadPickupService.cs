using System;
using System.Collections.Generic;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Estimates cold-load pickup and stages feeder restoration to respect available capacity.
/// </summary>
public static class ColdLoadPickupService
{
    public enum LoadMix
    {
        Residential,
        Commercial,
        Mixed,
        Industrial,
    }

    public record RestorationStage
    {
        public int StageNumber { get; init; }
        public double NormalLoadRestoredKw { get; init; }
        public double EstimatedPickupDemandKw { get; init; }
        public int DelayMinutes { get; init; }
    }

    public record ColdLoadPickupPlan
    {
        public double PickupMultiplier { get; init; }
        public double EstimatedInitialDemandKw { get; init; }
        public bool RequiresStagedRestore { get; init; }
        public List<RestorationStage> Stages { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static double GetPickupMultiplier(LoadMix loadMix, double outageHours)
    {
        if (outageHours < 0)
            throw new ArgumentOutOfRangeException(nameof(outageHours), "Outage duration must be non-negative.");

        double baseMultiplier = loadMix switch
        {
            LoadMix.Residential => 1.6,
            LoadMix.Commercial => 1.4,
            LoadMix.Mixed => 1.5,
            _ => 1.2,
        };

        double durationAdder = outageHours switch
        {
            < 1 => 0.0,
            < 4 => 0.15,
            < 8 => 0.3,
            _ => 0.45,
        };

        return Math.Round(baseMultiplier + durationAdder, 2);
    }

    public static double EstimateInitialDemandKw(double normalDemandKw, LoadMix loadMix, double outageHours)
    {
        if (normalDemandKw < 0)
            throw new ArgumentOutOfRangeException(nameof(normalDemandKw), "Demand must be non-negative.");

        return Math.Round(normalDemandKw * GetPickupMultiplier(loadMix, outageHours), 2);
    }

    public static double CalculateSafeRestoreBlockKw(double availableCapacityKw, double pickupMultiplier)
    {
        if (availableCapacityKw < 0 || pickupMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(availableCapacityKw), "Capacity must be non-negative and multiplier must be positive.");

        return Math.Round(availableCapacityKw / pickupMultiplier, 2);
    }

    public static ColdLoadPickupPlan CreateRestorationPlan(
        double normalDemandKw,
        double availableCapacityKw,
        LoadMix loadMix,
        double outageHours,
        int stageDelayMinutes = 15)
    {
        if (normalDemandKw < 0 || availableCapacityKw < 0 || stageDelayMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(normalDemandKw), "Cold-load inputs must be non-negative.");

        double multiplier = GetPickupMultiplier(loadMix, outageHours);
        double initialDemand = EstimateInitialDemandKw(normalDemandKw, loadMix, outageHours);
        double safeBlock = CalculateSafeRestoreBlockKw(availableCapacityKw, multiplier);

        if (normalDemandKw == 0)
        {
            return new ColdLoadPickupPlan
            {
                PickupMultiplier = multiplier,
                EstimatedInitialDemandKw = 0,
            };
        }

        if (safeBlock <= 0)
        {
            return new ColdLoadPickupPlan
            {
                PickupMultiplier = multiplier,
                EstimatedInitialDemandKw = initialDemand,
                Issue = "No available capacity exists for cold-load restoration",
            };
        }

        var stages = new List<RestorationStage>();
        double remaining = normalDemandKw;
        int stage = 1;

        while (remaining > 0.001)
        {
            double restoredKw = Math.Min(remaining, safeBlock);
            stages.Add(new RestorationStage
            {
                StageNumber = stage,
                NormalLoadRestoredKw = Math.Round(restoredKw, 2),
                EstimatedPickupDemandKw = Math.Round(restoredKw * multiplier, 2),
                DelayMinutes = (stage - 1) * stageDelayMinutes,
            });

            remaining -= restoredKw;
            stage++;
        }

        return new ColdLoadPickupPlan
        {
            PickupMultiplier = multiplier,
            EstimatedInitialDemandKw = initialDemand,
            RequiresStagedRestore = stages.Count > 1,
            Stages = stages,
        };
    }
}