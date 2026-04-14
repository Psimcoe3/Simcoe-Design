using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SectionalizingServiceTests
{
    [Fact]
    public void FindNearestUpstreamSwitch_ReturnsClosestBoundaryBeforeFault()
    {
        var result = SectionalizingService.FindNearestUpstreamSwitch(new[]
        {
            new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
            new SectionalizingService.BoundarySwitch { Name = "Mid", BoundaryAfterSequenceOrder = 2 },
        }, 3);

        Assert.Equal("Mid", result?.Name);
    }

    [Fact]
    public void FindNearestDownstreamSwitch_ReturnsClosestBoundaryAfterFault()
    {
        var result = SectionalizingService.FindNearestDownstreamSwitch(new[]
        {
            new SectionalizingService.BoundarySwitch { Name = "Tail", BoundaryAfterSequenceOrder = 4 },
            new SectionalizingService.BoundarySwitch { Name = "Adjacent", BoundaryAfterSequenceOrder = 3 },
        }, 3);

        Assert.Equal("Adjacent", result?.Name);
    }

    [Fact]
    public void CreateIsolationPlan_MiddleFault_UsesBoundingSwitches()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3, ConnectedLoadAmps = 50 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
                new SectionalizingService.BoundarySwitch { Name = "SW-12", BoundaryAfterSequenceOrder = 1 },
                new SectionalizingService.BoundarySwitch { Name = "SW-23", BoundaryAfterSequenceOrder = 2 },
            },
            "S2");

        Assert.True(result.IsolatesFault);
        Assert.Equal("SW-12", result.UpstreamSwitchName);
        Assert.Equal("SW-23", result.DownstreamSwitchName);
        Assert.Single(result.IsolatedSections);
    }

    [Fact]
    public void CreateIsolationPlan_EndFault_CanIsolateTailWithoutDownstreamSwitch()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3, ConnectedLoadAmps = 50 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
                new SectionalizingService.BoundarySwitch { Name = "SW-23", BoundaryAfterSequenceOrder = 2 },
            },
            "S3");

        Assert.True(result.IsolatesFault);
        Assert.Equal("SW-23", result.UpstreamSwitchName);
        Assert.Null(result.DownstreamSwitchName);
    }

    [Fact]
    public void CreateIsolationPlan_MissingUpstreamSwitch_ReturnsIssue()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
            },
            System.Array.Empty<SectionalizingService.BoundarySwitch>(),
            "S1");

        Assert.Equal("No upstream isolation switch is available", result.Issue);
    }

    [Fact]
    public void CreateIsolationPlan_MiddleFaultWithoutDownstreamSwitch_ReturnsIssue()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3, ConnectedLoadAmps = 50 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
                new SectionalizingService.BoundarySwitch { Name = "SW-12", BoundaryAfterSequenceOrder = 1 },
            },
            "S2");

        Assert.Equal("No downstream isolation switch is available", result.Issue);
    }

    [Fact]
    public void CreateIsolationPlan_ComputesUnservedLoadForIsolatedBlock()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
                new SectionalizingService.FeederSection { Name = "S3", SequenceOrder = 3, ConnectedLoadAmps = 50 },
                new SectionalizingService.FeederSection { Name = "S4", SequenceOrder = 4, ConnectedLoadAmps = 45 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
                new SectionalizingService.BoundarySwitch { Name = "SW-12", BoundaryAfterSequenceOrder = 1 },
                new SectionalizingService.BoundarySwitch { Name = "SW-34", BoundaryAfterSequenceOrder = 3 },
            },
            "S2");

        Assert.Equal(105, result.UnservedLoadAmps);
        Assert.Equal(2, result.IsolatedSections.Count);
    }

    [Fact]
    public void CreateIsolationPlan_RemoteFlagRequiresBothSelectedSwitchesToBeRemote()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0, IsRemoteControlled = true },
                new SectionalizingService.BoundarySwitch { Name = "SW-12", BoundaryAfterSequenceOrder = 1, IsRemoteControlled = false },
            },
            "S1");

        Assert.False(result.CanBePerformedRemotely);
    }

    [Fact]
    public void CreateIsolationPlan_IgnoresNormallyOpenSwitches()
    {
        var result = SectionalizingService.CreateIsolationPlan(
            new[]
            {
                new SectionalizingService.FeederSection { Name = "S1", SequenceOrder = 1, ConnectedLoadAmps = 60 },
                new SectionalizingService.FeederSection { Name = "S2", SequenceOrder = 2, ConnectedLoadAmps = 55 },
            },
            new[]
            {
                new SectionalizingService.BoundarySwitch { Name = "Source", BoundaryAfterSequenceOrder = 0 },
                new SectionalizingService.BoundarySwitch { Name = "NO-Tie", BoundaryAfterSequenceOrder = 1, IsNormallyClosed = false },
            },
            "S1");

        Assert.Null(result.DownstreamSwitchName);
    }
}