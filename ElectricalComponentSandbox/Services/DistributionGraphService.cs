using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// A node in the distribution hierarchy tree.
/// </summary>
public class DistributionNode
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ComponentType NodeType { get; init; }
    public ElectricalComponent Component { get; init; } = null!;
    public List<DistributionNode> Children { get; } = new();

    /// <summary>Panel subtype when <see cref="NodeType"/> is Panel; null otherwise.</summary>
    public PanelSubtype? PanelSubtype { get; init; }

    /// <summary>Cumulative demand load in VA from this node and all downstream children.</summary>
    public double CumulativeDemandVA { get; set; }

    /// <summary>Available fault current in kA at this node (propagated from upstream).</summary>
    public double FaultCurrentKA { get; set; }
}

/// <summary>
/// Row in a feeder schedule report produced by the distribution graph.
/// </summary>
public class FeederScheduleEntry
{
    public string ComponentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ComponentType NodeType { get; init; }
    public PanelSubtype? PanelSubtype { get; init; }
    public int Depth { get; init; }
    public string? FeederId { get; init; }
    public double CumulativeDemandVA { get; init; }
    public double FaultCurrentKA { get; init; }
}

/// <summary>
/// Builds and analyzes the electrical distribution hierarchy from a collection
/// of components. Mirrors Revit's distribution system analytical graph that
/// supports one-line diagrams and feeder schedules.
/// </summary>
public class DistributionGraphService
{
    /// <summary>
    /// Builds a forest of distribution trees from the given components.
    /// Root nodes are power sources or any equipment with no upstream feeder.
    /// Returns the root nodes.
    /// </summary>
    public List<DistributionNode> BuildGraph(IEnumerable<ElectricalComponent> components)
    {
        var list = components.ToList();
        var nodeMap = new Dictionary<string, DistributionNode>();

        // Create nodes for all distribution-relevant components
        foreach (var comp in list)
        {
            if (!IsDistributionNode(comp)) continue;
            nodeMap[comp.Id] = new DistributionNode
            {
                Id = comp.Id,
                Name = comp.Name,
                NodeType = comp.Type,
                PanelSubtype = comp is PanelComponent pc ? pc.Subtype : null,
                Component = comp
            };
        }

        // Wire children by FeederId
        var roots = new List<DistributionNode>();
        foreach (var node in nodeMap.Values)
        {
            var feederId = GetFeederId(node.Component);
            if (feederId != null && nodeMap.TryGetValue(feederId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>
    /// Validates the graph has no circular references.
    /// Returns a list of component IDs that participate in cycles.
    /// </summary>
    public List<string> DetectCycles(IEnumerable<ElectricalComponent> components)
    {
        var list = components.ToList();
        var feederMap = new Dictionary<string, string?>(); // id → feederId

        foreach (var comp in list)
        {
            if (!IsDistributionNode(comp)) continue;
            feederMap[comp.Id] = GetFeederId(comp);
        }

        var cycleIds = new List<string>();
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var id in feederMap.Keys)
        {
            if (visited.Contains(id)) continue;
            DetectCycleDfs(id, feederMap, visited, inStack, cycleIds);
        }

        return cycleIds;
    }

    /// <summary>
    /// Computes cumulative demand load for each node by summing the node's own
    /// demand plus all descendants. Panels use their <see cref="PanelSchedule.TotalDemandVA"/>
    /// when a matching schedule is provided.
    /// </summary>
    public void ComputeCumulativeDemand(
        List<DistributionNode> roots,
        IReadOnlyDictionary<string, double>? panelDemandVA = null)
    {
        foreach (var root in roots)
            ComputeDemandRecursive(root, panelDemandVA);
    }

    /// <summary>
    /// Propagates fault current from root nodes downstream through the tree.
    /// Transformers reduce fault current using available = (KVA × 1000) / (SecondaryV × √3 × Z%).
    /// </summary>
    public void PropagateFaultCurrent(List<DistributionNode> roots)
    {
        foreach (var root in roots)
        {
            double rootFault = root.Component is PowerSourceComponent ps
                ? ps.AvailableFaultCurrentKA
                : 0;
            root.FaultCurrentKA = rootFault;
            PropagateFaultRecursive(root);
        }
    }

    /// <summary>
    /// Produces a flat feeder schedule (suitable for table/report display)
    /// from the distribution tree via depth-first traversal.
    /// </summary>
    public List<FeederScheduleEntry> GenerateFeederSchedule(List<DistributionNode> roots)
    {
        var entries = new List<FeederScheduleEntry>();
        foreach (var root in roots)
            FlattenNode(root, 0, entries);
        return entries;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static bool IsDistributionNode(ElectricalComponent comp)
    {
        return comp.Type is ComponentType.Panel
            or ComponentType.Transformer
            or ComponentType.Bus
            or ComponentType.PowerSource
            or ComponentType.TransferSwitch;
    }

    private static string? GetFeederId(ElectricalComponent comp)
    {
        return comp switch
        {
            PanelComponent p => p.FeederId,
            TransformerComponent t => t.FeederId,
            BusComponent b => b.FeederId,
            TransferSwitchComponent ts => ts.NormalFeederId,
            _ => null
        };
    }

    private void DetectCycleDfs(
        string id,
        Dictionary<string, string?> feederMap,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> cycleIds)
    {
        visited.Add(id);
        inStack.Add(id);

        if (feederMap.TryGetValue(id, out var feederId) && feederId != null)
        {
            if (!visited.Contains(feederId) && feederMap.ContainsKey(feederId))
                DetectCycleDfs(feederId, feederMap, visited, inStack, cycleIds);
            else if (inStack.Contains(feederId))
                cycleIds.Add(id);
        }

        inStack.Remove(id);
    }

    private double ComputeDemandRecursive(
        DistributionNode node,
        IReadOnlyDictionary<string, double>? panelDemandVA)
    {
        double ownDemand = 0;
        if (node.NodeType == ComponentType.Panel && panelDemandVA != null)
            panelDemandVA.TryGetValue(node.Id, out ownDemand);

        double childTotal = 0;
        foreach (var child in node.Children)
            childTotal += ComputeDemandRecursive(child, panelDemandVA);

        node.CumulativeDemandVA = ownDemand + childTotal;
        return node.CumulativeDemandVA;
    }

    private void PropagateFaultRecursive(DistributionNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Component is TransformerComponent xfmr && xfmr.ImpedancePercent > 0)
            {
                // Secondary fault current = KVA * 1000 / (V_secondary * √3 * Z%)
                double secondaryFault = (xfmr.KVA * 1000)
                    / (xfmr.SecondaryVoltage * Math.Sqrt(3) * (xfmr.ImpedancePercent / 100));
                child.FaultCurrentKA = secondaryFault / 1000; // convert A to kA
            }
            else
            {
                child.FaultCurrentKA = node.FaultCurrentKA;
            }
            PropagateFaultRecursive(child);
        }
    }

    private void FlattenNode(DistributionNode node, int depth, List<FeederScheduleEntry> entries)
    {
        entries.Add(new FeederScheduleEntry
        {
            ComponentId = node.Id,
            Name = node.Name,
            NodeType = node.NodeType,
            PanelSubtype = node.PanelSubtype,
            Depth = depth,
            FeederId = GetFeederId(node.Component),
            CumulativeDemandVA = node.CumulativeDemandVA,
            FaultCurrentKA = node.FaultCurrentKA
        });

        foreach (var child in node.Children)
            FlattenNode(child, depth + 1, entries);
    }
}
