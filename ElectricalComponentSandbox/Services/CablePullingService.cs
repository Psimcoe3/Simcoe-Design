using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Cable pulling tension, sidewall bearing pressure, jam ratio, and clearance calculations
/// per IEEE 1185 and NEC Chapter 3.
/// </summary>
public static class CablePullingService
{
    public enum CableType
    {
        SingleConductor,
        TriplexAssembly,
        MulticonductorJacketed,
    }

    public enum ConduitMaterial
    {
        PVC,
        SteelIMC,
        SteelRigid,
        Aluminum,
        HDPE,
    }

    public record CableSpec
    {
        public double OuterDiameterInches { get; init; }
        public double WeightPerFootLbs { get; init; }
        public double MaxTensionLbs { get; init; }
        public CableType Type { get; init; }
        public int ConductorCount { get; init; } = 1;
    }

    public record ConduitSpec
    {
        public double InnerDiameterInches { get; init; }
        public ConduitMaterial Material { get; init; }
    }

    public record PullSection
    {
        public double LengthFeet { get; init; }
        public double BendAngleDegrees { get; init; }
    }

    public record JamRatioResult
    {
        public double Ratio { get; init; }
        public bool IsJamRisk { get; init; }
        public string Warning { get; init; } = string.Empty;
    }

    public record ClearanceResult
    {
        public double FillPercent { get; init; }
        public double ClearanceInches { get; init; }
        public bool MeetsNecFill { get; init; }
    }

    public record TensionResult
    {
        public double TotalTensionLbs { get; init; }
        public double MaxAllowableTensionLbs { get; init; }
        public double SidewallBearingPressureLbsPerFt { get; init; }
        public double MaxSidewallPressureLbsPerFt { get; init; }
        public bool TensionAcceptable { get; init; }
        public bool SidewallAcceptable { get; init; }
        public string GoverningConstraint { get; init; } = string.Empty;
    }

    public record PullPlanResult
    {
        public TensionResult Tension { get; init; } = new();
        public JamRatioResult JamRatio { get; init; } = new();
        public ClearanceResult Clearance { get; init; } = new();
        public bool OverallAcceptable { get; init; }
        public List<string> Warnings { get; init; } = new();
    }

    /// <summary>
    /// Coefficient of friction for cable-in-conduit pulling.
    /// PVC and HDPE are lower friction; steel is higher.
    /// </summary>
    public static double GetFrictionCoefficient(ConduitMaterial material)
    {
        return material switch
        {
            ConduitMaterial.PVC => 0.35,
            ConduitMaterial.HDPE => 0.35,
            ConduitMaterial.Aluminum => 0.40,
            ConduitMaterial.SteelIMC => 0.50,
            ConduitMaterial.SteelRigid => 0.50,
            _ => 0.50,
        };
    }

    /// <summary>
    /// Maximum sidewall bearing pressure (lbs/ft of bend radius) by cable type.
    /// IEEE 1185 typical limits.
    /// </summary>
    public static double GetMaxSidewallPressure(CableType cableType)
    {
        return cableType switch
        {
            CableType.SingleConductor => 600.0,
            CableType.TriplexAssembly => 1000.0,
            CableType.MulticonductorJacketed => 1500.0,
            _ => 600.0,
        };
    }

    /// <summary>
    /// Jam ratio = conduit ID / cable OD for single cables, or conduit ID / (cable OD) for
    /// three cables in a conduit (triangular config). Risk zone is 2.8–3.2 for three cables.
    /// </summary>
    public static JamRatioResult CalculateJamRatio(ConduitSpec conduit, CableSpec cable, int cableCount)
    {
        if (conduit.InnerDiameterInches <= 0 || cable.OuterDiameterInches <= 0)
            return new JamRatioResult { Ratio = 0, IsJamRisk = false, Warning = "Invalid dimensions" };

        double ratio = conduit.InnerDiameterInches / cable.OuterDiameterInches;

        if (cableCount == 3)
        {
            // For three equal cables in triangular config, jam zone is D/d = 2.8 to 3.2
            bool isJamRisk = ratio >= 2.8 && ratio <= 3.2;
            string warning = isJamRisk
                ? $"Jam ratio {ratio:F2} is in the 2.8-3.2 danger zone for three cables"
                : string.Empty;
            return new JamRatioResult { Ratio = ratio, IsJamRisk = isJamRisk, Warning = warning };
        }

        // For single cable, jam is not a concern (ratio > 1 means it fits)
        return new JamRatioResult
        {
            Ratio = ratio,
            IsJamRisk = false,
            Warning = ratio < 1.05 ? "Very tight fit — cable may bind" : string.Empty,
        };
    }

    /// <summary>
    /// Conduit fill and clearance check per NEC 344/352/358.
    /// Single cable: 53% fill; two cables: 31%; three or more: 40%.
    /// </summary>
    public static ClearanceResult CalculateClearance(ConduitSpec conduit, CableSpec cable, int cableCount)
    {
        if (conduit.InnerDiameterInches <= 0 || cable.OuterDiameterInches <= 0 || cableCount <= 0)
            return new ClearanceResult { FillPercent = 0, ClearanceInches = 0, MeetsNecFill = false };

        double conduitArea = Math.PI * Math.Pow(conduit.InnerDiameterInches / 2.0, 2);
        double singleCableArea = Math.PI * Math.Pow(cable.OuterDiameterInches / 2.0, 2);
        double totalCableArea = singleCableArea * cableCount;
        double fillPercent = (totalCableArea / conduitArea) * 100.0;

        double maxFillPercent = cableCount switch
        {
            1 => 53.0,
            2 => 31.0,
            _ => 40.0,
        };

        double clearance = (conduit.InnerDiameterInches - cable.OuterDiameterInches) / 2.0;

        return new ClearanceResult
        {
            FillPercent = Math.Round(fillPercent, 1),
            ClearanceInches = Math.Round(clearance, 3),
            MeetsNecFill = fillPercent <= maxFillPercent,
        };
    }

    /// <summary>
    /// Pulling tension through a straight conduit section.
    /// T_out = T_in + μ × W × L, where W = total cable weight per foot.
    /// </summary>
    public static double CalculateStraightTension(double tensionIn, double frictionCoefficient,
        double cableWeightPerFootLbs, int cableCount, double lengthFeet)
    {
        double totalWeight = cableWeightPerFootLbs * cableCount;
        return tensionIn + frictionCoefficient * totalWeight * lengthFeet;
    }

    /// <summary>
    /// Tension multiplier through a bend: T_out = T_in × e^(μ × θ),
    /// where θ is in radians. IEEE 1185 capstan equation.
    /// </summary>
    public static double CalculateBendTension(double tensionIn, double frictionCoefficient,
        double bendAngleDegrees)
    {
        if (bendAngleDegrees <= 0)
            return tensionIn;

        double radians = bendAngleDegrees * Math.PI / 180.0;
        return tensionIn * Math.Exp(frictionCoefficient * radians);
    }

    /// <summary>
    /// Sidewall bearing pressure at a bend: SWBP = T / R,
    /// where T = tension at bend and R = bend radius in feet.
    /// Standard conduit bend radius = 8× conduit trade size (approximated as ID).
    /// </summary>
    public static double CalculateSidewallPressure(double tensionAtBendLbs, double bendRadiusFeet)
    {
        if (bendRadiusFeet <= 0)
            return 0;

        return tensionAtBendLbs / bendRadiusFeet;
    }

    /// <summary>
    /// Full pull tension calculation through a series of straight and bend sections.
    /// Returns accumulated tension, governing constraint, and sidewall check at worst bend.
    /// </summary>
    public static TensionResult CalculatePullTension(ConduitSpec conduit, CableSpec cable,
        int cableCount, IEnumerable<PullSection> sections)
    {
        if (cable == null || conduit == null || sections == null)
            return new TensionResult();

        var sectionList = sections.ToList();
        if (sectionList.Count == 0)
            return new TensionResult();

        double mu = GetFrictionCoefficient(conduit.Material);
        double maxSwbp = GetMaxSidewallPressure(cable.Type);
        double maxTension = cable.MaxTensionLbs * cableCount;

        // Approximate bend radius: 8 × conduit ID converted to feet
        double bendRadiusFeet = (8.0 * conduit.InnerDiameterInches) / 12.0;

        double tension = 0;
        double worstSwbp = 0;

        foreach (var section in sectionList)
        {
            if (section.LengthFeet > 0)
            {
                tension = CalculateStraightTension(tension, mu, cable.WeightPerFootLbs,
                    cableCount, section.LengthFeet);
            }

            if (section.BendAngleDegrees > 0)
            {
                tension = CalculateBendTension(tension, mu, section.BendAngleDegrees);
                double swbp = CalculateSidewallPressure(tension, bendRadiusFeet);
                if (swbp > worstSwbp) worstSwbp = swbp;
            }
        }

        bool tensionOk = tension <= maxTension;
        bool swbpOk = worstSwbp <= maxSwbp;

        string governing;
        if (!tensionOk && !swbpOk)
            governing = "Both tension and sidewall pressure exceeded";
        else if (!tensionOk)
            governing = "Maximum pulling tension exceeded";
        else if (!swbpOk)
            governing = "Sidewall bearing pressure exceeded";
        else
            governing = "Within limits";

        return new TensionResult
        {
            TotalTensionLbs = Math.Round(tension, 1),
            MaxAllowableTensionLbs = maxTension,
            SidewallBearingPressureLbsPerFt = Math.Round(worstSwbp, 1),
            MaxSidewallPressureLbsPerFt = maxSwbp,
            TensionAcceptable = tensionOk,
            SidewallAcceptable = swbpOk,
            GoverningConstraint = governing,
        };
    }

    /// <summary>
    /// Complete pull plan analysis: tension, jam ratio, clearance, and warnings.
    /// </summary>
    public static PullPlanResult AnalyzePull(ConduitSpec conduit, CableSpec cable,
        int cableCount, IEnumerable<PullSection> sections)
    {
        var tension = CalculatePullTension(conduit, cable, cableCount, sections);
        var jam = CalculateJamRatio(conduit, cable, cableCount);
        var clearance = CalculateClearance(conduit, cable, cableCount);

        var warnings = new List<string>();

        if (!tension.TensionAcceptable)
            warnings.Add($"Pull tension {tension.TotalTensionLbs:F0} lbs exceeds max {tension.MaxAllowableTensionLbs:F0} lbs");
        if (!tension.SidewallAcceptable)
            warnings.Add($"Sidewall pressure {tension.SidewallBearingPressureLbsPerFt:F0} lbs/ft exceeds max {tension.MaxSidewallPressureLbsPerFt:F0} lbs/ft");
        if (jam.IsJamRisk)
            warnings.Add(jam.Warning);
        if (!clearance.MeetsNecFill)
            warnings.Add($"Conduit fill {clearance.FillPercent:F1}% exceeds NEC limit");

        bool overall = tension.TensionAcceptable && tension.SidewallAcceptable
            && !jam.IsJamRisk && clearance.MeetsNecFill;

        return new PullPlanResult
        {
            Tension = tension,
            JamRatio = jam,
            Clearance = clearance,
            OverallAcceptable = overall,
            Warnings = warnings,
        };
    }
}
