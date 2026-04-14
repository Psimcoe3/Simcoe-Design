using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RestorationEtrServiceTests
{
    [Fact]
    public void EstimateSwitchingWindowMinutes_UsesRemoteAndManualDurations()
    {
        double result = RestorationEtrService.EstimateSwitchingWindowMinutes(new SwitchingSequenceService.SwitchingSequencePlan
        {
            IsValid = true,
            Steps =
            {
                new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true },
                new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = false },
                new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true },
            },
        });

        Assert.Equal(16, result);
    }

    [Fact]
    public void EstimateSwitchingWindowMinutes_InvalidPlanReturnsZero()
    {
        double result = RestorationEtrService.EstimateSwitchingWindowMinutes(new SwitchingSequenceService.SwitchingSequencePlan());

        Assert.Equal(0, result);
    }

    [Fact]
    public void CreateRestorationTimeline_InvalidSwitchingPlanReturnsIssue()
    {
        var result = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan { Issue = "Tie close would create a loop" },
            new ServiceRestorationService.ServiceRestorationPlan(),
            new CrewDispatchService.DispatchPlan());

        Assert.Equal("Tie close would create a loop", result.Issue);
        Assert.Empty(result.Milestones);
    }

    [Fact]
    public void CreateRestorationTimeline_BuildsSequentialMilestones()
    {
        var result = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan
            {
                IsValid = true,
                Steps =
                {
                    new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true },
                    new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true },
                },
            },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages =
                {
                    new ServiceRestorationService.RestorationStage { StageNumber = 1, BlockNames = new List<string> { "A" }, CustomerCount = 100 },
                    new ServiceRestorationService.RestorationStage { StageNumber = 2, BlockNames = new List<string> { "B" }, CustomerCount = 50 },
                },
            },
            new CrewDispatchService.DispatchPlan(),
            verificationMinutesPerStage: 10,
            remoteStageMinutes: 5);

        Assert.Equal(4, result.SwitchingWindowMinutes);
        Assert.Equal(19, result.Milestones[0].RestoreMinute);
        Assert.Equal(34, result.Milestones[1].RestoreMinute);
    }

    [Fact]
    public void CreateRestorationTimeline_ManualStagesTakeLonger()
    {
        var remote = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan { IsValid = true, Steps = { new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true } } },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages = { new ServiceRestorationService.RestorationStage { StageNumber = 1, CustomerCount = 20 } },
            },
            new CrewDispatchService.DispatchPlan());
        var manual = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan { IsValid = true, Steps = { new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true } } },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages = { new ServiceRestorationService.RestorationStage { StageNumber = 1, CustomerCount = 20, RequiresManualSwitching = true } },
            },
            new CrewDispatchService.DispatchPlan());

        Assert.True(manual.Milestones[0].RestoreMinute > remote.Milestones[0].RestoreMinute);
    }

    [Fact]
    public void CreateRestorationTimeline_FinalEtrRespectsRepairClearTime()
    {
        var result = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan { IsValid = true, Steps = { new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true } } },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages = { new ServiceRestorationService.RestorationStage { StageNumber = 1, CustomerCount = 20 } },
            },
            new CrewDispatchService.DispatchPlan { EstimatedClearTimeMinutes = 90 });

        Assert.Equal(90, result.FinalEtrMinutes);
    }

    [Fact]
    public void CreateRestorationTimeline_FinalEtrUsesLastStageWhenLaterThanRepairs()
    {
        var result = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan
            {
                IsValid = true,
                Steps =
                {
                    new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = false },
                    new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = false },
                },
            },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages = { new ServiceRestorationService.RestorationStage { StageNumber = 1, CustomerCount = 20, RequiresManualSwitching = true } },
            },
            new CrewDispatchService.DispatchPlan { EstimatedClearTimeMinutes = 20 });

        Assert.True(result.FinalEtrMinutes > 20);
        Assert.Equal(result.Milestones[0].RestoreMinute, result.FinalEtrMinutes);
    }

    [Fact]
    public void CreateRestorationTimeline_SumsCustomersRestoredByStages()
    {
        var result = RestorationEtrService.CreateRestorationTimeline(
            new SwitchingSequenceService.SwitchingSequencePlan { IsValid = true, Steps = { new SwitchingSequenceService.SwitchingStep { IsRemoteControlled = true } } },
            new ServiceRestorationService.ServiceRestorationPlan
            {
                Stages =
                {
                    new ServiceRestorationService.RestorationStage { StageNumber = 1, CustomerCount = 120 },
                    new ServiceRestorationService.RestorationStage { StageNumber = 2, CustomerCount = 80 },
                },
            },
            new CrewDispatchService.DispatchPlan());

        Assert.Equal(200, result.CustomersRestoredByStages);
    }
}