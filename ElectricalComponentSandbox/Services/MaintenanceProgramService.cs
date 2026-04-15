using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Compares maintenance portfolios by translating interventions into avoided annual risk cost.
/// </summary>
public static class MaintenanceProgramService
{
    public record AssetIntervention
    {
        public string AssetName { get; init; } = string.Empty;
        public ReplacementPrioritizationService.ReplacementCandidate BaselineCandidate { get; init; } = new();
        public bool PerformsPreventiveMaintenance { get; init; }
        public bool AddsCriticalSpare { get; init; }
        public bool ReplacesAsset { get; init; }
        public double InterventionCost { get; init; }
    }

    public record AssetInterventionResult
    {
        public string AssetName { get; init; } = string.Empty;
        public double BaselineAnnualRiskCost { get; init; }
        public double ImprovedAnnualRiskCost { get; init; }
        public double AvoidedAnnualRiskCost { get; init; }
    }

    public record MaintenanceProgramOption
    {
        public string Name { get; init; } = string.Empty;
        public List<AssetIntervention> Interventions { get; init; } = new();
    }

    public record MaintenanceProgramEvaluation
    {
        public string ProgramName { get; init; } = string.Empty;
        public double AnnualProgramCost { get; init; }
        public double BaselineAnnualRiskCost { get; init; }
        public double ImprovedAnnualRiskCost { get; init; }
        public double AvoidedAnnualRiskCost { get; init; }
        public double NetAnnualBenefit { get; init; }
        public double BenefitCostRatio { get; init; }
        public List<AssetInterventionResult> AssetResults { get; init; } = new();
    }

    public record MaintenanceProgramComparison
    {
        public string BaselineProgramName { get; init; } = string.Empty;
        public string ImprovedProgramName { get; init; } = string.Empty;
        public double AvoidedRiskDelta { get; init; }
        public double NetBenefitDelta { get; init; }
        public double BenefitCostRatioDelta { get; init; }
    }

    public static ReplacementPrioritizationService.ReplacementCandidate ApplyIntervention(AssetIntervention intervention)
    {
        var baseline = intervention.BaselineCandidate;
        var condition = baseline.ConditionAssessment;
        var failureImpact = baseline.FailureImpact;
        double annualMaintenanceCost = baseline.AnnualMaintenanceCost;

        if (intervention.PerformsPreventiveMaintenance)
        {
            condition = condition with
            {
                HealthIndex = Math.Round(condition.HealthIndex * 0.8, 2),
                FailureProbability = Math.Round(condition.FailureProbability * 0.65, 4),
            };
            annualMaintenanceCost = Math.Round(annualMaintenanceCost * 0.8, 2);
        }

        if (intervention.AddsCriticalSpare)
        {
            failureImpact = failureImpact with
            {
                TotalCost = Math.Round(failureImpact.TotalCost * 0.7, 2),
            };
        }

        if (intervention.ReplacesAsset)
        {
            condition = condition with
            {
                HealthIndex = 10,
                FailureProbability = 0.03,
            };
            failureImpact = failureImpact with
            {
                TotalCost = Math.Round(failureImpact.TotalCost * 0.2, 2),
            };
            annualMaintenanceCost = Math.Round(annualMaintenanceCost * 0.25, 2);
        }

        return baseline with
        {
            ConditionAssessment = condition,
            FailureImpact = failureImpact,
            AnnualMaintenanceCost = annualMaintenanceCost,
            HasComplianceIssue = intervention.ReplacesAsset ? false : baseline.HasComplianceIssue,
            HasSpareConstraint = intervention.AddsCriticalSpare || intervention.ReplacesAsset ? false : baseline.HasSpareConstraint,
        };
    }

    public static MaintenanceProgramEvaluation EvaluateProgram(MaintenanceProgramOption option)
    {
        var assetResults = new List<AssetInterventionResult>();

        foreach (var intervention in option.Interventions ?? Enumerable.Empty<AssetIntervention>())
        {
            double baselineRisk = ReplacementPrioritizationService.CalculateAnnualRiskCost(intervention.BaselineCandidate);
            var improvedCandidate = ApplyIntervention(intervention);
            double improvedRisk = ReplacementPrioritizationService.CalculateAnnualRiskCost(improvedCandidate);

            assetResults.Add(new AssetInterventionResult
            {
                AssetName = intervention.AssetName,
                BaselineAnnualRiskCost = baselineRisk,
                ImprovedAnnualRiskCost = improvedRisk,
                AvoidedAnnualRiskCost = Math.Round(baselineRisk - improvedRisk, 2),
            });
        }

        double annualProgramCost = Math.Round((option.Interventions ?? new List<AssetIntervention>()).Sum(intervention => intervention.InterventionCost), 2);
        double baselineAnnualRiskCost = Math.Round(assetResults.Sum(result => result.BaselineAnnualRiskCost), 2);
        double improvedAnnualRiskCost = Math.Round(assetResults.Sum(result => result.ImprovedAnnualRiskCost), 2);
        double avoidedRisk = Math.Round(assetResults.Sum(result => result.AvoidedAnnualRiskCost), 2);
        double netBenefit = Math.Round(avoidedRisk - annualProgramCost, 2);
        double ratio = annualProgramCost > 0 ? avoidedRisk / annualProgramCost : 0;

        return new MaintenanceProgramEvaluation
        {
            ProgramName = option.Name,
            AnnualProgramCost = annualProgramCost,
            BaselineAnnualRiskCost = baselineAnnualRiskCost,
            ImprovedAnnualRiskCost = improvedAnnualRiskCost,
            AvoidedAnnualRiskCost = avoidedRisk,
            NetAnnualBenefit = netBenefit,
            BenefitCostRatio = Math.Round(ratio, 4),
            AssetResults = assetResults,
        };
    }

    public static MaintenanceProgramComparison ComparePrograms(MaintenanceProgramOption baseline, MaintenanceProgramOption improved)
    {
        var baselineEvaluation = EvaluateProgram(baseline);
        var improvedEvaluation = EvaluateProgram(improved);

        return new MaintenanceProgramComparison
        {
            BaselineProgramName = baseline.Name,
            ImprovedProgramName = improved.Name,
            AvoidedRiskDelta = Math.Round(improvedEvaluation.AvoidedAnnualRiskCost - baselineEvaluation.AvoidedAnnualRiskCost, 2),
            NetBenefitDelta = Math.Round(improvedEvaluation.NetAnnualBenefit - baselineEvaluation.NetAnnualBenefit, 2),
            BenefitCostRatioDelta = Math.Round(improvedEvaluation.BenefitCostRatio - baselineEvaluation.BenefitCostRatio, 4),
        };
    }

    public static List<MaintenanceProgramEvaluation> RankPrograms(IEnumerable<MaintenanceProgramOption> programs)
    {
        return (programs ?? Array.Empty<MaintenanceProgramOption>())
            .Select(EvaluateProgram)
            .OrderByDescending(program => program.NetAnnualBenefit)
            .ThenByDescending(program => program.BenefitCostRatio)
            .ToList();
    }
}