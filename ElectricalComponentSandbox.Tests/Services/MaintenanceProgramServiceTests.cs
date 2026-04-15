using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MaintenanceProgramServiceTests
{
    private static ReplacementPrioritizationService.ReplacementCandidate CreateBaselineCandidate(string name, double failureProbability, double failureCost, double annualMaintenanceCost = 5000) => new()
    {
        AssetName = name,
        ConditionAssessment = new AssetConditionService.AssetConditionAssessment
        {
            AssetName = name,
            HealthIndex = 80,
            FailureProbability = failureProbability,
        },
        FailureImpact = new OutageCostService.OutageCostAnalysis { ScenarioName = name, TotalCost = failureCost },
        ReplacementCost = 200000,
        AnnualMaintenanceCost = annualMaintenanceCost,
        HasComplianceIssue = true,
        HasSpareConstraint = true,
    };

    [Fact]
    public void ApplyIntervention_PreventiveMaintenanceReducesFailureProbability()
    {
        var result = MaintenanceProgramService.ApplyIntervention(new MaintenanceProgramService.AssetIntervention
        {
            AssetName = "A",
            BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
            PerformsPreventiveMaintenance = true,
        });

        Assert.True(result.ConditionAssessment.FailureProbability < 0.2);
    }

    [Fact]
    public void ApplyIntervention_CriticalSpareReducesFailureImpact()
    {
        var result = MaintenanceProgramService.ApplyIntervention(new MaintenanceProgramService.AssetIntervention
        {
            AssetName = "A",
            BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
            AddsCriticalSpare = true,
        });

        Assert.True(result.FailureImpact.TotalCost < 100000);
        Assert.False(result.HasSpareConstraint);
    }

    [Fact]
    public void ApplyIntervention_ReplacementProducesLowestResidualRisk()
    {
        var baseline = CreateBaselineCandidate("A", 0.2, 100000);
        var maintained = MaintenanceProgramService.ApplyIntervention(new MaintenanceProgramService.AssetIntervention
        {
            AssetName = "A",
            BaselineCandidate = baseline,
            PerformsPreventiveMaintenance = true,
        });
        var replaced = MaintenanceProgramService.ApplyIntervention(new MaintenanceProgramService.AssetIntervention
        {
            AssetName = "A",
            BaselineCandidate = baseline,
            ReplacesAsset = true,
        });

        Assert.True(ReplacementPrioritizationService.CalculateAnnualRiskCost(replaced) < ReplacementPrioritizationService.CalculateAnnualRiskCost(maintained));
    }

    [Fact]
    public void EvaluateProgram_SumsAvoidedRiskAndProgramCost()
    {
        var result = MaintenanceProgramService.EvaluateProgram(new MaintenanceProgramService.MaintenanceProgramOption
        {
            Name = "PM Plan",
            Interventions =
            {
                new MaintenanceProgramService.AssetIntervention
                {
                    AssetName = "A",
                    BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                    PerformsPreventiveMaintenance = true,
                    InterventionCost = 4000,
                },
            },
        });

        Assert.Equal(4000, result.AnnualProgramCost);
        Assert.True(result.AvoidedAnnualRiskCost > 0);
        Assert.Single(result.AssetResults);
    }

    [Fact]
    public void EvaluateProgram_NetBenefitCanBeNegativeWhenProgramIsExpensive()
    {
        var result = MaintenanceProgramService.EvaluateProgram(new MaintenanceProgramService.MaintenanceProgramOption
        {
            Name = "Expensive",
            Interventions =
            {
                new MaintenanceProgramService.AssetIntervention
                {
                    AssetName = "A",
                    BaselineCandidate = CreateBaselineCandidate("A", 0.05, 20000),
                    PerformsPreventiveMaintenance = true,
                    InterventionCost = 50000,
                },
            },
        });

        Assert.True(result.NetAnnualBenefit < 0);
    }

    [Fact]
    public void ComparePrograms_ShowsImprovedProgramHasHigherAvoidedRisk()
    {
        var baseline = new MaintenanceProgramService.MaintenanceProgramOption
        {
            Name = "Base",
            Interventions =
            {
                new MaintenanceProgramService.AssetIntervention
                {
                    AssetName = "A",
                    BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                    InterventionCost = 0,
                },
            },
        };
        var improved = new MaintenanceProgramService.MaintenanceProgramOption
        {
            Name = "Improved",
            Interventions =
            {
                new MaintenanceProgramService.AssetIntervention
                {
                    AssetName = "A",
                    BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                    PerformsPreventiveMaintenance = true,
                    AddsCriticalSpare = true,
                    InterventionCost = 6000,
                },
            },
        };

        var comparison = MaintenanceProgramService.ComparePrograms(baseline, improved);

        Assert.True(comparison.AvoidedRiskDelta > 0);
    }

    [Fact]
    public void RankPrograms_OrdersByNetAnnualBenefit()
    {
        var ranked = MaintenanceProgramService.RankPrograms(new[]
        {
            new MaintenanceProgramService.MaintenanceProgramOption
            {
                Name = "Minimal",
                Interventions =
                {
                    new MaintenanceProgramService.AssetIntervention
                    {
                        AssetName = "A",
                        BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                        PerformsPreventiveMaintenance = true,
                        InterventionCost = 3000,
                    },
                },
            },
            new MaintenanceProgramService.MaintenanceProgramOption
            {
                Name = "Replacement",
                Interventions =
                {
                    new MaintenanceProgramService.AssetIntervention
                    {
                        AssetName = "A",
                        BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                        ReplacesAsset = true,
                        InterventionCost = 150000,
                    },
                },
            },
        });

        Assert.Equal("Minimal", ranked[0].ProgramName);
    }

    [Fact]
    public void EvaluateProgram_ZeroCostProgramHasZeroBenefitRatio()
    {
        var result = MaintenanceProgramService.EvaluateProgram(new MaintenanceProgramService.MaintenanceProgramOption
        {
            Name = "No Action",
            Interventions =
            {
                new MaintenanceProgramService.AssetIntervention
                {
                    AssetName = "A",
                    BaselineCandidate = CreateBaselineCandidate("A", 0.2, 100000),
                },
            },
        });

        Assert.Equal(0, result.BenefitCostRatio);
    }
}