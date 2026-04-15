using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MaintenanceTaskServiceTests
{
    private static MaintenanceTaskService.MaintenanceCandidate CreateCandidate(
        string name,
        double healthIndex,
        int intervalMonths,
        double laborHours,
        int customersExposed,
        bool requiresShutdown = false) => new()
    {
        AssetName = name,
        ConditionAssessment = new AssetConditionService.AssetConditionAssessment { AssetName = name, HealthIndex = healthIndex },
        InspectionRecommendation = new InspectionIntervalService.InspectionRecommendation { AssetName = name, IntervalMonths = intervalMonths },
        LaborHours = laborHours,
        CustomersExposed = customersExposed,
        RequiresShutdown = requiresShutdown,
    };

    [Fact]
    public void CalculatePriorityScore_IncreasesWithHealthRiskAndExposure()
    {
        double low = MaintenanceTaskService.CalculatePriorityScore(CreateCandidate("A", 30, 18, 8, 100));
        double high = MaintenanceTaskService.CalculatePriorityScore(CreateCandidate("B", 80, 3, 8, 500));

        Assert.True(high > low);
    }

    [Fact]
    public void CalculatePriorityScore_ShutdownCarriesPenalty()
    {
        double online = MaintenanceTaskService.CalculatePriorityScore(CreateCandidate("A", 70, 6, 4, 200, requiresShutdown: false));
        double shutdown = MaintenanceTaskService.CalculatePriorityScore(CreateCandidate("A", 70, 6, 4, 200, requiresShutdown: true));

        Assert.True(shutdown < online);
    }

    [Fact]
    public void RankCandidates_OrdersByPriorityThenLabor()
    {
        var ranked = MaintenanceTaskService.RankCandidates(new[]
        {
            CreateCandidate("Low", 40, 18, 2, 50),
            CreateCandidate("High", 85, 3, 8, 300),
            CreateCandidate("TieCheaper", 60, 6, 2, 200),
            CreateCandidate("TieCostlier", 60, 6, 4, 200),
        });

        Assert.Equal("High", ranked[0].AssetName);
        Assert.True(ranked.IndexOf(ranked.Single(candidate => candidate.AssetName == "TieCheaper")) < ranked.IndexOf(ranked.Single(candidate => candidate.AssetName == "TieCostlier")));
    }

    [Fact]
    public void CreatePlan_FitsHighestPriorityWorkWithinLaborBudget()
    {
        var plan = MaintenanceTaskService.CreatePlan(
            new[]
            {
                CreateCandidate("Critical", 90, 3, 12, 600),
                CreateCandidate("Routine", 35, 18, 10, 50),
            },
            laborHourBudget: 12,
            shutdownWindowBudget: 1);

        Assert.Single(plan.ScheduledTasks);
        Assert.Equal("Critical", plan.ScheduledTasks[0].AssetName);
        Assert.Contains("Routine", plan.DeferredAssets);
    }

    [Fact]
    public void CreatePlan_RespectsShutdownWindowBudget()
    {
        var plan = MaintenanceTaskService.CreatePlan(
            new[]
            {
                CreateCandidate("SWGR-1", 85, 3, 8, 400, requiresShutdown: true),
                CreateCandidate("SWGR-2", 82, 4, 8, 350, requiresShutdown: true),
            },
            laborHourBudget: 24,
            shutdownWindowBudget: 1);

        Assert.Single(plan.ScheduledTasks);
        Assert.Single(plan.DeferredAssets);
        Assert.Equal(1, plan.ShutdownWindowsUsed);
    }

    [Fact]
    public void CreatePlan_SumsCustomersRiskAddressedAndLabor()
    {
        var plan = MaintenanceTaskService.CreatePlan(
            new[]
            {
                CreateCandidate("A", 75, 6, 6, 200),
                CreateCandidate("B", 70, 6, 4, 150),
            },
            laborHourBudget: 12,
            shutdownWindowBudget: 0);

        Assert.Equal(10, plan.UsedLaborHours);
        Assert.Equal(350, plan.CustomersRiskAddressed);
    }

    [Fact]
    public void CreatePlan_NoBudgetDefersAllWork()
    {
        var plan = MaintenanceTaskService.CreatePlan(
            new[] { CreateCandidate("A", 80, 3, 6, 300) },
            laborHourBudget: 0,
            shutdownWindowBudget: 0);

        Assert.Empty(plan.ScheduledTasks);
        Assert.Equal(new[] { "A" }, plan.DeferredAssets);
        Assert.NotNull(plan.Issue);
    }

    [Fact]
    public void CreatePlan_ZeroDeferredWorkClearsIssue()
    {
        var plan = MaintenanceTaskService.CreatePlan(
            new[] { CreateCandidate("A", 50, 12, 2, 50) },
            laborHourBudget: 8,
            shutdownWindowBudget: 0);

        Assert.Null(plan.Issue);
    }
}