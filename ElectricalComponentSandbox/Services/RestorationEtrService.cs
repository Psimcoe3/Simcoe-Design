using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Builds a customer-facing restoration timeline from switching, staged restoration, and repair clearance.
/// </summary>
public static class RestorationEtrService
{
    public record RestorationMilestone
    {
        public int StageNumber { get; init; }
        public List<string> BlockNames { get; init; } = new();
        public int CustomerCount { get; init; }
        public double RestoreMinute { get; init; }
        public bool RequiresManualSwitching { get; init; }
    }

    public record RestorationEtrPlan
    {
        public double SwitchingWindowMinutes { get; init; }
        public double RepairClearTimeMinutes { get; init; }
        public double FinalEtrMinutes { get; init; }
        public int CustomersRestoredByStages { get; init; }
        public List<RestorationMilestone> Milestones { get; init; } = new();
        public string? Issue { get; init; }
    }

    public static double EstimateSwitchingWindowMinutes(
        SwitchingSequenceService.SwitchingSequencePlan switchingPlan,
        double remoteStepMinutes = 2,
        double manualStepMinutes = 12)
    {
        if (remoteStepMinutes < 0 || manualStepMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(remoteStepMinutes), "Switching step durations must be non-negative.");

        if (!switchingPlan.IsValid || switchingPlan.Steps.Count == 0)
            return 0;

        double total = switchingPlan.Steps.Sum(step => step.IsRemoteControlled ? remoteStepMinutes : manualStepMinutes);
        return Math.Round(total, 2);
    }

    public static RestorationEtrPlan CreateRestorationTimeline(
        SwitchingSequenceService.SwitchingSequencePlan switchingPlan,
        ServiceRestorationService.ServiceRestorationPlan restorationPlan,
        CrewDispatchService.DispatchPlan dispatchPlan,
        double verificationMinutesPerStage = 10,
        double remoteStageMinutes = 5,
        double manualStageMinutes = 15)
    {
        if (verificationMinutesPerStage < 0 || remoteStageMinutes < 0 || manualStageMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(verificationMinutesPerStage), "Stage timing inputs must be non-negative.");

        double switchingWindow = EstimateSwitchingWindowMinutes(switchingPlan);
        if (!switchingPlan.IsValid)
        {
            return new RestorationEtrPlan
            {
                RepairClearTimeMinutes = dispatchPlan.EstimatedClearTimeMinutes,
                Issue = switchingPlan.Issue ?? "Switching plan is invalid",
            };
        }

        var milestones = new List<RestorationMilestone>();
        double currentMinute = switchingWindow;

        foreach (var stage in restorationPlan.Stages)
        {
            currentMinute += stage.RequiresManualSwitching ? manualStageMinutes : remoteStageMinutes;
            currentMinute += verificationMinutesPerStage;

            milestones.Add(new RestorationMilestone
            {
                StageNumber = stage.StageNumber,
                BlockNames = stage.BlockNames.ToList(),
                CustomerCount = stage.CustomerCount,
                RestoreMinute = Math.Round(currentMinute, 2),
                RequiresManualSwitching = stage.RequiresManualSwitching,
            });
        }

        double finalEtr = Math.Max(currentMinute, dispatchPlan.EstimatedClearTimeMinutes);
        return new RestorationEtrPlan
        {
            SwitchingWindowMinutes = switchingWindow,
            RepairClearTimeMinutes = Math.Round(dispatchPlan.EstimatedClearTimeMinutes, 2),
            FinalEtrMinutes = Math.Round(finalEtr, 2),
            CustomersRestoredByStages = milestones.Sum(milestone => milestone.CustomerCount),
            Milestones = milestones,
            Issue = restorationPlan.Issue,
        };
    }
}