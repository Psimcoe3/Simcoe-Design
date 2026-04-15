using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Ranks asset replacements by annualized risk and fits them into a constrained capital plan.
/// </summary>
public static class ReplacementPrioritizationService
{
    public enum PriorityBand
    {
        Monitor,
        Plan,
        Urgent,
        Immediate,
    }

    public record ReplacementCandidate
    {
        public string AssetName { get; init; } = string.Empty;
        public AssetConditionService.AssetConditionAssessment ConditionAssessment { get; init; } = new();
        public OutageCostService.OutageCostAnalysis FailureImpact { get; init; } = new();
        public double ReplacementCost { get; init; }
        public double AnnualMaintenanceCost { get; init; }
        public bool HasComplianceIssue { get; init; }
        public bool HasSpareConstraint { get; init; }
    }

    public record ReplacementAssessment
    {
        public string AssetName { get; init; } = string.Empty;
        public double AnnualRiskCost { get; init; }
        public double PriorityScore { get; init; }
        public double BenefitCostRatio { get; init; }
        public PriorityBand PriorityBand { get; init; }
        public double ReplacementCost { get; init; }
    }

    public record CapitalReplacementPlan
    {
        public List<ReplacementAssessment> SelectedAssets { get; init; } = new();
        public List<string> DeferredAssets { get; init; } = new();
        public double UsedBudget { get; init; }
        public double DeferredAnnualRiskCost { get; init; }
        public string? Issue { get; init; }
    }

    public static double CalculateAnnualRiskCost(ReplacementCandidate candidate)
    {
        double outageRiskCost = candidate.ConditionAssessment.FailureProbability * candidate.FailureImpact.TotalCost;
        return Math.Round(outageRiskCost + candidate.AnnualMaintenanceCost, 2);
    }

    public static double CalculatePriorityScore(ReplacementCandidate candidate)
    {
        double riskCost = CalculateAnnualRiskCost(candidate);
        double healthComponent = candidate.ConditionAssessment.HealthIndex * 0.5;
        double riskComponent = Math.Min(riskCost / 1000.0, 200) * 0.4;
        double complianceComponent = candidate.HasComplianceIssue ? 15 : 0;
        double spareComponent = candidate.HasSpareConstraint ? 10 : 0;

        return Math.Round(healthComponent + riskComponent + complianceComponent + spareComponent, 2);
    }

    public static PriorityBand GetPriorityBand(double priorityScore)
    {
        return priorityScore switch
        {
            < 40 => PriorityBand.Monitor,
            < 70 => PriorityBand.Plan,
            < 100 => PriorityBand.Urgent,
            _ => PriorityBand.Immediate,
        };
    }

    public static ReplacementAssessment AssessCandidate(ReplacementCandidate candidate)
    {
        double annualRiskCost = CalculateAnnualRiskCost(candidate);
        double priorityScore = CalculatePriorityScore(candidate);
        double benefitCostRatio = candidate.ReplacementCost > 0 ? annualRiskCost / candidate.ReplacementCost : 0;

        return new ReplacementAssessment
        {
            AssetName = candidate.AssetName,
            AnnualRiskCost = annualRiskCost,
            PriorityScore = priorityScore,
            BenefitCostRatio = Math.Round(benefitCostRatio, 4),
            PriorityBand = GetPriorityBand(priorityScore),
            ReplacementCost = candidate.ReplacementCost,
        };
    }

    public static List<ReplacementAssessment> RankCandidates(IEnumerable<ReplacementCandidate> candidates)
    {
        return (candidates ?? Array.Empty<ReplacementCandidate>())
            .Select(AssessCandidate)
            .OrderByDescending(candidate => candidate.PriorityScore)
            .ThenByDescending(candidate => candidate.BenefitCostRatio)
            .ThenBy(candidate => candidate.ReplacementCost)
            .ToList();
    }

    public static CapitalReplacementPlan CreateCapitalPlan(IEnumerable<ReplacementCandidate> candidates, double capitalBudget)
    {
        if (capitalBudget < 0)
            throw new ArgumentOutOfRangeException(nameof(capitalBudget), "Capital budget must be non-negative.");

        var ranked = RankCandidates(candidates);
        var selected = new List<ReplacementAssessment>();
        var deferred = new List<string>();
        double usedBudget = 0;
        double deferredAnnualRiskCost = 0;

        foreach (var candidate in ranked)
        {
            if (usedBudget + candidate.ReplacementCost > capitalBudget)
            {
                deferred.Add(candidate.AssetName);
                deferredAnnualRiskCost += candidate.AnnualRiskCost;
                continue;
            }

            usedBudget += candidate.ReplacementCost;
            selected.Add(candidate);
        }

        return new CapitalReplacementPlan
        {
            SelectedAssets = selected,
            DeferredAssets = deferred,
            UsedBudget = Math.Round(usedBudget, 2),
            DeferredAnnualRiskCost = Math.Round(deferredAnnualRiskCost, 2),
            Issue = deferred.Count == 0 ? null : "Some replacement candidates were deferred due to capital budget limits",
        };
    }
}