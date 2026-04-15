using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class InspectionIntervalServiceTests
{
    [Theory]
    [InlineData(AssetConditionService.AssetType.Transformer, 12)]
    [InlineData(AssetConditionService.AssetType.Switchgear, 12)]
    [InlineData(AssetConditionService.AssetType.Breaker, 18)]
    [InlineData(AssetConditionService.AssetType.UndergroundCable, 24)]
    [InlineData(AssetConditionService.AssetType.OverheadLine, 24)]
    public void GetBaseIntervalMonths_ReturnsExpectedDefaults(AssetConditionService.AssetType assetType, int expected)
    {
        Assert.Equal(expected, InspectionIntervalService.GetBaseIntervalMonths(assetType));
    }

    [Fact]
    public void GetPrimaryMethod_CriticalAssetsUseDetailedOffline()
    {
        var result = InspectionIntervalService.GetPrimaryMethod(
            AssetConditionService.AssetType.Transformer,
            AssetConditionService.ConditionBand.Critical);

        Assert.Equal(InspectionIntervalService.InspectionMethod.DetailedOffline, result);
    }

    [Fact]
    public void AdjustIntervalMonths_PoorConditionShortensInterval()
    {
        int result = InspectionIntervalService.AdjustIntervalMonths(
            24,
            new AssetConditionService.AssetConditionAssessment { ConditionBand = AssetConditionService.ConditionBand.Poor },
            AssetConditionService.AssetCriticality.Standard);

        Assert.Equal(12, result);
    }

    [Fact]
    public void AdjustIntervalMonths_CriticalityFurtherShortensInterval()
    {
        int standard = InspectionIntervalService.AdjustIntervalMonths(
            12,
            new AssetConditionService.AssetConditionAssessment { ConditionBand = AssetConditionService.ConditionBand.Fair },
            AssetConditionService.AssetCriticality.Standard);
        int critical = InspectionIntervalService.AdjustIntervalMonths(
            12,
            new AssetConditionService.AssetConditionAssessment { ConditionBand = AssetConditionService.ConditionBand.Fair },
            AssetConditionService.AssetCriticality.Critical);

        Assert.True(critical < standard);
    }

    [Fact]
    public void RecommendInspection_GoodOverheadAssetGetsRoutineVisualInspection()
    {
        var result = InspectionIntervalService.RecommendInspection(new AssetConditionService.AssetProfile
        {
            Name = "Line-1",
            AssetType = AssetConditionService.AssetType.OverheadLine,
            AgeYears = 5,
            ExpectedLifeYears = 40,
            PeakLoadPerUnit = 0.4,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Mild,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Good,
        });

        Assert.Equal(InspectionIntervalService.InspectionMethod.Visual, result.PrimaryMethod);
        Assert.True(result.IntervalMonths > 24);
        Assert.False(result.RequiresOfflineTesting);
    }

    [Fact]
    public void RecommendInspection_DegradedTransformerGetsDiagnosticTesting()
    {
        var result = InspectionIntervalService.RecommendInspection(new AssetConditionService.AssetProfile
        {
            Name = "TX-1",
            AssetType = AssetConditionService.AssetType.Transformer,
            AgeYears = 30,
            ExpectedLifeYears = 35,
            PeakLoadPerUnit = 1.0,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Severe,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Investigate,
        });

        Assert.Equal(InspectionIntervalService.InspectionMethod.DiagnosticTesting, result.PrimaryMethod);
        Assert.True(result.RequiresInfrared);
        Assert.True(result.RequiresOfflineTesting);
    }

    [Fact]
    public void RecommendInspection_CriticalSwitchgearGetsVeryShortInterval()
    {
        var result = InspectionIntervalService.RecommendInspection(new AssetConditionService.AssetProfile
        {
            Name = "SWGR-1",
            AssetType = AssetConditionService.AssetType.Switchgear,
            AgeYears = 38,
            ExpectedLifeYears = 40,
            PeakLoadPerUnit = 1.2,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Coastal,
            Criticality = AssetConditionService.AssetCriticality.Critical,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Bad,
        });

        Assert.True(result.IntervalMonths <= 3);
        Assert.Equal(InspectionIntervalService.InspectionMethod.DetailedOffline, result.PrimaryMethod);
    }

    [Fact]
    public void RecommendInspection_RationaleReflectsConditionBand()
    {
        var result = InspectionIntervalService.RecommendInspection(new AssetConditionService.AssetProfile
        {
            Name = "CB-1",
            AssetType = AssetConditionService.AssetType.Breaker,
            AgeYears = 38,
            ExpectedLifeYears = 35,
            PeakLoadPerUnit = 1.3,
            EnvironmentSeverity = AssetConditionService.EnvironmentSeverity.Coastal,
            Criticality = AssetConditionService.AssetCriticality.Critical,
            LatestTestVerdict = ElectricalTestingService.TestVerdict.Bad,
        });

        Assert.Contains("critical", result.Rationale.ToLowerInvariant());
    }
}