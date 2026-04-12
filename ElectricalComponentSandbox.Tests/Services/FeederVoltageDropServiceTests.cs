using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class FeederVoltageDropServiceTests
{
    // ── Segment Drop Calculation ─────────────────────────────────────────────

    [Fact]
    public void CalculateSegmentDrop_ThreePhase_CorrectFormula()
    {
        // #4/0 Cu, 200ft, 200A, 480V, 3-phase
        // R(4/0) = 0.0608 Ω/1000ft
        // Vd = √3 × 200 × 0.0608 × 200 / 1000 = √3 × 2.432 ≈ 4.213V
        var segment = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        double drop = FeederVoltageDropService.CalculateSegmentDrop(segment);
        Assert.True(drop > 4.0 && drop < 4.5, $"Expected ~4.2V, got {drop:F2}V");
    }

    [Fact]
    public void CalculateSegmentDrop_SinglePhase_UsesMultiplier2()
    {
        var segment = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 240, Poles = 1, LoadAmps = 100,
        };
        double drop = FeederVoltageDropService.CalculateSegmentDrop(segment);
        // Vd = 2 × 100 × 0.0608 × 200 / 1000 = 2.432V
        Assert.True(drop > 2.3 && drop < 2.6);
    }

    [Fact]
    public void CalculateSegmentDrop_ZeroLength_ReturnsZero()
    {
        var segment = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 0, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        Assert.Equal(0, FeederVoltageDropService.CalculateSegmentDrop(segment));
    }

    [Fact]
    public void CalculateSegmentDrop_ZeroAmps_ReturnsZero()
    {
        var segment = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 0,
        };
        Assert.Equal(0, FeederVoltageDropService.CalculateSegmentDrop(segment));
    }

    [Fact]
    public void CalculateSegmentDropPercent_Correct()
    {
        var segment = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        double pct = FeederVoltageDropService.CalculateSegmentDropPercent(segment);
        double expectedDrop = FeederVoltageDropService.CalculateSegmentDrop(segment);
        Assert.Equal((expectedDrop / 480.0) * 100.0, pct);
    }

    [Fact]
    public void CalculateSegmentDrop_LongerRun_HigherDrop()
    {
        var short100 = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 100, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        var long500 = short100 with { LengthFeet = 500 };
        Assert.True(FeederVoltageDropService.CalculateSegmentDrop(long500) >
                     FeederVoltageDropService.CalculateSegmentDrop(short100));
    }

    [Fact]
    public void CalculateSegmentDrop_SmallerWire_HigherDrop()
    {
        var large = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        var small = large with { WireSize = "1" };
        Assert.True(FeederVoltageDropService.CalculateSegmentDrop(small) >
                     FeederVoltageDropService.CalculateSegmentDrop(large));
    }

    [Fact]
    public void CalculateSegmentDrop_Aluminum_HigherThanCopper()
    {
        var cu = new FeederSegment
        {
            WireSize = "4/0", Material = ConductorMaterial.Copper,
            LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 200,
        };
        var al = cu with { Material = ConductorMaterial.Aluminum };
        Assert.True(FeederVoltageDropService.CalculateSegmentDrop(al) >
                     FeederVoltageDropService.CalculateSegmentDrop(cu));
    }

    // ── Tree Analysis ────────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeTree_SingleRoot_NoSegment_ZeroDrop()
    {
        var root = MakeNode("SRC", "Utility Source", ComponentType.PowerSource);
        var results = FeederVoltageDropService.AnalyzeTree(
            new List<DistributionNode> { root },
            new Dictionary<string, FeederSegment>(),
            480);

        Assert.Single(results);
        Assert.Equal(0, results[0].CumulativeDropVolts);
        Assert.Equal(480, results[0].VoltageAtNode);
    }

    [Fact]
    public void AnalyzeTree_TwoLevels_CumulativeDrop()
    {
        var root = MakeNode("SRC", "Source", ComponentType.PowerSource);
        var child = MakeNode("MDP", "Main Panel", ComponentType.Panel);
        root.Children.Add(child);

        var segments = new Dictionary<string, FeederSegment>
        {
            ["MDP"] = new FeederSegment
            {
                FromNodeId = "SRC", ToNodeId = "MDP",
                WireSize = "4/0", Material = ConductorMaterial.Copper,
                LengthFeet = 200, Voltage = 480, Poles = 3, LoadAmps = 200,
            }
        };

        var results = FeederVoltageDropService.AnalyzeTree(
            new List<DistributionNode> { root }, segments, 480);

        Assert.Equal(2, results.Count);
        var mdpResult = results.Find(r => r.NodeId == "MDP")!;
        Assert.True(mdpResult.CumulativeDropVolts > 0);
        Assert.True(mdpResult.VoltageAtNode < 480);
    }

    [Fact]
    public void AnalyzeTree_ThreeLevels_Cumulates()
    {
        var root = MakeNode("SRC", "Source", ComponentType.PowerSource);
        var mid = MakeNode("MDP", "MDP", ComponentType.Panel);
        var leaf = MakeNode("LP1", "Lighting Panel", ComponentType.Panel);
        root.Children.Add(mid);
        mid.Children.Add(leaf);

        var segments = new Dictionary<string, FeederSegment>
        {
            ["MDP"] = new FeederSegment
            {
                WireSize = "4/0", Material = ConductorMaterial.Copper,
                LengthFeet = 100, Voltage = 480, Poles = 3, LoadAmps = 200,
            },
            ["LP1"] = new FeederSegment
            {
                WireSize = "1", Material = ConductorMaterial.Copper,
                LengthFeet = 150, Voltage = 208, Poles = 3, LoadAmps = 60,
            }
        };

        var results = FeederVoltageDropService.AnalyzeTree(
            new List<DistributionNode> { root }, segments, 480);

        var leafResult = results.Find(r => r.NodeId == "LP1")!;
        var midResult = results.Find(r => r.NodeId == "MDP")!;

        Assert.True(leafResult.CumulativeDropVolts > midResult.CumulativeDropVolts);
    }

    [Fact]
    public void AnalyzeTree_HighDrop_FlagsTotalLimit()
    {
        var root = MakeNode("SRC", "Source", ComponentType.PowerSource);
        var child = MakeNode("P1", "Panel", ComponentType.Panel);
        root.Children.Add(child);

        // Use small wire and long run to exceed 5%
        var segments = new Dictionary<string, FeederSegment>
        {
            ["P1"] = new FeederSegment
            {
                WireSize = "8", Material = ConductorMaterial.Copper,
                LengthFeet = 500, Voltage = 480, Poles = 3, LoadAmps = 200,
            }
        };

        var results = FeederVoltageDropService.AnalyzeTree(
            new List<DistributionNode> { root }, segments, 480);
        var panelResult = results.Find(r => r.NodeId == "P1")!;

        Assert.True(panelResult.ExceedsTotalLimit || panelResult.ExceedsBranchLimit);
    }

    // ── Violations ───────────────────────────────────────────────────────────

    [Fact]
    public void GetViolations_NoIssues_Empty()
    {
        var root = MakeNode("SRC", "Source", ComponentType.PowerSource);
        var child = MakeNode("MDP", "MDP", ComponentType.Panel);
        root.Children.Add(child);

        // Short run, large wire → no violation
        var segments = new Dictionary<string, FeederSegment>
        {
            ["MDP"] = new FeederSegment
            {
                WireSize = "500", Material = ConductorMaterial.Copper,
                LengthFeet = 50, Voltage = 480, Poles = 3, LoadAmps = 100,
            }
        };

        var violations = FeederVoltageDropService.GetViolations(
            new List<DistributionNode> { root }, segments, 480);
        Assert.Empty(violations);
    }

    [Fact]
    public void GetViolations_ExceedsLimit_ReturnsNode()
    {
        var root = MakeNode("SRC", "Source", ComponentType.PowerSource);
        var child = MakeNode("P1", "Panel", ComponentType.Panel);
        root.Children.Add(child);

        var segments = new Dictionary<string, FeederSegment>
        {
            ["P1"] = new FeederSegment
            {
                WireSize = "8", Material = ConductorMaterial.Copper,
                LengthFeet = 500, Voltage = 480, Poles = 3, LoadAmps = 200,
            }
        };

        var violations = FeederVoltageDropService.GetViolations(
            new List<DistributionNode> { root }, segments, 480);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.NodeId == "P1");
    }

    // ── Wire Size Recommendation ─────────────────────────────────────────────

    [Fact]
    public void RecommendFeederWireSize_FindsAdequateSize()
    {
        string? size = FeederVoltageDropService.RecommendFeederWireSize(
            200, 200, 480, 3, ConductorMaterial.Copper, 3.0);
        Assert.NotNull(size);
    }

    [Fact]
    public void RecommendFeederWireSize_ShortRun_SmallWire()
    {
        string? shortRun = FeederVoltageDropService.RecommendFeederWireSize(
            100, 50, 480, 3, ConductorMaterial.Copper);
        string? longRun = FeederVoltageDropService.RecommendFeederWireSize(
            100, 500, 480, 3, ConductorMaterial.Copper);

        Assert.NotNull(shortRun);
        Assert.NotNull(longRun);
        // Longer run needs larger wire
        int shortIdx = Array.IndexOf(NecAmpacityService.StandardSizes, shortRun);
        int longIdx = Array.IndexOf(NecAmpacityService.StandardSizes, longRun);
        Assert.True(longIdx >= shortIdx, "Longer run should require same or larger wire");
    }

    [Fact]
    public void RecommendFeederWireSize_ReturnsNull_IfNoSizeAdequate()
    {
        // Extreme scenario: 5000A at 2000ft on 120V — no standard wire will do
        string? size = FeederVoltageDropService.RecommendFeederWireSize(
            5000, 2000, 120, 1, ConductorMaterial.Copper, 0.1);
        Assert.Null(size);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static DistributionNode MakeNode(string id, string name, ComponentType type)
    {
        return new DistributionNode
        {
            Id = id,
            Name = name,
            NodeType = type,
            Component = new PanelComponent { Id = id, Name = name },
        };
    }
}
