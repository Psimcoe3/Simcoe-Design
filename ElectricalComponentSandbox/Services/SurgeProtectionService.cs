using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Surge protective device (SPD) selection and sizing per NEC 242 (2023)
/// and UL 1449 / IEEE C62.41.
/// </summary>
public static class SurgeProtectionService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    /// <summary>UL 1449 Type (location in electrical system).</summary>
    public enum SpdType
    {
        /// <summary>Type 1: Service entrance (ahead of main OCPD). Per NEC 230.67.</summary>
        Type1,
        /// <summary>Type 2: Distribution panel (load side of service OCPD). Per NEC 242.24.</summary>
        Type2,
        /// <summary>Type 3: Branch panel / point of use (min 10m from panel).</summary>
        Type3,
        /// <summary>Type 4: Component-level SPD (not UL 1449).</summary>
        Type4,
    }

    /// <summary>Exposure level per IEEE C62.41.</summary>
    public enum ExposureLevel
    {
        Low,      // Interior, well-protected
        Medium,   // Commercial / light industrial
        High,     // Industrial, outdoor, lightning-prone
    }

    /// <summary>SPD specification result.</summary>
    public record SpdSpecification
    {
        public SpdType Type { get; init; }
        public double SystemVoltage { get; init; }
        public double McovVolts { get; init; }
        public double MinSurgeRatingKA { get; init; }
        public double RecommendedSurgeKA { get; init; }
        public double VprVolts { get; init; }
        public string Modes { get; init; } = "";
        public bool NecRequired { get; init; }
        public string Notes { get; init; } = "";
    }

    /// <summary>System-level SPD coordination result.</summary>
    public record SpdCoordinationResult
    {
        public List<SpdSpecification> Devices { get; init; } = new();
        public bool HasServiceEntrance { get; init; }
        public bool HasDistribution { get; init; }
        public bool HasBranch { get; init; }
        public bool MeetsNec242 { get; init; }
        public List<string> Recommendations { get; init; } = new();
    }

    // ── MCOV (Maximum Continuous Operating Voltage) ──────────────────────────

    /// <summary>
    /// Returns minimum MCOV per UL 1449 for the given system voltage.
    /// MCOV must be ≥ 115% of nominal line-to-ground voltage.
    /// </summary>
    public static double GetMinimumMcov(double systemVoltageLL)
    {
        double vLG = systemVoltageLL / Math.Sqrt(3);
        return Math.Ceiling(vLG * 1.15);
    }

    /// <summary>
    /// Returns the minimum MCOV for single-phase systems (L-N = V/2 for split-phase).
    /// </summary>
    public static double GetMinimumMcovSinglePhase(double systemVoltage)
    {
        double vLN = systemVoltage / 2.0;
        return Math.Ceiling(vLN * 1.15);
    }

    // ── Surge Rating ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns minimum and recommended surge current rating (kA) per
    /// IEEE C62.41 exposure level and SPD type.
    /// </summary>
    public static (double MinKA, double RecommendedKA) GetSurgeRating(
        SpdType type, ExposureLevel exposure)
    {
        return (type, exposure) switch
        {
            (SpdType.Type1, ExposureLevel.High)   => (200, 300),
            (SpdType.Type1, ExposureLevel.Medium)  => (100, 200),
            (SpdType.Type1, ExposureLevel.Low)     => (50, 100),
            (SpdType.Type2, ExposureLevel.High)    => (100, 200),
            (SpdType.Type2, ExposureLevel.Medium)  => (50, 100),
            (SpdType.Type2, ExposureLevel.Low)     => (25, 50),
            (SpdType.Type3, ExposureLevel.High)    => (20, 50),
            (SpdType.Type3, ExposureLevel.Medium)  => (10, 20),
            (SpdType.Type3, ExposureLevel.Low)     => (5, 10),
            (SpdType.Type4, _)                     => (1, 5),
            _                                      => (10, 20),
        };
    }

    // ── VPR (Voltage Protection Rating) ──────────────────────────────────────

    /// <summary>
    /// Returns typical VPR (clamping voltage) for given system voltage.
    /// Lower VPR = better protection. UL 1449 rates at standard test waveforms.
    /// </summary>
    public static double GetTypicalVpr(double systemVoltageLL, SpdType type)
    {
        double vLG = systemVoltageLL / Math.Sqrt(3);
        double multiplier = type switch
        {
            SpdType.Type1 => 2.5,
            SpdType.Type2 => 2.0,
            SpdType.Type3 => 1.5,
            _ => 2.0,
        };
        return Math.Round(vLG * multiplier, 0);
    }

    // ── Protection Modes ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns required protection modes for the given system configuration.
    /// </summary>
    public static string GetProtectionModes(bool threePhase, bool hasNeutral)
    {
        if (threePhase && hasNeutral)
            return "L-N, L-G, L-L, N-G";
        if (threePhase)
            return "L-L, L-G";
        if (hasNeutral)
            return "L-N, L-G, N-G";
        return "L-L, L-G";
    }

    // ── Full SPD Specification ───────────────────────────────────────────────

    /// <summary>
    /// Generates a complete SPD specification for a given installation point.
    /// </summary>
    public static SpdSpecification SpecifyDevice(
        SpdType type, double systemVoltageLL,
        bool threePhase = true, bool hasNeutral = true,
        ExposureLevel exposure = ExposureLevel.Medium)
    {
        double mcov = threePhase
            ? GetMinimumMcov(systemVoltageLL)
            : GetMinimumMcovSinglePhase(systemVoltageLL);

        var (minKA, recKA) = GetSurgeRating(type, exposure);
        double vpr = GetTypicalVpr(systemVoltageLL, type);
        string modes = GetProtectionModes(threePhase, hasNeutral);

        // NEC 230.67 (2023): Type 1 or Type 2 SPD required at service
        // NEC 210.8(F)/242.24: Type 2 at dwelling units and per AHJ
        bool necRequired = type == SpdType.Type1 || type == SpdType.Type2;

        return new SpdSpecification
        {
            Type = type,
            SystemVoltage = systemVoltageLL,
            McovVolts = mcov,
            MinSurgeRatingKA = minKA,
            RecommendedSurgeKA = recKA,
            VprVolts = vpr,
            Modes = modes,
            NecRequired = necRequired,
            Notes = type == SpdType.Type1
                ? "Install ahead of or integral with service disconnect per NEC 230.67."
                : type == SpdType.Type2
                    ? "Install on load side of service OCPD per NEC 242.24."
                    : "Install at least 10m (30ft) from panel per UL 1449.",
        };
    }

    // ── System Coordination ──────────────────────────────────────────────────

    /// <summary>
    /// Evaluates SPD coordination across multiple levels.
    /// Good practice: cascaded Type 1 → Type 2 → Type 3 for let-through reduction.
    /// </summary>
    public static SpdCoordinationResult EvaluateCoordination(
        IEnumerable<SpdSpecification> devices)
    {
        var list = devices.ToList();
        var recommendations = new List<string>();

        bool hasT1 = list.Any(d => d.Type == SpdType.Type1);
        bool hasT2 = list.Any(d => d.Type == SpdType.Type2);
        bool hasT3 = list.Any(d => d.Type == SpdType.Type3);

        if (!hasT1 && !hasT2)
            recommendations.Add("NEC 230.67/242: At minimum, install Type 1 or Type 2 SPD at service entrance.");

        if (hasT1 && !hasT2)
            recommendations.Add("Add Type 2 SPD at distribution panels for cascaded protection.");

        if (list.Count > 1)
        {
            var vprByType = list.GroupBy(d => d.Type)
                .ToDictionary(g => g.Key, g => g.Min(d => d.VprVolts));

            if (vprByType.ContainsKey(SpdType.Type1) && vprByType.ContainsKey(SpdType.Type2))
            {
                if (vprByType[SpdType.Type2] >= vprByType[SpdType.Type1])
                    recommendations.Add("Type 2 VPR should be lower than Type 1 for effective cascading.");
            }
        }

        return new SpdCoordinationResult
        {
            Devices = list,
            HasServiceEntrance = hasT1,
            HasDistribution = hasT2,
            HasBranch = hasT3,
            MeetsNec242 = hasT1 || hasT2,
            Recommendations = recommendations,
        };
    }
}
