using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class OneLineDiagramServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PowerSourceComponent MakeSource(string id = "SRC-1", double kva = 1500, double voltage = 480) =>
        new() { Id = id, Name = $"UTIL {id}", KVA = kva, Voltage = voltage };

    private static PanelComponent MakePanel(string id, string name, PanelSubtype subtype = PanelSubtype.Panelboard) =>
        new() { Id = id, Name = name, Subtype = subtype, BusAmpacity = 200, AICRatingKA = 22 };

    private static PanelSchedule MakeSchedule(string panelId, int circuits = 4, double vaPerCircuit = 1800)
    {
        var sched = new PanelSchedule
        {
            PanelId = panelId,
            PanelName = panelId,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            MainBreakerAmps = 200,
            BusAmps = 200,
        };
        for (int i = 1; i <= circuits; i++)
        {
            sched.Circuits.Add(new Circuit
            {
                CircuitNumber = i.ToString(),
                ConnectedLoadVA = vaPerCircuit,
                DemandFactor = 1.0,
                Poles = 1,
                Phase = "A",
                SlotType = CircuitSlotType.Circuit,
            });
        }
        return sched;
    }

    private static DistributionNode MakeTree()
    {
        var source = MakeSource();
        var swb = MakePanel("SWB-1", "Main Switchboard", PanelSubtype.Switchboard);
        var pnl1 = MakePanel("PNL-1", "Panel LP-1");

        var root = new DistributionNode { Id = source.Id, Name = source.Name, NodeType = ComponentType.PowerSource, Component = source, CumulativeDemandVA = 10000 };
        var swbNode = new DistributionNode { Id = swb.Id, Name = swb.Name, NodeType = ComponentType.Panel, Component = swb, PanelSubtype = PanelSubtype.Switchboard, CumulativeDemandVA = 10000 };
        var pnlNode = new DistributionNode { Id = pnl1.Id, Name = pnl1.Name, NodeType = ComponentType.Panel, Component = pnl1, PanelSubtype = PanelSubtype.Panelboard, CumulativeDemandVA = 7200, FaultCurrentKA = 14 };

        root.Children.Add(swbNode);
        swbNode.Children.Add(pnlNode);
        return root;
    }

    // ── Generate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SingleSource_ReturnsOneRoot()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root }, projectName: "Test Project");

        Assert.Single(diagram.Roots);
        Assert.Equal("Test Project", diagram.ProjectName);
    }

    [Fact]
    public void Generate_CountsNodesCorrectly()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Equal(3, diagram.TotalNodes);
    }

    [Fact]
    public void Generate_CalculatesMaxDepth()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Equal(2, diagram.MaxDepth);
    }

    [Fact]
    public void Generate_CreatesEdges()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Equal(2, diagram.Edges.Count);
        Assert.Contains(diagram.Edges, e => e.FromNodeId == "SRC-1" && e.ToNodeId == "SWB-1");
        Assert.Contains(diagram.Edges, e => e.FromNodeId == "SWB-1" && e.ToNodeId == "PNL-1");
    }

    [Fact]
    public void Generate_AssignsSymbolTypes()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Equal(OneLineDiagramService.SymbolType.UtilitySource, diagram.Roots[0].Symbol);
        Assert.Equal(OneLineDiagramService.SymbolType.Switchboard, diagram.Roots[0].Children[0].Symbol);
        Assert.Equal(OneLineDiagramService.SymbolType.Panelboard, diagram.Roots[0].Children[0].Children[0].Symbol);
    }

    [Fact]
    public void Generate_WithSchedules_PopulatesLoadVA()
    {
        var root = MakeTree();
        var schedules = new[] { MakeSchedule("PNL-1", 4, 1800) };

        var diagram = OneLineDiagramService.Generate(new[] { root }, schedules, "Test");
        var flat = OneLineDiagramService.Flatten(diagram);
        var pnl = flat.First(n => n.Id == "PNL-1");

        Assert.Equal(7200, pnl.ConnectedLoadVA);
        Assert.Equal(7200, pnl.DemandLoadVA); // demand factor = 1.0
    }

    [Fact]
    public void Generate_SourceGetsKVAAndVoltage()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Equal(1500, diagram.Roots[0].KVA);
        Assert.Equal(480, diagram.Roots[0].VoltageV);
    }

    [Fact]
    public void Generate_PanelGetsBusAmpsAndAIC()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });
        var swb = diagram.Roots[0].Children[0];

        Assert.Equal(200, swb.BusAmps);
        Assert.Equal(22, swb.AICRatingKA);
    }

    [Fact]
    public void Generate_SetsParentIdOnChildren()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });

        Assert.Null(diagram.Roots[0].ParentId);
        Assert.Equal("SRC-1", diagram.Roots[0].Children[0].ParentId);
        Assert.Equal("SWB-1", diagram.Roots[0].Children[0].Children[0].ParentId);
    }

    [Fact]
    public void Generate_EmptyRoots_ReturnsEmptyDiagram()
    {
        var diagram = OneLineDiagramService.Generate(Array.Empty<DistributionNode>());

        Assert.Empty(diagram.Roots);
        Assert.Equal(0, diagram.TotalNodes);
        Assert.Equal(0, diagram.MaxDepth);
    }

    // ── Flatten ──────────────────────────────────────────────────────────────

    [Fact]
    public void Flatten_ReturnsAllNodesInDepthFirstOrder()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });
        var flat = OneLineDiagramService.Flatten(diagram);

        Assert.Equal(3, flat.Count);
        Assert.Equal("SRC-1", flat[0].Id);
        Assert.Equal("SWB-1", flat[1].Id);
        Assert.Equal("PNL-1", flat[2].Id);
    }

    // ── Text Diagram ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateTextDiagram_ContainsProjectName()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root }, projectName: "My Project");
        var text = OneLineDiagramService.GenerateTextDiagram(diagram);

        Assert.Contains("My Project", text);
        Assert.Contains("ONE-LINE DIAGRAM", text);
    }

    [Fact]
    public void GenerateTextDiagram_ContainsNodeLabels()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });
        var text = OneLineDiagramService.GenerateTextDiagram(diagram);

        Assert.Contains("UTIL SRC-1", text);
        Assert.Contains("Main Switchboard", text);
        Assert.Contains("Panel LP-1", text);
    }

    [Fact]
    public void GenerateTextDiagram_IndentsChildNodes()
    {
        var root = MakeTree();
        var diagram = OneLineDiagramService.Generate(new[] { root });
        var text = OneLineDiagramService.GenerateTextDiagram(diagram);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var swbLine = lines.First(l => l.Contains("Main Switchboard"));
        var pnlLine = lines.First(l => l.Contains("Panel LP-1"));

        // Child (depth 1) has more leading spaces than root (depth 0)
        int swbIndent = swbLine.Length - swbLine.TrimStart().Length;
        int pnlIndent = pnlLine.Length - pnlLine.TrimStart().Length;
        Assert.True(pnlIndent > swbIndent);
    }

    // ── Multiple Roots ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_MultipleRoots_HandlesCorrectly()
    {
        var src1 = MakeSource("SRC-1");
        var src2 = MakeSource("SRC-2");
        var root1 = new DistributionNode { Id = src1.Id, Name = src1.Name, NodeType = ComponentType.PowerSource, Component = src1 };
        var root2 = new DistributionNode { Id = src2.Id, Name = src2.Name, NodeType = ComponentType.PowerSource, Component = src2 };

        var diagram = OneLineDiagramService.Generate(new[] { root1, root2 });

        Assert.Equal(2, diagram.Roots.Count);
        Assert.Equal(2, diagram.TotalNodes);
        Assert.Equal(0, diagram.MaxDepth);
    }

    // ── MCC / TransferSwitch symbol mapping ─────────────────────────────────

    [Fact]
    public void Generate_MCCSectionPanel_MapsMCCSymbol()
    {
        var panel = MakePanel("MCC-1", "Motor Control Center", PanelSubtype.MCCSection);
        var node = new DistributionNode { Id = panel.Id, Name = panel.Name, NodeType = ComponentType.Panel, Component = panel, PanelSubtype = PanelSubtype.MCCSection };

        var diagram = OneLineDiagramService.Generate(new[] { node });

        Assert.Equal(OneLineDiagramService.SymbolType.MCC, diagram.Roots[0].Symbol);
    }

    [Fact]
    public void Generate_TransferSwitchPanel_MapsTransferSwitchSymbol()
    {
        var panel = MakePanel("ATS-1", "Auto Transfer Switch", PanelSubtype.TransferSwitch);
        var node = new DistributionNode { Id = panel.Id, Name = panel.Name, NodeType = ComponentType.Panel, Component = panel, PanelSubtype = PanelSubtype.TransferSwitch };

        var diagram = OneLineDiagramService.Generate(new[] { node });

        Assert.Equal(OneLineDiagramService.SymbolType.TransferSwitch, diagram.Roots[0].Symbol);
    }

    // ── TotalConnectedVA / TotalDemandVA ─────────────────────────────────────

    [Fact]
    public void Generate_TotalVA_SumsAcrossTree()
    {
        var root = MakeTree();
        var schedules = new[]
        {
            MakeSchedule("SWB-1", 2, 5000),
            MakeSchedule("PNL-1", 4, 1800),
        };

        var diagram = OneLineDiagramService.Generate(new[] { root }, schedules);

        Assert.Equal(17200, diagram.TotalConnectedVA); // 10000 + 7200
        Assert.Equal(17200, diagram.TotalDemandVA);     // all demand factors = 1.0
    }
}
