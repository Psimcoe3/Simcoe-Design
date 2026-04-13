using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generates a structured one-line diagram data model from the distribution
/// graph. The output describes nodes, connections, ratings, and protection
/// devices — ready for rendering or export.
///
/// Each <see cref="OneLineDiagramNode"/> maps to a graphical symbol on the
/// one-line. Edges represent feeders between distribution equipment.
/// </summary>
public static class OneLineDiagramService
{
    /// <summary>Symbol type for one-line diagram rendering.</summary>
    public enum SymbolType
    {
        UtilitySource,
        Generator,
        Transformer,
        Switchboard,
        Panelboard,
        MCC,
        TransferSwitch,
        Bus,
        Disconnect,
        Motor,
        Load,
    }

    /// <summary>A node on the one-line diagram.</summary>
    public record OneLineNode
    {
        public string Id { get; init; } = "";
        public string Label { get; init; } = "";
        public SymbolType Symbol { get; init; }
        public int Depth { get; init; }
        public string? ParentId { get; init; }

        // Ratings
        public double VoltageV { get; init; }
        public int Phases { get; init; } = 3;
        public double BusAmps { get; init; }
        public double KVA { get; init; }
        public double FaultCurrentKA { get; init; }

        // Protection
        public double MainBreakerAmps { get; init; }
        public double AICRatingKA { get; init; }

        // Feeder to this node from parent
        public string? FeederWireSize { get; init; }
        public string? FeederConduitSize { get; init; }
        public int FeederSets { get; init; } = 1;

        // Load summary
        public double ConnectedLoadVA { get; init; }
        public double DemandLoadVA { get; init; }

        public List<OneLineNode> Children { get; init; } = new();
    }

    /// <summary>An edge (feeder) between two nodes.</summary>
    public record OneLineEdge
    {
        public string FromNodeId { get; init; } = "";
        public string ToNodeId { get; init; } = "";
        public string WireSize { get; init; } = "";
        public string ConduitSize { get; init; } = "";
        public int Sets { get; init; } = 1;
        public double LengthFeet { get; init; }
        public double LoadAmps { get; init; }
    }

    /// <summary>Complete one-line diagram data model.</summary>
    public record OneLineDiagram
    {
        public string ProjectName { get; init; } = "";
        public List<OneLineNode> Roots { get; init; } = new();
        public List<OneLineEdge> Edges { get; init; } = new();
        public int TotalNodes { get; init; }
        public int MaxDepth { get; init; }
        public double TotalConnectedVA { get; init; }
        public double TotalDemandVA { get; init; }
    }

    /// <summary>
    /// Generates a one-line diagram data model from distribution graph roots.
    /// </summary>
    public static OneLineDiagram Generate(
        IReadOnlyList<DistributionNode> roots,
        IReadOnlyList<PanelSchedule>? schedules = null,
        string projectName = "")
    {
        var scheduleMap = (schedules ?? Array.Empty<PanelSchedule>())
            .ToDictionary(s => s.PanelId, s => s);

        var edges = new List<OneLineEdge>();
        int nodeCount = 0;
        int maxDepth = 0;

        var rootNodes = roots.Select(r => BuildNode(r, null, 0, scheduleMap, edges, ref nodeCount, ref maxDepth)).ToList();

        double totalConnected = SumLoadVA(rootNodes, n => n.ConnectedLoadVA);
        double totalDemand = SumLoadVA(rootNodes, n => n.DemandLoadVA);

        return new OneLineDiagram
        {
            ProjectName = projectName,
            Roots = rootNodes,
            Edges = edges,
            TotalNodes = nodeCount,
            MaxDepth = maxDepth,
            TotalConnectedVA = totalConnected,
            TotalDemandVA = totalDemand,
        };
    }

    /// <summary>
    /// Flattens the diagram tree into a depth-first ordered list.
    /// </summary>
    public static List<OneLineNode> Flatten(OneLineDiagram diagram)
    {
        var result = new List<OneLineNode>();
        foreach (var root in diagram.Roots)
            FlattenNode(root, result);
        return result;
    }

    /// <summary>
    /// Returns a text-based one-line summary for quick review.
    /// </summary>
    public static string GenerateTextDiagram(OneLineDiagram diagram)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ONE-LINE DIAGRAM: {diagram.ProjectName}");
        sb.AppendLine($"Nodes: {diagram.TotalNodes}  Depth: {diagram.MaxDepth}  " +
                       $"Connected: {diagram.TotalConnectedVA:N0} VA  Demand: {diagram.TotalDemandVA:N0} VA");
        sb.AppendLine(new string('─', 60));

        foreach (var root in diagram.Roots)
            AppendNodeText(root, sb, 0);

        return sb.ToString();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private static OneLineNode BuildNode(
        DistributionNode dn,
        string? parentId,
        int depth,
        Dictionary<string, PanelSchedule> scheduleMap,
        List<OneLineEdge> edges,
        ref int nodeCount,
        ref int maxDepth)
    {
        nodeCount++;
        if (depth > maxDepth) maxDepth = depth;

        var symbol = MapSymbol(dn);
        double voltage = 0, busAmps = 0, mainBreaker = 0, aicKA = 0, kva = 0;
        double connectedVA = 0, demandVA = 0;

        if (dn.Component is PanelComponent panel)
        {
            busAmps = panel.BusAmpacity;
            aicKA = panel.AICRatingKA;

            if (scheduleMap.TryGetValue(panel.Id, out var schedule))
            {
                voltage = GetVoltage(schedule.VoltageConfig);
                mainBreaker = schedule.MainBreakerAmps;
                connectedVA = schedule.Circuits.Where(c => c.SlotType == CircuitSlotType.Circuit)
                    .Sum(c => c.ConnectedLoadVA);
                demandVA = schedule.Circuits.Where(c => c.SlotType == CircuitSlotType.Circuit)
                    .Sum(c => c.ConnectedLoadVA * c.DemandFactor);
            }
        }
        else if (dn.Component is PowerSourceComponent ps)
        {
            voltage = ps.Voltage;
            kva = ps.KVA;
        }

        var childNodes = new List<OneLineNode>(dn.Children.Count);
        foreach (var c in dn.Children)
            childNodes.Add(BuildNode(c, dn.Id, depth + 1, scheduleMap, edges, ref nodeCount, ref maxDepth));

        // Create feeder edge from parent
        if (parentId != null)
        {
            edges.Add(new OneLineEdge
            {
                FromNodeId = parentId,
                ToNodeId = dn.Id,
                LoadAmps = dn.CumulativeDemandVA > 0 && voltage > 0
                    ? dn.CumulativeDemandVA / voltage
                    : 0,
            });
        }

        return new OneLineNode
        {
            Id = dn.Id,
            Label = dn.Name,
            Symbol = symbol,
            Depth = depth,
            ParentId = parentId,
            VoltageV = voltage,
            BusAmps = busAmps,
            KVA = kva,
            FaultCurrentKA = dn.FaultCurrentKA,
            MainBreakerAmps = mainBreaker,
            AICRatingKA = aicKA,
            ConnectedLoadVA = connectedVA,
            DemandLoadVA = demandVA,
            Children = childNodes,
        };
    }

    private static SymbolType MapSymbol(DistributionNode dn)
    {
        if (dn.Component is PowerSourceComponent)
            return SymbolType.UtilitySource;

        if (dn.PanelSubtype.HasValue)
        {
            return dn.PanelSubtype.Value switch
            {
                Models.PanelSubtype.Switchboard => SymbolType.Switchboard,
                Models.PanelSubtype.MCCSection => SymbolType.MCC,
                Models.PanelSubtype.TransferSwitch => SymbolType.TransferSwitch,
                _ => SymbolType.Panelboard,
            };
        }

        return dn.NodeType switch
        {
            ComponentType.Panel => SymbolType.Panelboard,
            ComponentType.Transformer => SymbolType.Transformer,
            _ => SymbolType.Load,
        };
    }

    private static double GetVoltage(PanelVoltageConfig config) => config switch
    {
        PanelVoltageConfig.V120_208_3Ph => 208,
        PanelVoltageConfig.V277_480_3Ph => 480,
        PanelVoltageConfig.V120_240_1Ph => 240,
        PanelVoltageConfig.V240_3Ph => 240,
        _ => 208,
    };

    private static double SumLoadVA(List<OneLineNode> nodes, Func<OneLineNode, double> selector)
    {
        double sum = 0;
        foreach (var n in nodes)
        {
            sum += selector(n);
            sum += SumLoadVA(n.Children, selector);
        }
        return sum;
    }

    private static void FlattenNode(OneLineNode node, List<OneLineNode> result)
    {
        result.Add(node);
        foreach (var child in node.Children)
            FlattenNode(child, result);
    }

    private static void AppendNodeText(OneLineNode node, System.Text.StringBuilder sb, int indent)
    {
        string prefix = new string(' ', indent * 2);
        string sym = node.Symbol.ToString().ToUpperInvariant();
        string voltLabel = node.VoltageV > 0 ? $" {node.VoltageV}V" : "";
        string amps = node.BusAmps > 0 ? $" {node.BusAmps}A" : "";
        string kva = node.KVA > 0 ? $" {node.KVA}kVA" : "";

        sb.AppendLine($"{prefix}[{sym}] {node.Label}{voltLabel}{amps}{kva}");

        foreach (var child in node.Children)
            AppendNodeText(child, sb, indent + 1);
    }
}
