using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ColdLoadPickupServiceTests
{
    [Fact]
    public void GetPickupMultiplier_GrowsWithOutageDuration()
    {
        double shortOutage = ColdLoadPickupService.GetPickupMultiplier(ColdLoadPickupService.LoadMix.Mixed, 0.5);
        double longOutage = ColdLoadPickupService.GetPickupMultiplier(ColdLoadPickupService.LoadMix.Mixed, 8);

        Assert.True(longOutage > shortOutage);
    }

    [Fact]
    public void GetPickupMultiplier_ResidentialExceedsIndustrial()
    {
        double residential = ColdLoadPickupService.GetPickupMultiplier(ColdLoadPickupService.LoadMix.Residential, 4);
        double industrial = ColdLoadPickupService.GetPickupMultiplier(ColdLoadPickupService.LoadMix.Industrial, 4);

        Assert.True(residential > industrial);
    }

    [Fact]
    public void EstimateInitialDemandKw_AppliesMultiplier()
    {
        double result = ColdLoadPickupService.EstimateInitialDemandKw(1000, ColdLoadPickupService.LoadMix.Mixed, 4);

        Assert.Equal(1800, result);
    }

    [Fact]
    public void CalculateSafeRestoreBlockKw_DividesByPickupMultiplier()
    {
        double result = ColdLoadPickupService.CalculateSafeRestoreBlockKw(900, 1.8);

        Assert.Equal(500, result);
    }

    [Fact]
    public void CreateRestorationPlan_SingleStage_WhenCapacityIsSufficient()
    {
        var result = ColdLoadPickupService.CreateRestorationPlan(400, 900, ColdLoadPickupService.LoadMix.Industrial, 1);

        Assert.False(result.RequiresStagedRestore);
        Assert.Single(result.Stages);
    }

    [Fact]
    public void CreateRestorationPlan_MultipleStages_WhenCapacityIsLimited()
    {
        var result = ColdLoadPickupService.CreateRestorationPlan(900, 600, ColdLoadPickupService.LoadMix.Mixed, 6);

        Assert.True(result.RequiresStagedRestore);
        Assert.True(result.Stages.Count > 1);
    }

    [Fact]
    public void CreateRestorationPlan_StageDelaysIncreaseByConfiguredIncrement()
    {
        var result = ColdLoadPickupService.CreateRestorationPlan(900, 600, ColdLoadPickupService.LoadMix.Mixed, 6, stageDelayMinutes: 20);

        Assert.Equal(0, result.Stages[0].DelayMinutes);
        Assert.Equal(20, result.Stages[1].DelayMinutes);
    }

    [Fact]
    public void CreateRestorationPlan_NoCapacity_ReturnsIssue()
    {
        var result = ColdLoadPickupService.CreateRestorationPlan(900, 0, ColdLoadPickupService.LoadMix.Mixed, 6);

        Assert.Equal("No available capacity exists for cold-load restoration", result.Issue);
    }

    [Fact]
    public void CreateRestorationPlan_ZeroDemand_ReturnsEmptyStages()
    {
        var result = ColdLoadPickupService.CreateRestorationPlan(0, 500, ColdLoadPickupService.LoadMix.Mixed, 6);

        Assert.Empty(result.Stages);
        Assert.Equal(0, result.EstimatedInitialDemandKw);
    }
}