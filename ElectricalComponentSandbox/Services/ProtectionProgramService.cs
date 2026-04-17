using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Aggregates protection-study findings into a program-level readiness report.
/// </summary>
public static class ProtectionProgramService
{
    public enum ProgramPriority
    {
        Low,
        Medium,
        High,
        Critical,
    }

    public record ProgramAction
    {
        public ProgramPriority Priority { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public record ProgramReport
    {
        public int ReadinessScore { get; init; }
        public int RelayCriticalCount { get; init; }
        public int CoordinationViolationCount { get; init; }
        public int DutyViolationCount { get; init; }
        public double AverageArcFlashReductionPercent { get; init; }
        public List<ProgramAction> Actions { get; init; } = new();
        public List<ProtectionUpgradePlannerService.UpgradeRecommendation> RecommendedUpgrades { get; init; } = new();
    }

    public static ProgramReport BuildReport(
        IEnumerable<RelaySettingsAuditService.RelayAuditResult> relayAudits,
        IEnumerable<CoordinationSweepService.SweepSummary> sweeps,
        InterruptingDutyAuditService.DutyAuditSummary dutySummary,
        IEnumerable<ArcFlashMitigationService.MitigationSummary> mitigations,
        IEnumerable<ProtectionUpgradePlannerService.UpgradeRecommendation> upgrades)
    {
        var relayAuditList = (relayAudits ?? Array.Empty<RelaySettingsAuditService.RelayAuditResult>()).ToList();
        var sweepList = (sweeps ?? Array.Empty<CoordinationSweepService.SweepSummary>()).ToList();
        var mitigationList = (mitigations ?? Array.Empty<ArcFlashMitigationService.MitigationSummary>()).ToList();
        var upgradeList = (upgrades ?? Array.Empty<ProtectionUpgradePlannerService.UpgradeRecommendation>())
            .OrderByDescending(item => item.PriorityScore)
            .Take(3)
            .ToList();

        dutySummary ??= new InterruptingDutyAuditService.DutyAuditSummary();

        int relayCriticalCount = relayAuditList.Sum(item => item.CriticalCount);
        int coordinationViolationCount = sweepList.Sum(item => item.Violations.Count);
        int dutyViolationCount = dutySummary.ViolationCount;
        double averageArcFlashReduction = mitigationList.Count == 0
            ? 0
            : Math.Round(mitigationList.Average(item => item.BestScenario.EnergyReductionPercent), 2);

        int score = 100
            - (relayCriticalCount * 10)
            - (coordinationViolationCount * 8)
            - (dutySummary.CriticalCount * 15)
            - ((dutyViolationCount - dutySummary.CriticalCount) * 8);

        var actions = BuildActions(relayCriticalCount, coordinationViolationCount, dutySummary, mitigationList, upgradeList);

        return new ProgramReport
        {
            ReadinessScore = Math.Max(score, 0),
            RelayCriticalCount = relayCriticalCount,
            CoordinationViolationCount = coordinationViolationCount,
            DutyViolationCount = dutyViolationCount,
            AverageArcFlashReductionPercent = averageArcFlashReduction,
            Actions = actions,
            RecommendedUpgrades = upgradeList,
        };
    }

    private static List<ProgramAction> BuildActions(
        int relayCriticalCount,
        int coordinationViolationCount,
        InterruptingDutyAuditService.DutyAuditSummary dutySummary,
        List<ArcFlashMitigationService.MitigationSummary> mitigations,
        List<ProtectionUpgradePlannerService.UpgradeRecommendation> upgrades)
    {
        var actions = new List<ProgramAction>();

        AddRelayAction(actions, relayCriticalCount);
        AddCoordinationAction(actions, coordinationViolationCount);
        AddDutyAction(actions, dutySummary);
        AddMitigationActions(actions, mitigations);
        AddUpgradeActions(actions, upgrades);
        return actions;
    }

    private static void AddRelayAction(List<ProgramAction> actions, int relayCriticalCount)
    {
        if (relayCriticalCount <= 0)
            return;

        actions.Add(new ProgramAction
        {
            Priority = ProgramPriority.High,
            Category = "Relay settings",
            Description = $"Resolve {relayCriticalCount} relay critical finding(s).",
        });
    }

    private static void AddCoordinationAction(List<ProgramAction> actions, int coordinationViolationCount)
    {
        if (coordinationViolationCount <= 0)
            return;

        actions.Add(new ProgramAction
        {
            Priority = ProgramPriority.High,
            Category = "Coordination",
            Description = $"Correct {coordinationViolationCount} coordination violation(s).",
        });
    }

    private static void AddDutyAction(List<ProgramAction> actions, InterruptingDutyAuditService.DutyAuditSummary dutySummary)
    {
        if (dutySummary.CriticalCount > 0)
        {
            actions.Add(new ProgramAction
            {
                Priority = ProgramPriority.Critical,
                Category = "Interrupting duty",
                Description = $"Replace or re-rate equipment at {dutySummary.CriticalCount} critical location(s).",
            });
            return;
        }

        if (dutySummary.ViolationCount <= 0)
            return;

        actions.Add(new ProgramAction
        {
            Priority = ProgramPriority.High,
            Category = "Interrupting duty",
            Description = $"Address {dutySummary.ViolationCount} interrupting-duty violation(s).",
        });
    }

    private static void AddMitigationActions(List<ProgramAction> actions, List<ArcFlashMitigationService.MitigationSummary> mitigations)
    {
        foreach (var mitigation in mitigations.Where(item => item.BestScenario.EnergyReductionPercent >= 20))
        {
            actions.Add(new ProgramAction
            {
                Priority = ProgramPriority.Medium,
                Category = "Arc flash",
                Description = $"Apply {mitigation.BestScenario.Name} at {mitigation.NodeId} to reduce incident energy by {mitigation.BestScenario.EnergyReductionPercent:F1}%.",
            });
        }
    }

    private static void AddUpgradeActions(List<ProgramAction> actions, List<ProtectionUpgradePlannerService.UpgradeRecommendation> upgrades)
    {
        foreach (var upgrade in upgrades)
        {
            actions.Add(new ProgramAction
            {
                Priority = upgrade.PriorityScore >= 80 ? ProgramPriority.High : ProgramPriority.Medium,
                Category = "Capital plan",
                Description = $"Advance {upgrade.Name}: {upgrade.Reason}.",
            });
        }
    }
}