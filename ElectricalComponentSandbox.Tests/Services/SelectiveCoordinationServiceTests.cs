using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.SelectiveCoordinationService;

namespace ElectricalComponentSandbox.Tests.Services;

public class SelectiveCoordinationServiceTests
{
    // ── Trip Time Estimation ─────────────────────────────────────────────────

    [Fact]
    public void EstimateTripTime_BelowRating_MaxValue()
    {
        var device = new ProtectiveDevice { TripRatingAmps = 100 };
        Assert.Equal(double.MaxValue, SelectiveCoordinationService.EstimateTripTimeMs(device, 50));
    }

    [Fact]
    public void EstimateTripTime_AtRating_MaxValue()
    {
        var device = new ProtectiveDevice { TripRatingAmps = 100 };
        Assert.Equal(double.MaxValue, SelectiveCoordinationService.EstimateTripTimeMs(device, 100));
    }

    [Fact]
    public void EstimateTripTime_ZeroCurrent_MaxValue()
    {
        var device = new ProtectiveDevice { TripRatingAmps = 100 };
        Assert.Equal(double.MaxValue, SelectiveCoordinationService.EstimateTripTimeMs(device, 0));
    }

    [Fact]
    public void EstimateTripTime_HighFault_FuseIsFastest()
    {
        var fuse = new ProtectiveDevice { TripRatingAmps = 100, DeviceType = OCPDType.Fuse };
        var mccb = new ProtectiveDevice { TripRatingAmps = 100, DeviceType = OCPDType.MoldedCaseBreaker };

        double fuseTime = SelectiveCoordinationService.EstimateTripTimeMs(fuse, 5000);
        double mccbTime = SelectiveCoordinationService.EstimateTripTimeMs(mccb, 5000);

        Assert.True(fuseTime < mccbTime, "Fuse should trip faster than MCCB");
    }

    [Fact]
    public void EstimateTripTime_InverseTime_HigherCurrentFasterTrip()
    {
        var device = new ProtectiveDevice { TripRatingAmps = 100, DeviceType = OCPDType.MoldedCaseBreaker };
        double time200 = SelectiveCoordinationService.EstimateTripTimeMs(device, 200);
        double time500 = SelectiveCoordinationService.EstimateTripTimeMs(device, 500);

        Assert.True(time500 < time200, "Higher fault current should trip faster");
    }

    [Fact]
    public void EstimateTripTime_InstantaneousRegion()
    {
        var device = new ProtectiveDevice
        {
            TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
            InstantaneousTripMultiplier = 10.0,
        };
        // 10× = 1000A triggers instantaneous
        double time = SelectiveCoordinationService.EstimateTripTimeMs(device, 1000);
        Assert.True(time < 100, "Instantaneous trip should be very fast");
    }

    [Fact]
    public void EstimateTripTime_ShortTimeDelay_AddsTime()
    {
        var noDelay = new ProtectiveDevice
        {
            TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
            ShortTimeDelayCycles = 0,
            InstantaneousTripMultiplier = 5.0,
        };
        var withDelay = new ProtectiveDevice
        {
            TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
            ShortTimeDelayCycles = 6,
            InstantaneousTripMultiplier = 5.0,
        };

        double timeNoDelay = SelectiveCoordinationService.EstimateTripTimeMs(noDelay, 1000);
        double timeWithDelay = SelectiveCoordinationService.EstimateTripTimeMs(withDelay, 1000);

        Assert.True(timeWithDelay > timeNoDelay, "Short time delay should increase trip time");
    }

    [Fact]
    public void EstimateTripTime_PowerCircuitBreaker_SlowerThanMCCB()
    {
        var mccb = new ProtectiveDevice { TripRatingAmps = 400, DeviceType = OCPDType.MoldedCaseBreaker, InstantaneousTripMultiplier = 5.0 };
        var pcb = new ProtectiveDevice { TripRatingAmps = 400, DeviceType = OCPDType.PowerCircuitBreaker, InstantaneousTripMultiplier = 5.0 };

        double mccbTime = SelectiveCoordinationService.EstimateTripTimeMs(mccb, 5000);
        double pcbTime = SelectiveCoordinationService.EstimateTripTimeMs(pcb, 5000);

        Assert.True(pcbTime > mccbTime);
    }

    // ── Pair Evaluation ──────────────────────────────────────────────────────

    [Fact]
    public void EvaluatePair_LargeUpstream_SmallDownstream_Coordinated()
    {
        var upstream = new ProtectiveDevice
        {
            Id = "MSB", Name = "Main", TripRatingAmps = 1200,
            DeviceType = OCPDType.PowerCircuitBreaker,
            ShortTimeDelayCycles = 6,
        };
        var downstream = new ProtectiveDevice
        {
            Id = "P1", Name = "Panel1", TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };

        var result = SelectiveCoordinationService.EvaluatePair(upstream, downstream, 10.0);
        Assert.True(result.IsCoordinated, $"Should coordinate: ratio = {result.Ratio}");
        Assert.Null(result.Issue);
    }

    [Fact]
    public void EvaluatePair_SameSize_NotCoordinated()
    {
        var upstream = new ProtectiveDevice
        {
            Id = "A", Name = "DeviceA", TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };
        var downstream = new ProtectiveDevice
        {
            Id = "B", Name = "DeviceB", TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };

        var result = SelectiveCoordinationService.EvaluatePair(upstream, downstream, 5.0);
        Assert.False(result.IsCoordinated);
        Assert.NotNull(result.Issue);
    }

    [Fact]
    public void EvaluatePair_FuseDownstream_BreakerUpstream_CanCoordinate()
    {
        var upstream = new ProtectiveDevice
        {
            Id = "U", Name = "Upstream", TripRatingAmps = 400,
            DeviceType = OCPDType.PowerCircuitBreaker,
            ShortTimeDelayCycles = 6,
        };
        var downstream = new ProtectiveDevice
        {
            Id = "D", Name = "Downstream", TripRatingAmps = 100,
            DeviceType = OCPDType.Fuse,
        };

        var result = SelectiveCoordinationService.EvaluatePair(upstream, downstream, 10.0);
        // Fuse clears fast, large PCB with STD has longer trip → should coordinate
        Assert.True(result.IsCoordinated);
    }

    [Fact]
    public void EvaluatePair_ReturnsDeviceInfo()
    {
        var up = new ProtectiveDevice { Id = "U", Name = "Up", TripRatingAmps = 400 };
        var down = new ProtectiveDevice { Id = "D", Name = "Down", TripRatingAmps = 100 };
        var result = SelectiveCoordinationService.EvaluatePair(up, down, 5.0);

        Assert.Equal("U", result.Upstream.Id);
        Assert.Equal("D", result.Downstream.Id);
        Assert.Equal(5.0, result.FaultCurrentKA);
    }

    // ── Range Evaluation ─────────────────────────────────────────────────────

    [Fact]
    public void EvaluateAcrossRange_ReturnsWorstCase()
    {
        var upstream = new ProtectiveDevice
        {
            Id = "U", Name = "Main", TripRatingAmps = 400,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };
        var downstream = new ProtectiveDevice
        {
            Id = "D", Name = "Branch", TripRatingAmps = 200,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };

        var result = SelectiveCoordinationService.EvaluateAcrossRange(upstream, downstream, 20.0, 5);
        Assert.True(result.Ratio > 0, "Should have a valid ratio");
    }

    [Fact]
    public void EvaluateAcrossRange_WellSized_Coordinated()
    {
        var upstream = new ProtectiveDevice
        {
            Id = "U", Name = "Main", TripRatingAmps = 2000,
            DeviceType = OCPDType.PowerCircuitBreaker,
            ShortTimeDelayCycles = 12,
        };
        var downstream = new ProtectiveDevice
        {
            Id = "D", Name = "Branch", TripRatingAmps = 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };

        var result = SelectiveCoordinationService.EvaluateAcrossRange(upstream, downstream, 10.0, 10);
        Assert.True(result.IsCoordinated, $"Well-sized pair should coordinate: ratio={result.Ratio}");
    }

    // ── Tree Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeTree_NoChildren_NoViolations()
    {
        var root = new DistributionNode
        {
            Id = "MDP", Name = "Main", NodeType = ComponentType.Panel,
            FaultCurrentKA = 22.0,
            Component = MakePanel("MDP", 2000),
        };

        var violations = SelectiveCoordinationService.AnalyzeTree(
            new[] { root },
            n => MakeDevice(n));

        Assert.Empty(violations);
    }

    [Fact]
    public void AnalyzeTree_WellCoordinated_NoViolations()
    {
        var child = new DistributionNode
        {
            Id = "P1", Name = "Panel1", NodeType = ComponentType.Panel,
            FaultCurrentKA = 10.0,
            Component = MakePanel("P1", 100),
        };
        var root = new DistributionNode
        {
            Id = "MDP", Name = "Main", NodeType = ComponentType.Panel,
            FaultCurrentKA = 22.0,
            Component = MakePanel("MDP", 2000),
            Children = { child },
        };

        var violations = SelectiveCoordinationService.AnalyzeTree(
            new[] { root },
            n => new ProtectiveDevice
            {
                Id = n.Id,
                Name = n.Name,
                TripRatingAmps = n.Component is PanelComponent p ? (int)p.BusAmpacity : 100,
                DeviceType = n.Id == "MDP" ? OCPDType.PowerCircuitBreaker : OCPDType.MoldedCaseBreaker,
                ShortTimeDelayCycles = n.Id == "MDP" ? 12 : 0,
            });

        Assert.Empty(violations);
    }

    [Fact]
    public void AnalyzeTree_SameSizeDevices_FindsViolation()
    {
        var child = new DistributionNode
        {
            Id = "P1", Name = "Panel1", NodeType = ComponentType.Panel,
            FaultCurrentKA = 10.0,
            Component = MakePanel("P1", 200),
        };
        var root = new DistributionNode
        {
            Id = "MDP", Name = "Main", NodeType = ComponentType.Panel,
            FaultCurrentKA = 22.0,
            Component = MakePanel("MDP", 200),
            Children = { child },
        };

        var violations = SelectiveCoordinationService.AnalyzeTree(
            new[] { root },
            n => new ProtectiveDevice
            {
                Id = n.Id,
                Name = n.Name,
                TripRatingAmps = 200,
                DeviceType = OCPDType.MoldedCaseBreaker,
            });

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void AnalyzeTree_NullDevice_Skipped()
    {
        var child = new DistributionNode
        {
            Id = "P1", Name = "Panel1", NodeType = ComponentType.Panel,
            FaultCurrentKA = 10.0,
            Component = MakePanel("P1", 100),
        };
        var root = new DistributionNode
        {
            Id = "MDP", Name = "Main", NodeType = ComponentType.Panel,
            FaultCurrentKA = 22.0,
            Component = MakePanel("MDP", 2000),
            Children = { child },
        };

        // getDevice returns null → should skip, no violations
        var violations = SelectiveCoordinationService.AnalyzeTree(
            new[] { root },
            n => (ProtectiveDevice?)null);

        Assert.Empty(violations);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PanelComponent MakePanel(string name, double busAmps)
    {
        return new PanelComponent
        {
            Name = name,
            BusAmpacity = busAmps,
            AICRatingKA = 22.0,
        };
    }

    private static ProtectiveDevice MakeDevice(DistributionNode n)
    {
        return new ProtectiveDevice
        {
            Id = n.Id,
            Name = n.Name,
            TripRatingAmps = n.Component is PanelComponent p ? (int)p.BusAmpacity : 100,
            DeviceType = OCPDType.MoldedCaseBreaker,
        };
    }
}
