using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ServiceRestorationServiceTests
{
    [Fact]
    public void CalculatePickupDemand_AppliesMultiplier()
    {
        double result = ServiceRestorationService.CalculatePickupDemand(250, 1.8);

        Assert.Equal(450, result);
    }

    [Fact]
    public void RankBlocks_PrioritizesCriticalBeforeStandard()
    {
        var result = ServiceRestorationService.RankBlocks(new[]
        {
            new ServiceRestorationService.RestorationBlock { Name = "Standard", Priority = ServiceRestorationService.RestorationPriority.Standard, NormalLoadKw = 50 },
            new ServiceRestorationService.RestorationBlock { Name = "Critical", Priority = ServiceRestorationService.RestorationPriority.Critical, NormalLoadKw = 80 },
        });

        Assert.Equal("Critical", result[0].Name);
    }

    [Fact]
    public void RankBlocks_PrefersAutomaticSwitchingWithinSamePriority()
    {
        var result = ServiceRestorationService.RankBlocks(new[]
        {
            new ServiceRestorationService.RestorationBlock { Name = "Manual", Priority = ServiceRestorationService.RestorationPriority.Essential, NormalLoadKw = 30, RequiresManualSwitching = true },
            new ServiceRestorationService.RestorationBlock { Name = "Auto", Priority = ServiceRestorationService.RestorationPriority.Essential, NormalLoadKw = 35, RequiresManualSwitching = false },
        });

        Assert.Equal("Auto", result[0].Name);
    }

    [Fact]
    public void CreateRestorationPlan_BuildsMultipleStages_WhenSafeBlockIsLimited()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "Critical-1", Priority = ServiceRestorationService.RestorationPriority.Critical, NormalLoadKw = 150, CustomerCount = 40 },
                new ServiceRestorationService.RestorationBlock { Name = "Essential-1", Priority = ServiceRestorationService.RestorationPriority.Essential, NormalLoadKw = 130, CustomerCount = 35 },
                new ServiceRestorationService.RestorationBlock { Name = "Standard-1", Priority = ServiceRestorationService.RestorationPriority.Standard, NormalLoadKw = 120, CustomerCount = 25 },
            },
            availableCapacityKw: 450,
            loadMix: ColdLoadPickupService.LoadMix.Mixed,
            outageHours: 6);

        Assert.True(result.Stages.Count > 1);
        Assert.Equal("Critical-1", result.Stages[0].BlockNames[0]);
    }

    [Fact]
    public void CreateRestorationPlan_DefersOversizedBlock()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "Oversized", Priority = ServiceRestorationService.RestorationPriority.Critical, NormalLoadKw = 500, CustomerCount = 60 },
                new ServiceRestorationService.RestorationBlock { Name = "Fit", Priority = ServiceRestorationService.RestorationPriority.Essential, NormalLoadKw = 120, CustomerCount = 20 },
            },
            availableCapacityKw: 300,
            loadMix: ColdLoadPickupService.LoadMix.Mixed,
            outageHours: 6);

        Assert.Contains("Oversized", result.DeferredBlocks);
        Assert.Contains("Fit", result.Stages.SelectMany(stage => stage.BlockNames));
    }

    [Fact]
    public void CreateRestorationPlan_TracksManualSwitchingAtStageLevel()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "Manual-1", Priority = ServiceRestorationService.RestorationPriority.Critical, NormalLoadKw = 80, CustomerCount = 10, RequiresManualSwitching = true },
                new ServiceRestorationService.RestorationBlock { Name = "Auto-1", Priority = ServiceRestorationService.RestorationPriority.Essential, NormalLoadKw = 60, CustomerCount = 10 },
            },
            availableCapacityKw: 350,
            loadMix: ColdLoadPickupService.LoadMix.Industrial,
            outageHours: 1);

        Assert.True(result.Stages[0].RequiresManualSwitching);
    }

    [Fact]
    public void CreateRestorationPlan_NoCapacity_ReturnsIssue()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "A", NormalLoadKw = 80 },
            },
            availableCapacityKw: 0,
            loadMix: ColdLoadPickupService.LoadMix.Mixed,
            outageHours: 6);

        Assert.Equal("No transfer capacity is available for restoration", result.Issue);
    }

    [Fact]
    public void CreateRestorationPlan_UsesColdLoadMultiplierInStageDemand()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "A", NormalLoadKw = 100, CustomerCount = 10 },
            },
            availableCapacityKw: 250,
            loadMix: ColdLoadPickupService.LoadMix.Mixed,
            outageHours: 6);

        Assert.True(result.Stages[0].EstimatedPickupDemandKw > result.Stages[0].RestoredNormalLoadKw);
    }

    [Fact]
    public void CreateRestorationPlan_SafeRestoreBlock_IsReported()
    {
        var result = ServiceRestorationService.CreateRestorationPlan(
            new[]
            {
                new ServiceRestorationService.RestorationBlock { Name = "A", NormalLoadKw = 100, CustomerCount = 10 },
            },
            availableCapacityKw: 300,
            loadMix: ColdLoadPickupService.LoadMix.Mixed,
            outageHours: 6);

        Assert.True(result.SafeRestoreBlockKw > 0);
    }
}