using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Neutral conductor sizing per NEC 220.61 and 310.15.
/// Handles standard neutral reduction, harmonic-loaded neutrals
/// (200% neutral for nonlinear loads), and shared neutral sizing.
/// </summary>
public static class NeutralConductorSizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum LoadType
    {
        Linear,             // Motors, heaters, incandescent lighting
        NonlinearLight,     // < 50% nonlinear (some electronic ballasts)
        NonlinearHeavy,     // ≥ 50% nonlinear (VFDs, computers, LEDs)
    }

    public enum SystemType
    {
        SinglePhase2Wire,
        SinglePhase3Wire,
        ThreePhase4Wire,
    }

    /// <summary>Neutral sizing result.</summary>
    public record NeutralSizingResult
    {
        public double PhaseCurrentAmps { get; init; }
        public double NeutralCurrentAmps { get; init; }
        public double NeutralToPhaseRatio { get; init; }
        public string RecommendedNeutralSize { get; init; } = "";
        public string PhaseSize { get; init; } = "";
        public bool IsOversizedNeutral { get; init; }
        public string Justification { get; init; } = "";
    }

    /// <summary>NEC 220.61 neutral load calculation result.</summary>
    public record NeutralLoadResult
    {
        public double First200ALoad { get; init; }
        public double Over200ALoad { get; init; }
        public double Over200AReduced { get; init; }
        public double TotalNeutralLoadAmps { get; init; }
    }

    // ── Standard Wire Sizes ──────────────────────────────────────────────────

    private static readonly (string Size, double Ampacity75C)[] _wireSizes = new[]
    {
        ("14", 20.0),   ("12", 25.0),   ("10", 35.0),   ("8", 50.0),
        ("6", 65.0),    ("4", 85.0),    ("3", 100.0),   ("2", 115.0),
        ("1", 130.0),   ("1/0", 150.0), ("2/0", 175.0), ("3/0", 200.0),
        ("4/0", 230.0), ("250", 255.0), ("300", 285.0), ("350", 310.0),
        ("400", 335.0), ("500", 380.0), ("600", 420.0), ("750", 475.0),
        ("1000", 545.0),
    };

    /// <summary>Returns the minimum wire size for a given ampacity.</summary>
    public static string GetMinWireSize(double requiredAmps)
    {
        foreach (var (size, ampacity) in _wireSizes)
        {
            if (ampacity >= requiredAmps)
                return size;
        }
        return "1000+"; // Exceeds table — parallel conductors needed
    }

    // ── NEC 220.61 Neutral Load Calculation ──────────────────────────────────

    /// <summary>
    /// Calculates neutral load per NEC 220.61:
    /// - First 200A at 100%
    /// - Over 200A at 70% (for feeders/services only)
    /// Does NOT apply 70% reduction if loads are nonlinear or electric discharge lighting.
    /// </summary>
    public static NeutralLoadResult CalculateNeutralLoad(
        double maxUnbalancedAmps, bool allowReduction = true)
    {
        double first200 = Math.Min(maxUnbalancedAmps, 200);
        double over200 = Math.Max(0, maxUnbalancedAmps - 200);
        double over200Reduced = allowReduction ? over200 * 0.70 : over200;
        double total = first200 + over200Reduced;

        return new NeutralLoadResult
        {
            First200ALoad = Math.Round(first200, 2),
            Over200ALoad = Math.Round(over200, 2),
            Over200AReduced = Math.Round(over200Reduced, 2),
            TotalNeutralLoadAmps = Math.Round(total, 2),
        };
    }

    // ── Harmonic Neutral Current ─────────────────────────────────────────────

    /// <summary>
    /// Estimates neutral current for 3-phase 4-wire systems with nonlinear loads.
    /// In balanced 3-phase linear systems, neutral ≈ 0.
    /// With triplen harmonics (3rd, 9th, 15th), neutral current can reach √3 × phase current.
    /// </summary>
    /// <param name="phaseCurrentAmps">Per-phase RMS current.</param>
    /// <param name="thirdHarmonicPercent">Third harmonic as % of fundamental (0–100).</param>
    public static double EstimateHarmonicNeutralCurrent(
        double phaseCurrentAmps, double thirdHarmonicPercent)
    {
        // Neutral = 3 × I_phase × (3rd harmonic fraction)
        // because triplen harmonics add in phase on the neutral
        double h3Fraction = thirdHarmonicPercent / 100.0;
        return Math.Round(3 * phaseCurrentAmps * h3Fraction, 2);
    }

    // ── Neutral Sizing ───────────────────────────────────────────────────────

    /// <summary>
    /// Sizes the neutral conductor based on system type, load type, and phase current.
    /// </summary>
    /// <param name="phaseCurrentAmps">Phase conductor current.</param>
    /// <param name="systemType">System configuration.</param>
    /// <param name="loadType">Type of loads on the circuit.</param>
    /// <param name="thirdHarmonicPercent">Third harmonic content (for NonlinearHeavy).</param>
    public static NeutralSizingResult SizeNeutral(
        double phaseCurrentAmps, SystemType systemType,
        LoadType loadType = LoadType.Linear,
        double thirdHarmonicPercent = 0)
    {
        string phaseSize = GetMinWireSize(phaseCurrentAmps);
        double neutralAmps;
        bool oversized = false;
        string justification;

        switch (systemType)
        {
            case SystemType.SinglePhase2Wire:
                // Neutral carries full phase current
                neutralAmps = phaseCurrentAmps;
                justification = "Single-phase 2-wire: neutral = phase current";
                break;

            case SystemType.SinglePhase3Wire:
                // Neutral carries unbalanced current only
                // Worst case = full phase current if unbalanced
                neutralAmps = phaseCurrentAmps;
                justification = "Single-phase 3-wire: neutral sized for full unbalance";
                break;

            case SystemType.ThreePhase4Wire:
                switch (loadType)
                {
                    case LoadType.Linear:
                        // Balanced linear: neutral ≈ 0, size for unbalance
                        neutralAmps = phaseCurrentAmps * 0.7; // NEC 220.61 reduction
                        justification = "3Φ 4W linear: 70% neutral per NEC 220.61";
                        break;

                    case LoadType.NonlinearLight:
                        // Moderate nonlinear: neutral = phase
                        neutralAmps = phaseCurrentAmps;
                        justification = "3Φ 4W moderate nonlinear: neutral = phase (no reduction)";
                        break;

                    case LoadType.NonlinearHeavy:
                        // Heavy nonlinear: triplen harmonics on neutral
                        double harmonicNeutral = EstimateHarmonicNeutralCurrent(
                            phaseCurrentAmps, thirdHarmonicPercent > 0 ? thirdHarmonicPercent : 33);
                        neutralAmps = Math.Max(phaseCurrentAmps, harmonicNeutral);
                        oversized = neutralAmps > phaseCurrentAmps;
                        justification = oversized
                            ? $"3Φ 4W heavy nonlinear: 200% neutral ({neutralAmps:F0}A) due to triplen harmonics"
                            : "3Φ 4W heavy nonlinear: neutral ≥ phase due to harmonics";
                        break;

                    default:
                        neutralAmps = phaseCurrentAmps;
                        justification = "Default: neutral = phase";
                        break;
                }
                break;

            default:
                neutralAmps = phaseCurrentAmps;
                justification = "Default: neutral = phase";
                break;
        }

        string neutralSize = GetMinWireSize(neutralAmps);
        double ratio = phaseCurrentAmps > 0 ? neutralAmps / phaseCurrentAmps : 0;

        return new NeutralSizingResult
        {
            PhaseCurrentAmps = Math.Round(phaseCurrentAmps, 2),
            NeutralCurrentAmps = Math.Round(neutralAmps, 2),
            NeutralToPhaseRatio = Math.Round(ratio, 4),
            RecommendedNeutralSize = neutralSize,
            PhaseSize = phaseSize,
            IsOversizedNeutral = oversized,
            Justification = justification,
        };
    }

    // ── Shared Neutral (Multi-wire Branch Circuit) ───────────────────────────

    /// <summary>
    /// Evaluates shared neutral sizing for multi-wire branch circuits per NEC 210.4.
    /// For 3-phase MWBC, neutral carries only unbalanced current for linear loads.
    /// </summary>
    /// <param name="phaseCurrentsAmps">Current on each phase (2 for 1Φ 3W, 3 for 3Φ).</param>
    /// <param name="loadType">Type of connected loads.</param>
    public static NeutralSizingResult SizeSharedNeutral(
        IReadOnlyList<double> phaseCurrentsAmps, LoadType loadType = LoadType.Linear)
    {
        double maxPhase = phaseCurrentsAmps.Max();
        double minPhase = phaseCurrentsAmps.Min();
        string phaseSize = GetMinWireSize(maxPhase);

        double neutralAmps;
        bool oversized = false;
        string justification;

        if (loadType == LoadType.NonlinearHeavy)
        {
            // With heavy triplen harmonics, size neutral = max phase
            neutralAmps = maxPhase;
            justification = "Shared neutral with nonlinear loads: neutral ≥ max phase";
        }
        else if (phaseCurrentsAmps.Count == 2)
        {
            // 1Φ 3W MWBC: neutral carries full current if unbalanced
            neutralAmps = maxPhase;
            justification = "1Φ 3W MWBC: neutral sized for max phase current";
        }
        else
        {
            // 3Φ MWBC balanced: neutral carries unbalance
            neutralAmps = maxPhase - minPhase;
            neutralAmps = Math.Max(neutralAmps, maxPhase * 0.5); // Min 50% for safety
            justification = $"3Φ MWBC: neutral for unbalance ({maxPhase:F0}A – {minPhase:F0}A)";
        }

        string neutralSize = GetMinWireSize(neutralAmps);
        double ratio = maxPhase > 0 ? neutralAmps / maxPhase : 0;

        return new NeutralSizingResult
        {
            PhaseCurrentAmps = Math.Round(maxPhase, 2),
            NeutralCurrentAmps = Math.Round(neutralAmps, 2),
            NeutralToPhaseRatio = Math.Round(ratio, 4),
            RecommendedNeutralSize = neutralSize,
            PhaseSize = phaseSize,
            IsOversizedNeutral = oversized,
            Justification = justification,
        };
    }
}
