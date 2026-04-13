using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.AutoConductorSizingService;

namespace ElectricalComponentSandbox.Tests.Services;

public class AutoConductorSizingServiceTests
{
    // ── Single Feeder Sizing ─────────────────────────────────────────────────

    [Fact]
    public void SizeFeeder_SmallLoad_SmallWire()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 15, lengthFeet: 50, voltage: 480, poles: 3);

        Assert.Equal("14", result.RecommendedPhaseSize);
        Assert.True(result.AmpacityOfRecommended >= 15);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SizeFeeder_200A_ReasonableSize()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 200, lengthFeet: 100, voltage: 480, poles: 3);

        // 200A on 75°C copper → 3/0 AWG (200A) or larger
        int idx = Array.IndexOf(new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0" }, result.RecommendedPhaseSize);
        Assert.True(idx >= 9, $"200A should need at least 1/0, got {result.RecommendedPhaseSize}");
        Assert.True(result.AmpacityOfRecommended >= 200);
    }

    [Fact]
    public void SizeFeeder_LongRun_VoltageDropGoverns()
    {
        // 100A over 500' on 208V → voltage drop likely dictates larger wire
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 500, voltage: 208, poles: 3);

        Assert.True(result.VoltageDropGoverning, "Long run should be VD-governed");
        Assert.True(result.SegmentVoltageDropPercent <= 3.0,
            $"VD should be ≤ 3%, got {result.SegmentVoltageDropPercent}%");
    }

    [Fact]
    public void SizeFeeder_ShortRun_AmpacityGoverns()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 20, voltage: 480, poles: 3);

        Assert.True(result.AmpacityGoverning, "Short run should be ampacity-governed");
    }

    [Fact]
    public void SizeFeeder_IncludesGroundSize()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3);

        Assert.False(string.IsNullOrEmpty(result.RecommendedGroundSize));
    }

    [Fact]
    public void SizeFeeder_OCPDSized()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 85, lengthFeet: 50, voltage: 480, poles: 3);

        Assert.True(result.OCPDAmps >= 85, $"OCPD {result.OCPDAmps} should be ≥ 85A load");
        // Should be a standard size
        Assert.Contains(result.OCPDAmps, new[] { 15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100, 110, 125, 150, 175, 200 });
    }

    [Fact]
    public void SizeFeeder_CumulativeVD_Tracked()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 50, lengthFeet: 100, voltage: 480, poles: 3,
            upstreamCumulativeVDPercent: 2.5);

        Assert.True(result.CumulativeVoltageDropPercent > 2.5);
    }

    [Fact]
    public void SizeFeeder_CumulativeVD_WarningWhenExceedsTotal()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 50, lengthFeet: 100, voltage: 208, poles: 3,
            upstreamCumulativeVDPercent: 4.0);

        if (result.CumulativeVoltageDropPercent > 5.0)
        {
            Assert.Contains(result.Warnings, w => w.Contains("Cumulative"));
        }
    }

    // ── Derating Parameters ──────────────────────────────────────────────────

    [Fact]
    public void SizeFeeder_HighAmbientTemp_LargerWire()
    {
        var normal = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { AmbientTempC = 30 });

        var hot = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { AmbientTempC = 45 });

        int normalIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            normal.RecommendedPhaseSize);
        int hotIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            hot.RecommendedPhaseSize);

        Assert.True(hotIdx >= normalIdx, "High ambient temp should require same or larger wire");
    }

    [Fact]
    public void SizeFeeder_BundleDerating_LargerWire()
    {
        var normal = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { ConductorsInRaceway = 3 });

        var bundled = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { ConductorsInRaceway = 10 });

        int normalIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            normal.RecommendedPhaseSize);
        int bundledIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            bundled.RecommendedPhaseSize);

        Assert.True(bundledIdx >= normalIdx, "Bundle derating should require same or larger wire");
    }

    [Fact]
    public void SizeFeeder_AluminumConductor()
    {
        var copper = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { Material = ConductorMaterial.Copper });

        var aluminum = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 50, voltage: 480, poles: 3,
            parameters: new SizingParameters { Material = ConductorMaterial.Aluminum });

        // Aluminum needs larger wire for same ampacity
        int cuIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            copper.RecommendedPhaseSize);
        int alIdx = Array.IndexOf(
            new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500" },
            aluminum.RecommendedPhaseSize);

        Assert.True(alIdx >= cuIdx, "Aluminum should require same or larger wire than copper");
    }

    // ── System-Wide Sizing ───────────────────────────────────────────────────

    private static DistributionNode MakeNode(string id, string name, ComponentType type, double demandVA, double faultKA = 0, params DistributionNode[] children)
    {
        var node = new DistributionNode
        {
            Id = id, Name = name, NodeType = type,
            CumulativeDemandVA = demandVA, FaultCurrentKA = faultKA,
        };
        foreach (var c in children) node.Children.Add(c);
        return node;
    }

    [Fact]
    public void SizeAllFeeders_SimpleTree()
    {
        var lp1 = MakeNode("LP1", "Lighting Panel", ComponentType.Panel, 30000);
        var pp1 = MakeNode("PP1", "Power Panel", ComponentType.Panel, 50000);
        var mdp = MakeNode("MDP", "Main Panel", ComponentType.Panel, 100000, 0, lp1, pp1);
        var source = MakeNode("SRC", "Utility Source", ComponentType.PowerSource, 100000, 42, mdp);

        var lengths = new Dictionary<string, double>
        {
            ["MDP"] = 50,
            ["LP1"] = 100,
            ["PP1"] = 75,
        };

        var result = AutoConductorSizingService.SizeAllFeeders(
            new List<DistributionNode> { source }, lengths);

        Assert.Equal(3, result.TotalFeedersAnalyzed); // SRC→MDP, MDP→LP1, MDP→PP1
        Assert.True(result.FeederResults.All(r => !string.IsNullOrEmpty(r.RecommendedPhaseSize)));
        Assert.True(result.FeederResults.All(r => !string.IsNullOrEmpty(r.RecommendedGroundSize)));
    }

    [Fact]
    public void SizeAllFeeders_TracksWorstVD()
    {
        var p2 = MakeNode("P2", "Remote Sub-Panel", ComponentType.Panel, 20000);
        var p1 = MakeNode("P1", "Far Panel", ComponentType.Panel, 50000, 0, p2);
        var source = MakeNode("SRC", "Source", ComponentType.PowerSource, 50000, 0, p1);

        var lengths = new Dictionary<string, double>
        {
            ["P1"] = 200,
            ["P2"] = 150,
        };

        var result = AutoConductorSizingService.SizeAllFeeders(
            new List<DistributionNode> { source }, lengths);

        Assert.True(result.WorstCumulativeVoltageDropPercent > 0);
        Assert.NotNull(result.WorstVoltageDropNodeId);
    }

    [Fact]
    public void SizeAllFeeders_Empty_ReturnsEmpty()
    {
        var result = AutoConductorSizingService.SizeAllFeeders(
            new List<DistributionNode>(), new Dictionary<string, double>());

        Assert.Equal(0, result.TotalFeedersAnalyzed);
        Assert.Empty(result.FeederResults);
    }

    [Fact]
    public void SizeAllFeeders_LargerLoad_LargerWire()
    {
        var smallChild = MakeNode("P1", "Panel", ComponentType.Panel, 20000);
        var small = MakeNode("SRC", "Src", ComponentType.PowerSource, 20000, 0, smallChild);

        var largeChild = MakeNode("P1", "Panel", ComponentType.Panel, 200000);
        var large = MakeNode("SRC", "Src", ComponentType.PowerSource, 200000, 0, largeChild);

        var lengths = new Dictionary<string, double> { ["P1"] = 100 };

        var smallResult = AutoConductorSizingService.SizeAllFeeders(new() { small }, lengths);
        var largeResult = AutoConductorSizingService.SizeAllFeeders(new() { large }, lengths);

        var sizes = new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500", "600", "700", "750", "1000" };
        int smallIdx = Array.IndexOf(sizes, smallResult.FeederResults[0].RecommendedPhaseSize);
        int largeIdx = Array.IndexOf(sizes, largeResult.FeederResults[0].RecommendedPhaseSize);

        Assert.True(largeIdx > smallIdx, "Larger load should require larger wire");
    }

    // ── Real-World Scenario ──────────────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialBuilding_480V()
    {
        // 400A service → MDP → {lighting 75kVA, mechanical 150kVA, receptacle 50kVA}
        var lp = MakeNode("LP", "Lighting Panel", ComponentType.Panel, 75000);
        var mp = MakeNode("MP", "Mechanical Panel", ComponentType.Panel, 150000);
        var rp = MakeNode("RP", "Receptacle Panel", ComponentType.Panel, 50000);
        var mdp = MakeNode("MDP", "Main Distribution Panel", ComponentType.Panel, 275000, 35, lp, mp, rp);
        var source = MakeNode("UTIL", "Utility Service", ComponentType.PowerSource, 275000, 42, mdp);

        var lengths = new Dictionary<string, double>
        {
            ["MDP"] = 25,
            ["LP"] = 120,
            ["MP"] = 80,
            ["RP"] = 150,
        };

        var result = AutoConductorSizingService.SizeAllFeeders(
            new List<DistributionNode> { source }, lengths);

        Assert.Equal(4, result.TotalFeedersAnalyzed);
        Assert.Equal(0, result.VoltageDropViolations);
        Assert.True(result.WorstCumulativeVoltageDropPercent <= 5.0,
            $"Total VD {result.WorstCumulativeVoltageDropPercent}% should be ≤ 5%");

        // MDP feeder should be largest
        var mdpFeeder = result.FeederResults.First(r => r.ToNodeId == "MDP");
        var lpFeeder = result.FeederResults.First(r => r.ToNodeId == "LP");
        var sizes = new[] { "14", "12", "10", "8", "6", "4", "3", "2", "1", "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500", "600", "700", "750", "1000" };
        Assert.True(
            Array.IndexOf(sizes, mdpFeeder.RecommendedPhaseSize) > Array.IndexOf(sizes, lpFeeder.RecommendedPhaseSize),
            "MDP feeder should be larger than lighting panel feeder");
    }

    [Fact]
    public void SizeFeeder_ZeroLength_NoVDCalc()
    {
        var result = AutoConductorSizingService.SizeFeeder(
            loadAmps: 100, lengthFeet: 0, voltage: 480, poles: 3);

        Assert.Equal(0, result.SegmentVoltageDropPercent);
        Assert.True(result.AmpacityGoverning);
    }
}
