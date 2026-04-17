using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Prioritizes protection upgrades using avoided-risk and cost weighting.
/// </summary>
public static class ProtectionUpgradePlannerService
{
    public enum UpgradeType
    {
        SettingsChange,
        BreakerReplacement,
        CurrentLimitingDevice,
        MaintenanceSwitch,
        CtReplacement,
    }

    public record UpgradeCandidate
    {
        public string Name { get; init; } = string.Empty;
        public UpgradeType Type { get; init; }
        public double EstimatedCost { get; init; }
        public int RelayCriticalFindingsResolved { get; init; }
        public int RelayWarningFindingsResolved { get; init; }
        public int CoordinationViolationsResolved { get; init; }
        public int DutySeverityLevelsReduced { get; init; }
        public double ArcFlashReductionPercent { get; init; }
    }

    public record UpgradeRecommendation
    {
        public string Name { get; init; } = string.Empty;
        public UpgradeType Type { get; init; }
        public double PriorityScore { get; init; }
        public double BenefitCostRatio { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public static double CalculatePriorityScore(UpgradeCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        double score = (candidate.RelayCriticalFindingsResolved * 18.0)
            + (candidate.RelayWarningFindingsResolved * 6.0)
            + (candidate.CoordinationViolationsResolved * 14.0)
            + (candidate.DutySeverityLevelsReduced * 20.0)
            + (candidate.ArcFlashReductionPercent * 0.6);

        return Math.Round(score, 2);
    }

    public static double CalculateBenefitCostRatio(UpgradeCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.EstimatedCost <= 0)
            return CalculatePriorityScore(candidate);

        return Math.Round(CalculatePriorityScore(candidate) / candidate.EstimatedCost, 4);
    }

    public static List<UpgradeRecommendation> RankCandidates(IEnumerable<UpgradeCandidate> candidates)
    {
        return (candidates ?? Array.Empty<UpgradeCandidate>())
            .Select(candidate => new UpgradeRecommendation
            {
                Name = candidate.Name,
                Type = candidate.Type,
                PriorityScore = CalculatePriorityScore(candidate),
                BenefitCostRatio = CalculateBenefitCostRatio(candidate),
                Reason = BuildReason(candidate),
            })
            .OrderByDescending(recommendation => recommendation.PriorityScore)
            .ThenByDescending(recommendation => recommendation.BenefitCostRatio)
            .ToList();
    }

    public static UpgradeCandidate CreateSettingsChangeCandidate(
        string name,
        RelaySettingsAuditService.RelayAuditResult relayAudit,
        CoordinationSweepService.SweepSummary sweepSummary,
        double estimatedCost,
        double arcFlashReductionPercent = 0)
    {
        ArgumentNullException.ThrowIfNull(relayAudit);
        ArgumentNullException.ThrowIfNull(sweepSummary);

        return new UpgradeCandidate
        {
            Name = name,
            Type = UpgradeType.SettingsChange,
            EstimatedCost = estimatedCost,
            RelayCriticalFindingsResolved = relayAudit.CriticalCount,
            RelayWarningFindingsResolved = relayAudit.WarningCount,
            CoordinationViolationsResolved = sweepSummary.Violations.Count,
            DutySeverityLevelsReduced = 0,
            ArcFlashReductionPercent = arcFlashReductionPercent,
        };
    }

    public static UpgradeCandidate CreateEquipmentUpgradeCandidate(
        string name,
        InterruptingDutyAuditService.DutyExposure exposure,
        ArcFlashMitigationService.ScenarioResult? mitigation,
        double estimatedCost,
        UpgradeType type = UpgradeType.BreakerReplacement)
    {
        ArgumentNullException.ThrowIfNull(exposure);

        return new UpgradeCandidate
        {
            Name = name,
            Type = type,
            EstimatedCost = estimatedCost,
            RelayCriticalFindingsResolved = 0,
            RelayWarningFindingsResolved = 0,
            CoordinationViolationsResolved = 0,
            DutySeverityLevelsReduced = exposure.Severity switch
            {
                InterruptingDutyAuditService.DutySeverity.Critical => 3,
                InterruptingDutyAuditService.DutySeverity.High => 2,
                InterruptingDutyAuditService.DutySeverity.Moderate => 1,
                _ => 0,
            },
            ArcFlashReductionPercent = mitigation?.EnergyReductionPercent ?? 0,
        };
    }

    private static string BuildReason(UpgradeCandidate candidate)
    {
        var reasons = new List<string>();
        if (candidate.RelayCriticalFindingsResolved > 0)
            reasons.Add($"resolves {candidate.RelayCriticalFindingsResolved} relay critical finding(s)");
        if (candidate.CoordinationViolationsResolved > 0)
            reasons.Add($"eliminates {candidate.CoordinationViolationsResolved} coordination violation(s)");
        if (candidate.DutySeverityLevelsReduced > 0)
            reasons.Add($"reduces interrupting-duty risk by {candidate.DutySeverityLevelsReduced} level(s)");
        if (candidate.ArcFlashReductionPercent > 0)
            reasons.Add($"cuts incident energy by {candidate.ArcFlashReductionPercent:F1}%");

        return reasons.Count == 0
            ? "limited quantified risk reduction"
            : string.Join(", ", reasons);
    }
}