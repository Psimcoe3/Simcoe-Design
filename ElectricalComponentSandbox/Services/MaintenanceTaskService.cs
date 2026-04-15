using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Ranks and fits maintenance work into constrained labor and outage-window budgets.
/// </summary>
public static class MaintenanceTaskService
{
    public record MaintenanceCandidate
    {
        public string AssetName { get; init; } = string.Empty;
        public AssetConditionService.AssetConditionAssessment ConditionAssessment { get; init; } = new();
        public InspectionIntervalService.InspectionRecommendation InspectionRecommendation { get; init; } = new();
        public double LaborHours { get; init; }
        public double MaterialCost { get; init; }
        public int CustomersExposed { get; init; }
        public bool RequiresShutdown { get; init; }
    }

    public record ScheduledMaintenanceTask
    {
        public string AssetName { get; init; } = string.Empty;
        public double PriorityScore { get; init; }
        public double LaborHours { get; init; }
        public bool RequiresShutdown { get; init; }
        public int CustomersRiskAddressed { get; init; }
    }

    public record MaintenancePlan
    {
        public List<ScheduledMaintenanceTask> ScheduledTasks { get; init; } = new();
        public List<string> DeferredAssets { get; init; } = new();
        public double UsedLaborHours { get; init; }
        public double DeferredLaborHours { get; init; }
        public int ShutdownWindowsUsed { get; init; }
        public int CustomersRiskAddressed { get; init; }
        public string? Issue { get; init; }
    }

    public static double CalculatePriorityScore(MaintenanceCandidate candidate)
    {
        double healthComponent = candidate.ConditionAssessment.HealthIndex * 0.55;
        double inspectionComponent = Math.Max(0, 24 - candidate.InspectionRecommendation.IntervalMonths) * 1.5;
        double customerComponent = Math.Min(candidate.CustomersExposed, 5000) / 50.0;
        double shutdownPenalty = candidate.RequiresShutdown ? 5.0 : 0.0;

        return Math.Round(healthComponent + inspectionComponent + customerComponent - shutdownPenalty, 2);
    }

    public static List<MaintenanceCandidate> RankCandidates(IEnumerable<MaintenanceCandidate> candidates)
    {
        return (candidates ?? Array.Empty<MaintenanceCandidate>())
            .OrderByDescending(CalculatePriorityScore)
            .ThenBy(candidate => candidate.LaborHours)
            .ThenByDescending(candidate => candidate.CustomersExposed)
            .ToList();
    }

    public static MaintenancePlan CreatePlan(
        IEnumerable<MaintenanceCandidate> candidates,
        double laborHourBudget,
        int shutdownWindowBudget)
    {
        if (laborHourBudget < 0 || shutdownWindowBudget < 0)
            throw new ArgumentOutOfRangeException(nameof(laborHourBudget), "Labor and shutdown budgets must be non-negative.");

        var ranked = RankCandidates(candidates);
        var scheduled = new List<ScheduledMaintenanceTask>();
        var deferred = new List<string>();
        double usedLaborHours = 0;
        double deferredLaborHours = 0;
        int shutdownWindowsUsed = 0;

        foreach (var candidate in ranked)
        {
            bool exceedsLabor = usedLaborHours + candidate.LaborHours > laborHourBudget;
            bool exceedsShutdowns = candidate.RequiresShutdown && shutdownWindowsUsed + 1 > shutdownWindowBudget;

            if (exceedsLabor || exceedsShutdowns)
            {
                deferred.Add(candidate.AssetName);
                deferredLaborHours += candidate.LaborHours;
                continue;
            }

            usedLaborHours += candidate.LaborHours;
            if (candidate.RequiresShutdown)
                shutdownWindowsUsed++;

            scheduled.Add(new ScheduledMaintenanceTask
            {
                AssetName = candidate.AssetName,
                PriorityScore = CalculatePriorityScore(candidate),
                LaborHours = candidate.LaborHours,
                RequiresShutdown = candidate.RequiresShutdown,
                CustomersRiskAddressed = candidate.CustomersExposed,
            });
        }

        return new MaintenancePlan
        {
            ScheduledTasks = scheduled,
            DeferredAssets = deferred,
            UsedLaborHours = Math.Round(usedLaborHours, 2),
            DeferredLaborHours = Math.Round(deferredLaborHours, 2),
            ShutdownWindowsUsed = shutdownWindowsUsed,
            CustomersRiskAddressed = scheduled.Sum(task => task.CustomersRiskAddressed),
            Issue = deferred.Count == 0 ? null : "Some maintenance work was deferred due to labor or outage-window constraints",
        };
    }
}