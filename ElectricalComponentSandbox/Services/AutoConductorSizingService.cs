using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Automatic conductor sizing for the entire distribution hierarchy.
/// Walks the distribution tree and recommends feeder and branch conductor sizes
/// considering all applicable NEC constraints simultaneously:
///
/// - NEC 310.16 ampacity (base + temperature correction + bundle derating)
/// - NEC 215.2(A)(4) / 210.19(A)(4) voltage drop (≤3% per segment, ≤5% cumulative)
/// - NEC 250.122 equipment grounding conductor
/// - NEC 220 demand factors applied to load
/// - NEC 430.22/430.24 motor load 125% factor
///
/// Returns a sized feeder schedule that can be applied to the project.
/// </summary>
public static class AutoConductorSizingService
{
    /// <summary>
    /// Result for a single feeder or branch conductor recommendation.
    /// </summary>
    public record ConductorSizingResult
    {
        public string FromNodeId { get; init; } = "";
        public string FromNodeName { get; init; } = "";
        public string ToNodeId { get; init; } = "";
        public string ToNodeName { get; init; } = "";
        public double LoadAmps { get; init; }
        public string RecommendedPhaseSize { get; init; } = "";
        public string RecommendedGroundSize { get; init; } = "";
        public string MinSizeForAmpacity { get; init; } = "";
        public string MinSizeForVoltageDrop { get; init; } = "";
        public double AmpacityOfRecommended { get; init; }
        public double SegmentVoltageDropPercent { get; init; }
        public double CumulativeVoltageDropPercent { get; init; }
        public bool AmpacityGoverning { get; init; }
        public bool VoltageDropGoverning { get; init; }
        public int OCPDAmps { get; init; }
        public List<string> Warnings { get; init; } = new();
    }

    /// <summary>
    /// Overall sizing result for the entire distribution system.
    /// </summary>
    public record SystemSizingResult
    {
        public List<ConductorSizingResult> FeederResults { get; init; } = new();
        public int TotalFeedersAnalyzed { get; init; }
        public int FeedersWithWarnings { get; init; }
        public int VoltageDropViolations { get; init; }
        public int AmpacityViolations { get; init; }
        public double WorstCumulativeVoltageDropPercent { get; init; }
        public string? WorstVoltageDropNodeId { get; init; }
    }

    /// <summary>
    /// Parameters controlling how conductors are sized.
    /// </summary>
    public record SizingParameters
    {
        public ConductorMaterial Material { get; init; } = ConductorMaterial.Copper;
        public InsulationTemperatureRating TemperatureRating { get; init; } = InsulationTemperatureRating.C75;
        public double AmbientTempC { get; init; } = 30;
        public int ConductorsInRaceway { get; init; } = 3;
        public double MaxSegmentVoltageDropPercent { get; init; } = 3.0;
        public double MaxTotalVoltageDropPercent { get; init; } = 5.0;
        public double SourceVoltage { get; init; } = 480;
        public int DefaultPoles { get; init; } = 3;
    }

    /// <summary>
    /// Standard wire sizes in order from smallest to largest.
    /// </summary>
    private static readonly string[] WireSizes =
    {
        "14", "12", "10", "8", "6", "4", "3", "2", "1",
        "1/0", "2/0", "3/0", "4/0",
        "250", "300", "350", "400", "500", "600", "700", "750", "1000"
    };

    /// <summary>
    /// Standard OCPD sizes per NEC 240.6(A).
    /// </summary>
    private static readonly int[] StandardOCPDSizes =
    {
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500,
        600, 700, 800, 1000, 1200, 1600, 2000, 2500, 3000
    };

    /// <summary>
    /// Sizes all feeders in the distribution tree.
    /// </summary>
    public static SystemSizingResult SizeAllFeeders(
        List<DistributionNode> roots,
        Dictionary<string, double> feederLengthsFeet,
        SizingParameters? parameters = null)
    {
        var p = parameters ?? new SizingParameters();
        var results = new List<ConductorSizingResult>();
        double worstCumVD = 0;
        string? worstNodeId = null;

        foreach (var root in roots)
        {
            SizeNodeRecursive(root, null, 0, feederLengthsFeet, p, results, ref worstCumVD, ref worstNodeId);
        }

        return new SystemSizingResult
        {
            FeederResults = results,
            TotalFeedersAnalyzed = results.Count,
            FeedersWithWarnings = results.Count(r => r.Warnings.Count > 0),
            VoltageDropViolations = results.Count(r => r.SegmentVoltageDropPercent > p.MaxSegmentVoltageDropPercent),
            AmpacityViolations = results.Count(r => r.Warnings.Any(w => w.Contains("ampacity", StringComparison.OrdinalIgnoreCase))),
            WorstCumulativeVoltageDropPercent = Math.Round(worstCumVD, 2),
            WorstVoltageDropNodeId = worstNodeId,
        };
    }

    /// <summary>
    /// Sizes a single feeder segment between two nodes.
    /// </summary>
    public static ConductorSizingResult SizeFeeder(
        double loadAmps,
        double lengthFeet,
        double voltage,
        int poles,
        double upstreamCumulativeVDPercent = 0,
        SizingParameters? parameters = null,
        string fromNodeId = "",
        string fromNodeName = "",
        string toNodeId = "",
        string toNodeName = "")
    {
        var p = parameters ?? new SizingParameters();
        var warnings = new List<string>();

        // Get temperature and bundle derating factors
        double tempFactor = GetTemperatureCorrectionFactor(p.AmbientTempC, p.TemperatureRating);
        double bundleFactor = BundleDeratingService.GetAdjustmentFactor(p.ConductorsInRaceway);
        double combinedFactor = tempFactor * bundleFactor;

        // Find minimum wire size for ampacity (derated)
        string? ampacitySize = null;
        double ampacityOfSelected = 0;
        var ampacityTable = NecAmpacityService.GetDefaultTable(p.Material, p.TemperatureRating);

        foreach (var size in WireSizes)
        {
            int baseAmpacity = ampacityTable.Lookup(size);
            if (baseAmpacity <= 0) continue;
            double derated = baseAmpacity * combinedFactor;
            if (derated >= loadAmps)
            {
                ampacitySize = size;
                ampacityOfSelected = derated;
                break;
            }
        }

        // Find minimum wire size for voltage drop
        string? vdSize = null;
        double segmentVDPercent = 0;
        if (lengthFeet > 0 && voltage > 0)
        {
            foreach (var size in WireSizes)
            {
                double vd = CalculateVoltageDropPercent(size, p.Material, loadAmps, lengthFeet, voltage, poles);
                if (vd <= p.MaxSegmentVoltageDropPercent)
                {
                    vdSize = size;
                    segmentVDPercent = vd;
                    break;
                }
            }
        }

        // Take the larger of the two (VD not applicable when length is zero)
        int ampIdx = ampacitySize != null ? Array.IndexOf(WireSizes, ampacitySize) : WireSizes.Length - 1;
        bool vdApplicable = lengthFeet > 0 && voltage > 0;
        int vdIdx = vdApplicable
            ? (vdSize != null ? Array.IndexOf(WireSizes, vdSize) : WireSizes.Length - 1)
            : -1;
        int finalIdx = Math.Max(ampIdx, vdIdx);
        string recommendedSize = WireSizes[finalIdx];

        // Recalculate actual VD with final size
        if (lengthFeet > 0 && voltage > 0)
        {
            segmentVDPercent = CalculateVoltageDropPercent(recommendedSize, p.Material, loadAmps, lengthFeet, voltage, poles);
        }

        double cumulativeVD = upstreamCumulativeVDPercent + segmentVDPercent;

        // Recalculate ampacity of final selection
        int finalBaseAmpacity = ampacityTable.Lookup(recommendedSize);
        ampacityOfSelected = finalBaseAmpacity * combinedFactor;

        // OCPD sizing (next standard size ≥ load)
        int ocpdAmps = StandardOCPDSizes.FirstOrDefault(s => s >= loadAmps);
        if (ocpdAmps == 0) ocpdAmps = StandardOCPDSizes[^1];

        // EGC sizing per NEC 250.122
        string groundSize = GroundingService.GetMinEGCSize(ocpdAmps, p.Material);

        // Warnings
        if (ampacitySize == null)
            warnings.Add("No standard wire size meets ampacity requirement");
        if (vdSize == null && lengthFeet > 0)
            warnings.Add($"No standard wire size meets {p.MaxSegmentVoltageDropPercent}% voltage drop limit");
        if (cumulativeVD > p.MaxTotalVoltageDropPercent)
            warnings.Add($"Cumulative voltage drop {cumulativeVD:F1}% exceeds {p.MaxTotalVoltageDropPercent}% limit (NEC 215.2)");
        if (p.AmbientTempC > 30)
            warnings.Add($"Ambient temperature correction applied ({p.AmbientTempC}°C, factor {tempFactor:F2})");
        if (p.ConductorsInRaceway > 3)
            warnings.Add($"Bundle derating applied ({p.ConductorsInRaceway} conductors, factor {bundleFactor:F2})");

        return new ConductorSizingResult
        {
            FromNodeId = fromNodeId,
            FromNodeName = fromNodeName,
            ToNodeId = toNodeId,
            ToNodeName = toNodeName,
            LoadAmps = Math.Round(loadAmps, 1),
            RecommendedPhaseSize = recommendedSize,
            RecommendedGroundSize = groundSize,
            MinSizeForAmpacity = ampacitySize ?? WireSizes[^1],
            MinSizeForVoltageDrop = vdSize ?? "",
            AmpacityOfRecommended = Math.Round(ampacityOfSelected, 1),
            SegmentVoltageDropPercent = Math.Round(segmentVDPercent, 2),
            CumulativeVoltageDropPercent = Math.Round(cumulativeVD, 2),
            AmpacityGoverning = ampIdx >= vdIdx,
            VoltageDropGoverning = vdIdx > ampIdx,
            OCPDAmps = ocpdAmps,
            Warnings = warnings,
        };
    }

    private static void SizeNodeRecursive(
        DistributionNode node,
        DistributionNode? parent,
        double upstreamCumulativeVD,
        Dictionary<string, double> feederLengths,
        SizingParameters p,
        List<ConductorSizingResult> results,
        ref double worstCumVD,
        ref string? worstNodeId)
    {
        if (parent != null)
        {
            double loadVA = node.CumulativeDemandVA;
            double loadAmps = loadVA / (p.SourceVoltage * (p.DefaultPoles > 1 ? Math.Sqrt(3) : 1));

            // Check voltage for transformers
            double segmentVoltage = p.SourceVoltage;
            if (node.Component is TransformerComponent xfmr)
                segmentVoltage = xfmr.PrimaryVoltage;

            double length = feederLengths.TryGetValue(node.Id, out var len) ? len : 50;

            var result = SizeFeeder(
                loadAmps, length, segmentVoltage, p.DefaultPoles,
                upstreamCumulativeVD, p,
                parent.Id, parent.Name, node.Id, node.Name);

            results.Add(result);

            if (result.CumulativeVoltageDropPercent > worstCumVD)
            {
                worstCumVD = result.CumulativeVoltageDropPercent;
                worstNodeId = node.Id;
            }

            upstreamCumulativeVD = result.CumulativeVoltageDropPercent;
        }

        foreach (var child in node.Children)
        {
            SizeNodeRecursive(child, node, upstreamCumulativeVD, feederLengths, p, results, ref worstCumVD, ref worstNodeId);
        }
    }

    private static double CalculateVoltageDropPercent(
        string wireSize, ConductorMaterial material, double loadAmps,
        double lengthFeet, double voltage, int poles)
    {
        double resistance = ElectricalCalculationService.GetResistancePer1000Ft(wireSize, material);
        double multiplier = poles > 1 ? Math.Sqrt(3) : 2.0;
        double vdVolts = multiplier * loadAmps * resistance * lengthFeet / 1000.0;
        return voltage > 0 ? (vdVolts / voltage) * 100 : 0;
    }

    private static double GetTemperatureCorrectionFactor(double ambientTempC, InsulationTemperatureRating rating)
    {
        if (ambientTempC <= 30) return 1.0;

        // NEC 310.15(B)(1) simplified correction
        var correctionTable = NecAmpacityService.DefaultCorrectionFactors;
        return correctionTable.GetFactor(ambientTempC, rating);
    }
}
