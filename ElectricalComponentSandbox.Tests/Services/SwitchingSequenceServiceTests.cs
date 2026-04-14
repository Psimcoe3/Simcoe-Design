using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SwitchingSequenceServiceTests
{
    [Fact]
    public void CreateIsolationSequence_BuildsOpenStepsInOrder()
    {
        var result = SwitchingSequenceService.CreateIsolationSequence(new SectionalizingService.IsolationPlan
        {
            FaultedSectionName = "S2",
            UpstreamSwitchName = "SW-12",
            DownstreamSwitchName = "SW-23",
            IsolatesFault = true,
            CanBePerformedRemotely = true,
        });

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Steps.Count);
        Assert.All(result.Steps, step => Assert.Equal(SwitchingSequenceService.SwitchingOperationType.Open, step.Operation));
    }

    [Fact]
    public void CreateIsolationSequence_InvalidIsolation_ReturnsIssue()
    {
        var result = SwitchingSequenceService.CreateIsolationSequence(new SectionalizingService.IsolationPlan
        {
            FaultedSectionName = "S2",
            Issue = "No upstream isolation switch is available",
        });

        Assert.False(result.IsValid);
        Assert.Equal("No upstream isolation switch is available", result.Issue);
    }

    [Fact]
    public void CreateRestorationSequence_OrdersAllOpensBeforeTieClose()
    {
        var result = SwitchingSequenceService.CreateRestorationSequence(
            new SectionalizingService.IsolationPlan
            {
                FaultedSectionName = "S2",
                UpstreamSwitchName = "SW-12",
                DownstreamSwitchName = "SW-23",
                IsolatesFault = true,
                CanBePerformedRemotely = true,
            },
            new FeederReconfigurationService.ReconfigurationPlan
            {
                SwitchName = "Tie-7",
                OpenPointName = "NO-Section-9",
                IsRadial = true,
                PassesEmergencyRatings = true,
            });

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Steps.Count);
        Assert.Equal(SwitchingSequenceService.SwitchingOperationType.Close, result.Steps.Last().Operation);
    }

    [Fact]
    public void CreateRestorationSequence_NonRadialPlan_ReturnsIssue()
    {
        var result = SwitchingSequenceService.CreateRestorationSequence(
            new SectionalizingService.IsolationPlan
            {
                FaultedSectionName = "S2",
                UpstreamSwitchName = "SW-12",
                IsolatesFault = true,
                CanBePerformedRemotely = true,
            },
            new FeederReconfigurationService.ReconfigurationPlan
            {
                SwitchName = "Tie-7",
                IsRadial = false,
                PassesEmergencyRatings = true,
                Issue = "Selected switching sequence does not preserve a radial feeder",
            });

        Assert.False(result.IsValid);
        Assert.Contains("radial", result.Issue);
    }

    [Fact]
    public void CreateRestorationSequence_RatingViolation_ReturnsIssue()
    {
        var result = SwitchingSequenceService.CreateRestorationSequence(
            new SectionalizingService.IsolationPlan
            {
                FaultedSectionName = "S2",
                UpstreamSwitchName = "SW-12",
                IsolatesFault = true,
                CanBePerformedRemotely = true,
            },
            new FeederReconfigurationService.ReconfigurationPlan
            {
                SwitchName = "Tie-7",
                IsRadial = true,
                PassesEmergencyRatings = false,
                Issue = "Section North Main exceeds emergency rating at 108.3%",
            });

        Assert.False(result.IsValid);
        Assert.Contains("emergency rating", result.Issue);
    }

    [Fact]
    public void CreateRestorationSequence_RemoteFlagReflectsAnyManualStep()
    {
        var result = SwitchingSequenceService.CreateRestorationSequence(
            new SectionalizingService.IsolationPlan
            {
                FaultedSectionName = "S2",
                UpstreamSwitchName = "SW-12",
                DownstreamSwitchName = "SW-23",
                IsolatesFault = true,
                CanBePerformedRemotely = true,
            },
            new FeederReconfigurationService.ReconfigurationPlan
            {
                SwitchName = "Tie-7",
                OpenPointName = "NO-Section-9",
                IsRadial = true,
                PassesEmergencyRatings = true,
            },
            tieSwitchIsRemoteControlled: false,
            openPointIsRemoteControlled: true);

        Assert.False(result.CanBePerformedRemotely);
    }

    [Fact]
    public void SequenceMaintainsRadialOperation_FalseWhenTieClosesBeforeOpenPoint()
    {
        var result = SwitchingSequenceService.SequenceMaintainsRadialOperation(new SwitchingSequenceService.SwitchingSequencePlan
        {
            Steps =
            {
                new SwitchingSequenceService.SwitchingStep { OrderIndex = 1, Operation = SwitchingSequenceService.SwitchingOperationType.Close, DeviceName = "Tie-7" },
                new SwitchingSequenceService.SwitchingStep { OrderIndex = 2, Operation = SwitchingSequenceService.SwitchingOperationType.Open, DeviceName = "NO-Section-9" },
            },
        });

        Assert.False(result);
    }

    [Fact]
    public void SequenceMaintainsRadialOperation_TrueForIsolationOnlySequence()
    {
        var result = SwitchingSequenceService.SequenceMaintainsRadialOperation(new SwitchingSequenceService.SwitchingSequencePlan
        {
            Steps =
            {
                new SwitchingSequenceService.SwitchingStep { OrderIndex = 1, Operation = SwitchingSequenceService.SwitchingOperationType.Open, DeviceName = "SW-12" },
                new SwitchingSequenceService.SwitchingStep { OrderIndex = 2, Operation = SwitchingSequenceService.SwitchingOperationType.Open, DeviceName = "SW-23" },
            },
        });

        Assert.True(result);
    }
}