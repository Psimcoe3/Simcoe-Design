using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Ranks restoration blocks and packs them into cold-load-limited restoration stages.
/// </summary>
public static class ServiceRestorationService
{
    public enum RestorationPriority
    {
        Critical = 1,
        Essential = 2,
        Standard = 3,
        Deferrable = 4,
    }

    public record RestorationBlock
    {
        public string Name { get; init; } = string.Empty;
        public RestorationPriority Priority { get; init; } = RestorationPriority.Standard;
        public double NormalLoadKw { get; init; }
        public int CustomerCount { get; init; }
        public bool RequiresManualSwitching { get; init; }
    }

    public record RestorationStage
    {
        public int StageNumber { get; init; }
        public List<string> BlockNames { get; init; } = new();
        public double RestoredNormalLoadKw { get; init; }
        public double EstimatedPickupDemandKw { get; init; }
        public int CustomerCount { get; init; }
        public bool RequiresManualSwitching { get; init; }
    }

    public record ServiceRestorationPlan
    {
        public double PickupMultiplier { get; init; }
        public double SafeRestoreBlockKw { get; init; }
        public double RestoredNormalLoadKw { get; init; }
        public double DeferredNormalLoadKw { get; init; }
        public List<RestorationStage> Stages { get; init; } = new();
        public List<string> DeferredBlocks { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static double CalculatePickupDemand(double normalLoadKw, double pickupMultiplier)
    {
        if (normalLoadKw < 0 || pickupMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(normalLoadKw), "Load must be non-negative and multiplier must be positive.");

        return Math.Round(normalLoadKw * pickupMultiplier, 2);
    }

    public static List<RestorationBlock> RankBlocks(IEnumerable<RestorationBlock> blocks)
    {
        return (blocks ?? Array.Empty<RestorationBlock>())
            .OrderBy(block => (int)block.Priority)
            .ThenBy(block => block.RequiresManualSwitching)
            .ThenBy(block => block.NormalLoadKw)
            .ThenByDescending(block => block.CustomerCount)
            .ToList();
    }

    public static ServiceRestorationPlan CreateRestorationPlan(
        IEnumerable<RestorationBlock> blocks,
        double availableCapacityKw,
        ColdLoadPickupService.LoadMix loadMix,
        double outageHours)
    {
        if (availableCapacityKw < 0)
            throw new ArgumentOutOfRangeException(nameof(availableCapacityKw), "Available capacity must be non-negative.");

        double pickupMultiplier = ColdLoadPickupService.GetPickupMultiplier(loadMix, outageHours);
        double safeBlock = ColdLoadPickupService.CalculateSafeRestoreBlockKw(availableCapacityKw, pickupMultiplier);
        var rankedBlocks = RankBlocks(blocks);

        if (safeBlock <= 0)
        {
            return new ServiceRestorationPlan
            {
                PickupMultiplier = pickupMultiplier,
                SafeRestoreBlockKw = 0,
                DeferredNormalLoadKw = Math.Round(rankedBlocks.Sum(block => block.NormalLoadKw), 2),
                DeferredBlocks = rankedBlocks.Select(block => block.Name).ToList(),
                Issue = "No transfer capacity is available for restoration",
            };
        }

        var stages = new List<RestorationStage>();
        var deferred = new List<string>();
        var currentStageBlocks = new List<RestorationBlock>();
        double currentStageNormalKw = 0;
        int stageNumber = 1;

        foreach (var block in rankedBlocks)
        {
            if (block.NormalLoadKw > safeBlock)
            {
                deferred.Add(block.Name);
                continue;
            }

            if (currentStageNormalKw + block.NormalLoadKw > safeBlock && currentStageBlocks.Count > 0)
            {
                stages.Add(BuildStage(stageNumber++, currentStageBlocks, pickupMultiplier));
                currentStageBlocks = new List<RestorationBlock>();
                currentStageNormalKw = 0;
            }

            currentStageBlocks.Add(block);
            currentStageNormalKw += block.NormalLoadKw;
        }

        if (currentStageBlocks.Count > 0)
            stages.Add(BuildStage(stageNumber, currentStageBlocks, pickupMultiplier));

        double restoredLoad = stages.Sum(stage => stage.RestoredNormalLoadKw);
        double deferredLoad = rankedBlocks
            .Where(block => deferred.Contains(block.Name, StringComparer.OrdinalIgnoreCase))
            .Sum(block => block.NormalLoadKw);

        return new ServiceRestorationPlan
        {
            PickupMultiplier = pickupMultiplier,
            SafeRestoreBlockKw = safeBlock,
            RestoredNormalLoadKw = Math.Round(restoredLoad, 2),
            DeferredNormalLoadKw = Math.Round(deferredLoad, 2),
            Stages = stages,
            DeferredBlocks = deferred,
            Issue = deferred.Count == 0 ? null : "Some restoration blocks exceed the available cold-load-safe transfer block",
        };
    }

    private static RestorationStage BuildStage(int stageNumber, List<RestorationBlock> blocks, double pickupMultiplier)
    {
        double normalLoad = blocks.Sum(block => block.NormalLoadKw);
        return new RestorationStage
        {
            StageNumber = stageNumber,
            BlockNames = blocks.Select(block => block.Name).ToList(),
            RestoredNormalLoadKw = Math.Round(normalLoad, 2),
            EstimatedPickupDemandKw = CalculatePickupDemand(normalLoad, pickupMultiplier),
            CustomerCount = blocks.Sum(block => block.CustomerCount),
            RequiresManualSwitching = blocks.Any(block => block.RequiresManualSwitching),
        };
    }
}