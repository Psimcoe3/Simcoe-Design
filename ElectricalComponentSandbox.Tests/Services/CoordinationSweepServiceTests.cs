using System;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CoordinationSweepServiceTests
{
    private static ProtectiveRelayService.RelaySettings CreateUpstream() => new()
    {
        Id = "MAIN",
        Curve = ProtectiveRelayService.CurveType.VeryInverse,
        PickupAmps = 600,
        TimeDial = 1.2,
        InstantaneousAmps = 0,
    };

    private static ProtectiveRelayService.RelaySettings CreateDownstream() => new()
    {
        Id = "FEEDER",
        Curve = ProtectiveRelayService.CurveType.VeryInverse,
        PickupAmps = 250,
        TimeDial = 0.35,
        InstantaneousAmps = 0,
    };

    [Fact]
    public void BuildFaultCurrentSeries_ReturnsRoundedEndpoints()
    {
        var series = CoordinationSweepService.BuildFaultCurrentSeries(1200, 4800, 4);

        Assert.Equal(new[] { 1200d, 2400d, 3600d, 4800d }, series);
    }

    [Fact]
    public void BuildFaultCurrentSeries_RequiresPositiveMinimum()
    {
        Assert.Throws<ArgumentException>(() => CoordinationSweepService.BuildFaultCurrentSeries(0, 1000, 3));
    }

    [Fact]
    public void EvaluateFaultLevels_ReturnsWorstMarginAndViolationList()
    {
        var upstream = CreateUpstream() with { TimeDial = 0.5 };
        var downstream = CreateDownstream() with { TimeDial = 0.45 };

        var summary = CoordinationSweepService.EvaluateFaultLevels(upstream, downstream, new[] { 1500d, 3000d, 6000d }, 0.3);

        Assert.False(summary.IsFullyCoordinated);
        Assert.NotEmpty(summary.Violations);
        Assert.Equal(summary.MinimumMarginSec, summary.WorstPoint.MarginSec);
    }

    [Fact]
    public void EvaluateFaultLevels_ForCoordinatedRelaysHasNoViolations()
    {
        var summary = CoordinationSweepService.EvaluateFaultLevels(CreateUpstream(), CreateDownstream(), new[] { 1500d, 3000d, 6000d }, 0.3);

        Assert.True(summary.IsFullyCoordinated);
        Assert.Empty(summary.Violations);
        Assert.Equal(3, summary.EvaluatedPointCount);
    }

    [Fact]
    public void SweepRange_UsesGeneratedFaultSeries()
    {
        var summary = CoordinationSweepService.SweepRange(CreateUpstream(), CreateDownstream(), 2000, 5000, pointCount: 5, minimumCtiSec: 0.3);

        Assert.Equal(5, summary.EvaluatedPointCount);
        Assert.True(summary.MaximumMarginSec >= summary.MinimumMarginSec);
    }

    [Fact]
    public void EvaluateFaultLevels_FiltersDuplicateAndInvalidFaultPoints()
    {
        var summary = CoordinationSweepService.EvaluateFaultLevels(CreateUpstream(), CreateDownstream(), new[] { -1d, 0d, 2500d, 2500d, 4000d });

        Assert.Equal(2, summary.EvaluatedPointCount);
        Assert.Equal(new[] { 2500d, 4000d }, summary.EvaluatedPoints.Select(point => point.FaultCurrentAmps).ToArray());
    }
}