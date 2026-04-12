using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Specification for a feeder segment between distribution nodes (wire size, length, material).
/// </summary>
public record FeederSegment
{
    public string FromNodeId { get; init; } = "";
    public string ToNodeId { get; init; } = "";
    public string WireSize { get; init; } = "4/0";
    public ConductorMaterial Material { get; init; } = ConductorMaterial.Copper;
    public double LengthFeet { get; init; }
    public double Voltage { get; init; } = 480;
    public int Poles { get; init; } = 3;
    public double LoadAmps { get; init; }
}

/// <summary>
/// Result of cumulative feeder voltage drop analysis.
/// </summary>
public record FeederVoltageDropResult
{
    public string NodeId { get; init; } = "";
    public string NodeName { get; init; } = "";
    public double SegmentDropVolts { get; init; }
    public double SegmentDropPercent { get; init; }
    public double CumulativeDropVolts { get; init; }
    public double CumulativeDropPercent { get; init; }
    public double VoltageAtNode { get; init; }
    public bool ExceedsBranchLimit { get; init; }
    public bool ExceedsTotalLimit { get; init; }
}

/// <summary>
/// Cumulative feeder voltage drop analysis per NEC 210.19(A) Informational Note No. 4.
/// Walks the distribution hierarchy to compute voltage drop at each level,
/// ensuring the total (feeder + branch) does not exceed the NEC-recommended 5%.
/// Individual feeder segments should not exceed 3%.
/// </summary>
public static class FeederVoltageDropService
{
    /// <summary>NEC recommends max 3% for any single feeder or branch segment.</summary>
    public const double MaxSegmentDropPercent = 3.0;

    /// <summary>NEC recommends max 5% total from source to farthest load.</summary>
    public const double MaxTotalDropPercent = 5.0;

    /// <summary>
    /// Calculates voltage drop for a single feeder segment.
    /// Uses the same formula as ElectricalCalculationService but for feeder runs.
    /// Vd = multiplier × I × R × L / 1000  
    /// multiplier = 2 (single-phase) or √3 (three-phase)
    /// </summary>
    public static double CalculateSegmentDrop(FeederSegment segment)
    {
        if (segment.Voltage <= 0 || segment.LengthFeet <= 0 || segment.LoadAmps <= 0)
            return 0;

        double resistance = ElectricalCalculationService.GetResistancePer1000Ft(segment.WireSize, segment.Material);
        if (resistance <= 0) return 0;

        double multiplier = segment.Poles >= 3 ? Math.Sqrt(3) : 2.0;
        return multiplier * segment.LoadAmps * resistance * segment.LengthFeet / 1000.0;
    }

    /// <summary>
    /// Calculates the percentage voltage drop for a feeder segment.
    /// </summary>
    public static double CalculateSegmentDropPercent(FeederSegment segment)
    {
        if (segment.Voltage <= 0) return 0;
        double drop = CalculateSegmentDrop(segment);
        return (drop / segment.Voltage) * 100.0;
    }

    /// <summary>
    /// Walks the distribution tree computing cumulative voltage drop from root to each node.
    /// Requires a feeder segment lookup — maps (parentId, childId) → FeederSegment.
    /// Returns results for every node in the tree.
    /// </summary>
    public static List<FeederVoltageDropResult> AnalyzeTree(
        List<DistributionNode> roots,
        Dictionary<string, FeederSegment> segmentsByToNode,
        double sourceVoltage = 480)
    {
        var results = new List<FeederVoltageDropResult>();
        foreach (var root in roots)
        {
            WalkNode(root, 0, 0, sourceVoltage, segmentsByToNode, results);
        }
        return results;
    }

    /// <summary>
    /// Finds all nodes in the distribution tree where cumulative VD exceeds the 5% NEC recommendation.
    /// </summary>
    public static List<FeederVoltageDropResult> GetViolations(
        List<DistributionNode> roots,
        Dictionary<string, FeederSegment> segmentsByToNode,
        double sourceVoltage = 480)
    {
        return AnalyzeTree(roots, segmentsByToNode, sourceVoltage)
            .Where(r => r.ExceedsTotalLimit || r.ExceedsBranchLimit)
            .ToList();
    }

    /// <summary>
    /// Recommends minimum wire size for a feeder segment to stay within a target VD%.
    /// Iterates standard sizes to find the smallest that keeps segment drop ≤ targetPercent.
    /// </summary>
    public static string? RecommendFeederWireSize(
        double loadAmps,
        double lengthFeet,
        double voltage,
        int poles,
        ConductorMaterial material,
        double targetDropPercent = 3.0)
    {
        foreach (var size in NecAmpacityService.StandardSizes)
        {
            var testSegment = new FeederSegment
            {
                WireSize = size,
                Material = material,
                LengthFeet = lengthFeet,
                Voltage = voltage,
                Poles = poles,
                LoadAmps = loadAmps,
            };
            double pct = CalculateSegmentDropPercent(testSegment);
            if (pct <= targetDropPercent && pct > 0)
                return size;
        }
        return null;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private static void WalkNode(
        DistributionNode node,
        double cumulativeDropVolts,
        double cumulativeDropPercent,
        double sourceVoltage,
        Dictionary<string, FeederSegment> segmentsByToNode,
        List<FeederVoltageDropResult> results)
    {
        double segDropVolts = 0;
        double segDropPercent = 0;

        if (segmentsByToNode.TryGetValue(node.Id, out var segment))
        {
            segDropVolts = CalculateSegmentDrop(segment);
            segDropPercent = segment.Voltage > 0 ? (segDropVolts / segment.Voltage) * 100.0 : 0;
        }

        double cumDropVolts = cumulativeDropVolts + segDropVolts;
        double cumDropPercent = sourceVoltage > 0 ? (cumDropVolts / sourceVoltage) * 100.0 : 0;

        results.Add(new FeederVoltageDropResult
        {
            NodeId = node.Id,
            NodeName = node.Name,
            SegmentDropVolts = Math.Round(segDropVolts, 2),
            SegmentDropPercent = Math.Round(segDropPercent, 2),
            CumulativeDropVolts = Math.Round(cumDropVolts, 2),
            CumulativeDropPercent = Math.Round(cumDropPercent, 2),
            VoltageAtNode = Math.Round(sourceVoltage - cumDropVolts, 2),
            ExceedsBranchLimit = segDropPercent > MaxSegmentDropPercent,
            ExceedsTotalLimit = cumDropPercent > MaxTotalDropPercent,
        });

        foreach (var child in node.Children)
        {
            WalkNode(child, cumDropVolts, cumDropPercent, sourceVoltage, segmentsByToNode, results);
        }
    }
}
