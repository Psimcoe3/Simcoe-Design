using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DistributionGraphServiceTests
{
    private readonly DistributionGraphService _svc = new();

    // ── BuildGraph: basic tree ───────────────────────────────────────────────

    [Fact]
    public void BuildGraph_SinglePowerSource_ReturnsOneRoot()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var roots = _svc.BuildGraph(new[] { source });

        Assert.Single(roots);
        Assert.Equal("src", roots[0].Id);
        Assert.Equal(ComponentType.PowerSource, roots[0].NodeType);
    }

    [Fact]
    public void BuildGraph_PanelFedBySource_FormsTwoLevelTree()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var panel = new PanelComponent { Id = "p1", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, panel });

        Assert.Single(roots);
        Assert.Single(roots[0].Children);
        Assert.Equal("p1", roots[0].Children[0].Id);
    }

    [Fact]
    public void BuildGraph_ThreeLevelHierarchy_SourceTransformerPanel()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var xfmr = new TransformerComponent { Id = "t1", FeederId = "src" };
        var panel = new PanelComponent { Id = "p1", FeederId = "t1" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, xfmr, panel });

        Assert.Single(roots);
        Assert.Single(roots[0].Children); // transformer
        Assert.Single(roots[0].Children[0].Children); // panel
        Assert.Equal("p1", roots[0].Children[0].Children[0].Id);
    }

    [Fact]
    public void BuildGraph_BusWithMultiplePanels_FormsStarTopology()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var bus = new BusComponent { Id = "b1", FeederId = "src" };
        var p1 = new PanelComponent { Id = "p1", FeederId = "b1" };
        var p2 = new PanelComponent { Id = "p2", FeederId = "b1" };
        var p3 = new PanelComponent { Id = "p3", FeederId = "b1" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, bus, p1, p2, p3 });

        Assert.Single(roots);
        var busNode = roots[0].Children[0];
        Assert.Equal(3, busNode.Children.Count);
    }

    [Fact]
    public void BuildGraph_TransferSwitchUsesNormalFeederId()
    {
        var src1 = new PowerSourceComponent { Id = "utility" };
        var src2 = new PowerSourceComponent { Id = "generator" };
        var ts = new TransferSwitchComponent
        {
            Id = "ats",
            NormalFeederId = "utility",
            AlternateFeederId = "generator"
        };
        var panel = new PanelComponent { Id = "p1", FeederId = "ats" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { src1, src2, ts, panel });

        // utility is parent of ATS; generator is a separate root
        var utilityRoot = roots.First(r => r.Id == "utility");
        Assert.Single(utilityRoot.Children);
        Assert.Equal("ats", utilityRoot.Children[0].Id);
        Assert.Single(utilityRoot.Children[0].Children);
        Assert.Equal("p1", utilityRoot.Children[0].Children[0].Id);

        var genRoot = roots.First(r => r.Id == "generator");
        Assert.Empty(genRoot.Children);
    }

    [Fact]
    public void BuildGraph_IgnoresNonDistributionComponents()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var conduit = new ConduitComponent { Id = "c1" };
        var box = new BoxComponent { Id = "b1" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, conduit, box });

        Assert.Single(roots);
        Assert.Equal("src", roots[0].Id);
    }

    [Fact]
    public void BuildGraph_UnfedPanel_BecomesRoot()
    {
        var panel = new PanelComponent { Id = "p1" };

        var roots = _svc.BuildGraph(new[] { (ElectricalComponent)panel });

        Assert.Single(roots);
        Assert.Equal("p1", roots[0].Id);
    }

    // ── Cycle detection ──────────────────────────────────────────────────────

    [Fact]
    public void DetectCycles_NoCycles_ReturnsEmpty()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var panel = new PanelComponent { Id = "p1", FeederId = "src" };

        var cycles = _svc.DetectCycles(new ElectricalComponent[] { source, panel });

        Assert.Empty(cycles);
    }

    [Fact]
    public void DetectCycles_DirectCycle_TwoPanels_DetectsParticipant()
    {
        // p1 → p2, p2 → p1 (circular)
        var p1 = new PanelComponent { Id = "p1", FeederId = "p2" };
        var p2 = new PanelComponent { Id = "p2", FeederId = "p1" };

        var cycles = _svc.DetectCycles(new ElectricalComponent[] { p1, p2 });

        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void DetectCycles_SelfReference_Detected()
    {
        var panel = new PanelComponent { Id = "p1", FeederId = "p1" };

        var cycles = _svc.DetectCycles(new[] { (ElectricalComponent)panel });

        Assert.Contains("p1", cycles);
    }

    // ── Cumulative demand ────────────────────────────────────────────────────

    [Fact]
    public void ComputeCumulativeDemand_LeafPanelsAccumulate()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var p1 = new PanelComponent { Id = "p1", FeederId = "src" };
        var p2 = new PanelComponent { Id = "p2", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, p1, p2 });
        var panelDemand = new Dictionary<string, double>
        {
            ["p1"] = 10_000,
            ["p2"] = 25_000
        };
        _svc.ComputeCumulativeDemand(roots, panelDemand);

        Assert.Equal(35_000, roots[0].CumulativeDemandVA, precision: 2);
        Assert.Equal(10_000, roots[0].Children.First(c => c.Id == "p1").CumulativeDemandVA, precision: 2);
        Assert.Equal(25_000, roots[0].Children.First(c => c.Id == "p2").CumulativeDemandVA, precision: 2);
    }

    [Fact]
    public void ComputeCumulativeDemand_NestedHierarchy_SumsCorrectly()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var xfmr = new TransformerComponent { Id = "t1", FeederId = "src" };
        var p1 = new PanelComponent { Id = "p1", FeederId = "t1" };
        var p2 = new PanelComponent { Id = "p2", FeederId = "t1" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, xfmr, p1, p2 });
        var panelDemand = new Dictionary<string, double>
        {
            ["p1"] = 5_000,
            ["p2"] = 8_000
        };
        _svc.ComputeCumulativeDemand(roots, panelDemand);

        // source → xfmr(13000) → p1(5000), p2(8000)
        Assert.Equal(13_000, roots[0].Children[0].CumulativeDemandVA, precision: 2);
        Assert.Equal(13_000, roots[0].CumulativeDemandVA, precision: 2);
    }

    [Fact]
    public void ComputeCumulativeDemand_NoPanelDemandMap_AllZero()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var panel = new PanelComponent { Id = "p1", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, panel });
        _svc.ComputeCumulativeDemand(roots);

        Assert.Equal(0, roots[0].CumulativeDemandVA);
    }

    // ── Fault current propagation ────────────────────────────────────────────

    [Fact]
    public void PropagateFaultCurrent_SourceToPanel_InheritsFaultCurrent()
    {
        var source = new PowerSourceComponent { Id = "src", AvailableFaultCurrentKA = 65 };
        var panel = new PanelComponent { Id = "p1", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, panel });
        _svc.PropagateFaultCurrent(roots);

        Assert.Equal(65, roots[0].FaultCurrentKA, precision: 2);
        Assert.Equal(65, roots[0].Children[0].FaultCurrentKA, precision: 2);
    }

    [Fact]
    public void PropagateFaultCurrent_TransformerReducesFaultCurrent()
    {
        var source = new PowerSourceComponent { Id = "src", AvailableFaultCurrentKA = 65 };
        var xfmr = new TransformerComponent
        {
            Id = "t1",
            FeederId = "src",
            KVA = 75,
            SecondaryVoltage = 208,
            ImpedancePercent = 5.75
        };
        var panel = new PanelComponent { Id = "p1", FeederId = "t1" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, xfmr, panel });
        _svc.PropagateFaultCurrent(roots);

        // Expected: (75 * 1000) / (208 * √3 * 0.0575) = 75000 / 20.704... = 3621.6 A → 3.62 kA
        double expectedKA = (75.0 * 1000) / (208 * Math.Sqrt(3) * 0.0575) / 1000;
        Assert.Equal(expectedKA, roots[0].Children[0].FaultCurrentKA, precision: 1);

        // Panel downstream of transformer inherits transformer's secondary fault
        Assert.Equal(expectedKA, roots[0].Children[0].Children[0].FaultCurrentKA, precision: 1);
    }

    // ── Feeder schedule generation ───────────────────────────────────────────

    [Fact]
    public void GenerateFeederSchedule_FlatList_CorrectDepths()
    {
        var source = new PowerSourceComponent { Id = "src" };
        var xfmr = new TransformerComponent { Id = "t1", FeederId = "src" };
        var p1 = new PanelComponent { Id = "p1", FeederId = "t1" };
        var p2 = new PanelComponent { Id = "p2", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, xfmr, p1, p2 });
        var schedule = _svc.GenerateFeederSchedule(roots);

        Assert.Equal(4, schedule.Count);

        var srcEntry = schedule.First(e => e.ComponentId == "src");
        Assert.Equal(0, srcEntry.Depth);

        var xfmrEntry = schedule.First(e => e.ComponentId == "t1");
        Assert.Equal(1, xfmrEntry.Depth);

        var p1Entry = schedule.First(e => e.ComponentId == "p1");
        Assert.Equal(2, p1Entry.Depth);

        var p2Entry = schedule.First(e => e.ComponentId == "p2");
        Assert.Equal(1, p2Entry.Depth);
    }

    [Fact]
    public void GenerateFeederSchedule_IncludesCumulativeDemandAndFault()
    {
        var source = new PowerSourceComponent { Id = "src", AvailableFaultCurrentKA = 65 };
        var panel = new PanelComponent { Id = "p1", FeederId = "src" };

        var roots = _svc.BuildGraph(new ElectricalComponent[] { source, panel });
        _svc.ComputeCumulativeDemand(roots, new Dictionary<string, double> { ["p1"] = 12_000 });
        _svc.PropagateFaultCurrent(roots);
        var schedule = _svc.GenerateFeederSchedule(roots);

        var panelEntry = schedule.First(e => e.ComponentId == "p1");
        Assert.Equal(12_000, panelEntry.CumulativeDemandVA, precision: 2);
        Assert.Equal(65, panelEntry.FaultCurrentKA, precision: 2);
    }

    [Fact]
    public void GenerateFeederSchedule_EmptyComponents_ReturnsEmpty()
    {
        var roots = _svc.BuildGraph(Array.Empty<ElectricalComponent>());
        var schedule = _svc.GenerateFeederSchedule(roots);

        Assert.Empty(schedule);
    }

    // ── FeederId property verification ───────────────────────────────────────

    [Fact]
    public void PanelComponent_FeederId_DefaultsToNull()
    {
        var panel = new PanelComponent();
        Assert.Null(panel.FeederId);
    }

    [Fact]
    public void TransformerComponent_Properties_Defaults()
    {
        var xfmr = new TransformerComponent();
        Assert.Equal(480, xfmr.PrimaryVoltage);
        Assert.Equal(208, xfmr.SecondaryVoltage);
        Assert.Equal(75, xfmr.KVA);
        Assert.Equal(5.75, xfmr.ImpedancePercent);
        Assert.Equal(ComponentType.Transformer, xfmr.Type);
    }

    [Fact]
    public void BusComponent_Properties_Defaults()
    {
        var bus = new BusComponent();
        Assert.Equal(800, bus.BusAmps);
        Assert.Equal(480, bus.Voltage);
        Assert.Equal(ComponentType.Bus, bus.Type);
    }

    [Fact]
    public void PowerSourceComponent_Properties_Defaults()
    {
        var src = new PowerSourceComponent();
        Assert.Equal(65, src.AvailableFaultCurrentKA);
        Assert.Equal(480, src.Voltage);
        Assert.Equal(1500, src.KVA);
        Assert.Equal(ComponentType.PowerSource, src.Type);
    }

    [Fact]
    public void TransferSwitchComponent_Properties_Defaults()
    {
        var ts = new TransferSwitchComponent();
        Assert.Null(ts.NormalFeederId);
        Assert.Null(ts.AlternateFeederId);
        Assert.Equal(400, ts.AmpsRating);
        Assert.Equal(ComponentType.TransferSwitch, ts.Type);
    }
}
