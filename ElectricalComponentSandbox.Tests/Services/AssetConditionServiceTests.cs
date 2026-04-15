using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class AssetConditionServiceTests
{
    [Fact]
    public void CalculateAgeScore_ScalesWithLifeConsumed()
    {
        double result = AssetConditionService.CalculateAgeScore(20, 40);

        Assert.Equal(33.33, result, 2);
    }

    [Fact]
    public void CalculateLoadScore_ClampsOverloadedAssets()
    {
        double result = AssetConditionService.CalculateLoadScore(2.0);

        Assert.Equal(100, result);
    }

    [Theory]
    [InlineData(AssetConditionService.EnvironmentSeverity.Mild, 20)]
    [InlineData(AssetConditionService.EnvironmentSeverity.Moderate, 40)]
    [InlineData(AssetConditionService.EnvironmentSeverity.Severe, 70)]
    [InlineData(AssetConditionService.EnvironmentSeverity.Coastal, 85)]
    public void GetEnvironmentScore_ReturnsExpectedWeight(AssetConditionService.EnvironmentSeverity severity, double expected)
    {
        Assert.Equal(expected, AssetConditionService.GetEnvironmentScore(severity));
    }

    [Theory]
    [InlineData(ElectricalTestingService.TestVerdict.Good, 15)]
    [InlineData(ElectricalTestingService.TestVerdict.Investigate, 55)]
    [InlineData(ElectricalTestingService.TestVerdict.Bad, 90)]
    public void GetTestScore_ReflectsLatestTestVerdict(ElectricalTestingService.TestVerdict verdict, double expected)
    {
        Assert.Equal(expected, AssetConditionService.GetTestScore(verdict));
    }

    [Fact]
    public void GetCriticalityMultiplier_ReducesExposureWhenRedundant()
    {
        double noRedundancy = AssetConditionService.GetCriticalityMultiplier(AssetConditionService.AssetCriticality.Critical, false);
        double redundant = AssetConditionService.GetCriticalityMultiplier(AssetConditionService.AssetCriticality.Critical, true);

        Assert.True(redundant < noRedundancy);
    }

    [Fact]
    public void AssessAsset_CombinesConditionDriversIntoHealthIndex()
    {
        var result = AssetConditionService.AssessAsset(new AssetConditionService.AssetProfile
        {
            Name = "TX-1",
            AssetType = AssetConditionService.AssetType.Transformer,
            AgeYears = 35,
            ExpectedLifeYears = 40,
            PeakLoadPerUnit = 1.1,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Severe,
            Criticality = AssetConditionService.AssetCriticality.Important,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Investigate,
        });

        Assert.Equal("TX-1", result.AssetName);
        Assert.True(result.HealthIndex > 50);
        Assert.True(result.FailureProbability > 0.5);
    }

    [Fact]
    public void AssessAsset_BadTestPushesAssetIntoWorseConditionBand()
    {
        var good = AssetConditionService.AssessAsset(new AssetConditionService.AssetProfile
        {
            Name = "CB-1",
            ExpectedLifeYears = 30,
            AgeYears = 15,
            PeakLoadPerUnit = 0.7,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Moderate,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Good,
        });
        var bad = AssetConditionService.AssessAsset(new AssetConditionService.AssetProfile
        {
            Name = "CB-1",
            ExpectedLifeYears = 30,
            AgeYears = 15,
            PeakLoadPerUnit = 0.7,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Moderate,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Bad,
        });

        Assert.True(bad.HealthIndex > good.HealthIndex);
        Assert.NotEqual(good.ConditionBand, bad.ConditionBand);
    }

    [Fact]
    public void AssessAsset_RedundancyMitigatesHealthExposure()
    {
        var nonRedundant = AssetConditionService.AssessAsset(new AssetConditionService.AssetProfile
        {
            Name = "Feed-1",
            AgeYears = 25,
            ExpectedLifeYears = 35,
            PeakLoadPerUnit = 0.9,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Coastal,
            Criticality = AssetConditionService.AssetCriticality.Critical,
        });
        var redundant = AssetConditionService.AssessAsset(new AssetConditionService.AssetProfile
        {
            Name = "Feed-1",
            AgeYears = 25,
            ExpectedLifeYears = 35,
            PeakLoadPerUnit = 0.9,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Coastal,
            Criticality = AssetConditionService.AssetCriticality.Critical,
            HasRedundancy = true,
        });

        Assert.True(redundant.HealthIndex < nonRedundant.HealthIndex);
    }

    [Theory]
    [InlineData(20, AssetConditionService.ConditionBand.Good)]
    [InlineData(45, AssetConditionService.ConditionBand.Fair)]
    [InlineData(65, AssetConditionService.ConditionBand.Poor)]
    [InlineData(85, AssetConditionService.ConditionBand.Critical)]
    public void GetConditionBand_ReturnsExpectedBand(double healthIndex, AssetConditionService.ConditionBand expected)
    {
        Assert.Equal(expected, AssetConditionService.GetConditionBand(healthIndex));
    }
}