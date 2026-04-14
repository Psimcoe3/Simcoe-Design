using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class UnderFrequencyLoadSheddingServiceTests
{
    [Fact]
    public void GetDefaultStages_60Hz_ReturnsFourStages()
    {
        var stages = UnderFrequencyLoadSheddingService.GetDefaultStages();

        Assert.Equal(4, stages.Count);
    }

    [Fact]
    public void GetDefaultStages_50Hz_UsesLowerPickupValues()
    {
        var stages50 = UnderFrequencyLoadSheddingService.GetDefaultStages(UnderFrequencyLoadSheddingService.FrequencyBase.Hz50);
        var stages60 = UnderFrequencyLoadSheddingService.GetDefaultStages(UnderFrequencyLoadSheddingService.FrequencyBase.Hz60);

        Assert.True(stages50[0].PickupFrequencyHz < stages60[0].PickupFrequencyHz);
    }

    [Fact]
    public void EstimateRequiredShedPercent_DeficitScalesToLoad()
    {
        double result = UnderFrequencyLoadSheddingService.EstimateRequiredShedPercent(100, 15);

        Assert.Equal(15.0, result);
    }

    [Fact]
    public void EstimateRequiredShedPercent_CapsAtHundredPercent()
    {
        double result = UnderFrequencyLoadSheddingService.EstimateRequiredShedPercent(100, 150);

        Assert.Equal(100.0, result);
    }

    [Fact]
    public void EvaluateFrequency_AboveFirstStage_NoEmergency()
    {
        var result = UnderFrequencyLoadSheddingService.EvaluateFrequency(59.5, 100);

        Assert.False(result.EmergencyActive);
        Assert.Empty(result.TriggeredStages);
    }

    [Fact]
    public void EvaluateFrequency_FirstStageOnly_ShedsFivePercent()
    {
        var result = UnderFrequencyLoadSheddingService.EvaluateFrequency(59.2, 100);

        Assert.True(result.EmergencyActive);
        Assert.Single(result.TriggeredStages);
        Assert.Equal(5.0, result.ShedMW);
    }

    [Fact]
    public void EvaluateFrequency_MultipleStages_AccumulatesShed()
    {
        var result = UnderFrequencyLoadSheddingService.EvaluateFrequency(58.6, 100);

        Assert.Equal(3, result.TriggeredStages.Count);
        Assert.Equal(25.0, result.ShedMW);
        Assert.Equal(75.0, result.RemainingLoadMW);
    }

    [Fact]
    public void EvaluateFrequency_DeepEvent_TriggersAllStages()
    {
        var result = UnderFrequencyLoadSheddingService.EvaluateFrequency(58.2, 120);

        Assert.Equal(4, result.TriggeredStages.Count);
        Assert.Equal(48.0, result.ShedMW);
    }

    [Fact]
    public void GetRestoreFrequency_AddsHysteresis()
    {
        double result = UnderFrequencyLoadSheddingService.GetRestoreFrequency(59.0);

        Assert.Equal(59.4, result);
    }

    [Fact]
    public void ValidateStagePlan_DefaultPlan_IsValid()
    {
        var result = UnderFrequencyLoadSheddingService.ValidateStagePlan(
            UnderFrequencyLoadSheddingService.GetDefaultStages());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStagePlan_NonDescendingPickup_IsInvalid()
    {
        var stages = new List<UnderFrequencyLoadSheddingService.UflsStage>
        {
            new() { StageNumber = 1, PickupFrequencyHz = 59.3, ShedPercent = 5, TimeDelayCycles = 12 },
            new() { StageNumber = 2, PickupFrequencyHz = 59.4, ShedPercent = 10, TimeDelayCycles = 12 },
        };

        var result = UnderFrequencyLoadSheddingService.ValidateStagePlan(stages);

        Assert.False(result.IsValid);
        Assert.Contains("decrease", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateStagePlan_TooMuchConfiguredShed_IsInvalid()
    {
        var stages = new List<UnderFrequencyLoadSheddingService.UflsStage>
        {
            new() { StageNumber = 1, PickupFrequencyHz = 59.3, ShedPercent = 30, TimeDelayCycles = 12 },
            new() { StageNumber = 2, PickupFrequencyHz = 59.0, ShedPercent = 25, TimeDelayCycles = 12 },
            new() { StageNumber = 3, PickupFrequencyHz = 58.7, ShedPercent = 20, TimeDelayCycles = 18 },
        };

        var result = UnderFrequencyLoadSheddingService.ValidateStagePlan(stages);

        Assert.False(result.IsValid);
        Assert.Contains("maximum", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateStagePlan_ReportsConfiguredTotal()
    {
        var result = UnderFrequencyLoadSheddingService.ValidateStagePlan(
            UnderFrequencyLoadSheddingService.GetDefaultStages());

        Assert.Equal(40.0, result.TotalConfiguredShedPercent);
    }
}