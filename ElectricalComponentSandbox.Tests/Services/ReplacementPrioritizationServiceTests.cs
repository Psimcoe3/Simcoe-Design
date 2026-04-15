using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ReplacementPrioritizationServiceTests
{
    private static ReplacementPrioritizationService.ReplacementCandidate CreateCandidate(
        string name,
        double healthIndex,
        double failureProbability,
        double failureCost,
        double replacementCost,
        double annualMaintenanceCost = 0,
        bool compliance = false,
        bool spareConstraint = false) => new()
    {
        AssetName = name,
        ConditionAssessment = new AssetConditionService.AssetConditionAssessment
        {
            AssetName = name,
            HealthIndex = healthIndex,
            FailureProbability = failureProbability,
        },
        FailureImpact = new OutageCostService.OutageCostAnalysis { ScenarioName = name, TotalCost = failureCost },
        ReplacementCost = replacementCost,
        AnnualMaintenanceCost = annualMaintenanceCost,
        HasComplianceIssue = compliance,
        HasSpareConstraint = spareConstraint,
    };

    [Fact]
    public void CalculateAnnualRiskCost_UsesFailureProbabilityAndConsequence()
    {
        double result = ReplacementPrioritizationService.CalculateAnnualRiskCost(
            CreateCandidate("TX-1", 80, 0.2, 100000, 400000, annualMaintenanceCost: 5000));

        Assert.Equal(25000, result);
    }

    [Fact]
    public void CalculatePriorityScore_IncreasesWithComplianceAndSpareConstraint()
    {
        double baseScore = ReplacementPrioritizationService.CalculatePriorityScore(
            CreateCandidate("A", 70, 0.2, 50000, 300000));
        double elevatedScore = ReplacementPrioritizationService.CalculatePriorityScore(
            CreateCandidate("A", 70, 0.2, 50000, 300000, compliance: true, spareConstraint: true));

        Assert.True(elevatedScore > baseScore);
    }

    [Theory]
    [InlineData(20, ReplacementPrioritizationService.PriorityBand.Monitor)]
    [InlineData(60, ReplacementPrioritizationService.PriorityBand.Plan)]
    [InlineData(90, ReplacementPrioritizationService.PriorityBand.Urgent)]
    [InlineData(110, ReplacementPrioritizationService.PriorityBand.Immediate)]
    public void GetPriorityBand_MapsExpectedBand(double score, ReplacementPrioritizationService.PriorityBand expected)
    {
        Assert.Equal(expected, ReplacementPrioritizationService.GetPriorityBand(score));
    }

    [Fact]
    public void AssessCandidate_ComputesBenefitCostRatio()
    {
        var result = ReplacementPrioritizationService.AssessCandidate(
            CreateCandidate("TX-1", 85, 0.25, 200000, 500000, annualMaintenanceCost: 10000));

        Assert.Equal(0.12, result.BenefitCostRatio, 2);
    }

    [Fact]
    public void RankCandidates_OrdersByPriorityThenBenefitCost()
    {
        var ranked = ReplacementPrioritizationService.RankCandidates(new[]
        {
            CreateCandidate("Low", 40, 0.05, 10000, 100000),
            CreateCandidate("High", 90, 0.3, 150000, 300000, compliance: true),
            CreateCandidate("Mid", 70, 0.2, 80000, 200000),
        });

        Assert.Equal("High", ranked[0].AssetName);
    }

    [Fact]
    public void CreateCapitalPlan_SelectsHighestPriorityWithinBudget()
    {
        var plan = ReplacementPrioritizationService.CreateCapitalPlan(
            new[]
            {
                CreateCandidate("Critical", 95, 0.3, 200000, 250000, compliance: true),
                CreateCandidate("Routine", 50, 0.05, 20000, 200000),
            },
            capitalBudget: 250000);

        Assert.Single(plan.SelectedAssets);
        Assert.Equal("Critical", plan.SelectedAssets[0].AssetName);
        Assert.Contains("Routine", plan.DeferredAssets);
    }

    [Fact]
    public void CreateCapitalPlan_NoBudgetDefersAllCandidates()
    {
        var plan = ReplacementPrioritizationService.CreateCapitalPlan(
            new[] { CreateCandidate("A", 80, 0.2, 100000, 300000) },
            capitalBudget: 0);

        Assert.Empty(plan.SelectedAssets);
        Assert.Equal(new[] { "A" }, plan.DeferredAssets);
        Assert.NotNull(plan.Issue);
    }

    [Fact]
    public void CreateCapitalPlan_DeferredRiskCostSumsDeferredAssets()
    {
        var candidates = new[]
        {
            CreateCandidate("A", 90, 0.3, 100000, 200000),
            CreateCandidate("B", 85, 0.2, 100000, 200000),
        };
        var plan = ReplacementPrioritizationService.CreateCapitalPlan(candidates, capitalBudget: 200000);
        var deferred = ReplacementPrioritizationService.AssessCandidate(candidates.Single(candidate => candidate.AssetName == plan.DeferredAssets[0]));

        Assert.Equal(deferred.AnnualRiskCost, plan.DeferredAnnualRiskCost, 2);
    }
}