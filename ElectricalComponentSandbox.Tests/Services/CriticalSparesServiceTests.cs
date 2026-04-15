using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CriticalSparesServiceTests
{
    [Fact]
    public void CalculateAnnualExpectedFailures_MultipliesInstalledBaseAndFailureProbability()
    {
        double result = CriticalSparesService.CalculateAnnualExpectedFailures(10, 0.12);

        Assert.Equal(1.2, result, 4);
    }

    [Fact]
    public void RecommendStock_LongLeadTimeAndSinglePointIncreaseMinimumStock()
    {
        var result = CriticalSparesService.RecommendStock(new CriticalSparesService.SparePartProfile
        {
            PartName = "Breaker Bottle",
            InstalledBase = 5,
            AverageFailureProbability = 0.2,
            LeadTimeDays = 180,
            UnitCost = 5000,
            IsSinglePointOfFailure = true,
        });

        Assert.True(result.MinimumStock >= 2);
        Assert.True(result.IsCritical);
    }

    [Fact]
    public void RecommendStock_TargetStockIsAtLeastMinimum()
    {
        var result = CriticalSparesService.RecommendStock(new CriticalSparesService.SparePartProfile
        {
            PartName = "Fuse Link",
            InstalledBase = 100,
            AverageFailureProbability = 0.05,
            LeadTimeDays = 30,
            UnitCost = 25,
        });

        Assert.True(result.TargetStock >= result.MinimumStock);
    }

    [Fact]
    public void RecommendStock_CarryingCostUsesTargetStockAndUnitCost()
    {
        var result = CriticalSparesService.RecommendStock(new CriticalSparesService.SparePartProfile
        {
            PartName = "Relay",
            InstalledBase = 10,
            AverageFailureProbability = 0.2,
            LeadTimeDays = 90,
            UnitCost = 1000,
        });

        Assert.Equal(result.TargetStock * 1000 * 0.2, result.AnnualCarryingCost, 2);
    }

    [Fact]
    public void AssessCoverage_ReportsShortageWhenBelowMinimum()
    {
        var coverage = CriticalSparesService.AssessCoverage(
            new CriticalSparesService.SpareRecommendation { PartName = "Relay", MinimumStock = 3 },
            onHandQuantity: 1);

        Assert.False(coverage.HasAdequateCoverage);
        Assert.Equal(2, coverage.ShortageQuantity);
    }

    [Fact]
    public void AssessCoverage_PassesWhenOnHandMeetsMinimum()
    {
        var coverage = CriticalSparesService.AssessCoverage(
            new CriticalSparesService.SpareRecommendation { PartName = "Relay", MinimumStock = 2 },
            onHandQuantity: 2);

        Assert.True(coverage.HasAdequateCoverage);
        Assert.Equal(0, coverage.ShortageQuantity);
    }

    [Fact]
    public void RankRecommendations_PrioritizesCriticalRecommendations()
    {
        var ranked = CriticalSparesService.RankRecommendations(new[]
        {
            new CriticalSparesService.SparePartProfile { PartName = "Fuse", InstalledBase = 50, AverageFailureProbability = 0.02, LeadTimeDays = 20, UnitCost = 10 },
            new CriticalSparesService.SparePartProfile { PartName = "Transformer Fan", InstalledBase = 4, AverageFailureProbability = 0.15, LeadTimeDays = 150, UnitCost = 800, IsSinglePointOfFailure = true },
        });

        Assert.Equal("Transformer Fan", ranked[0].PartName);
    }

    [Fact]
    public void RecommendStock_ZeroRiskCanStillRequireSingleCriticalSpare()
    {
        var result = CriticalSparesService.RecommendStock(new CriticalSparesService.SparePartProfile
        {
            PartName = "Controller",
            InstalledBase = 1,
            AverageFailureProbability = 0,
            LeadTimeDays = 365,
            UnitCost = 15000,
            IsSinglePointOfFailure = true,
        });

        Assert.Equal(1, result.MinimumStock);
    }
}