using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Busway (bus duct) sizing and selection per NEC 368.
/// Covers plug-in and feeder busway sizing, voltage drop, and tap rules.
/// </summary>
public static class BuswaySizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum BuswayType
    {
        PlugIn,      // Plug-in busway with tap-off units
        Feeder,      // Feeder busway (point-to-point)
        Trolley,     // Trolley busway for moving loads
    }

    public enum BuswayMaterial
    {
        Copper,
        Aluminum,
    }

    /// <summary>Standard busway ampacity ratings per manufacturer data.</summary>
    public static readonly int[] StandardRatings =
    {
        225, 400, 600, 800, 1000, 1200, 1350, 1600,
        2000, 2500, 3000, 4000, 5000,
    };

    /// <summary>Busway specification result.</summary>
    public record BuswaySpecification
    {
        public BuswayType Type { get; init; }
        public BuswayMaterial Material { get; init; }
        public int RatingAmps { get; init; }
        public double SystemVoltage { get; init; }
        public double VoltageDrop { get; init; }
        public double VoltageDropPercent { get; init; }
        public bool VoltageDropAcceptable { get; init; }
        public double LengthFeet { get; init; }
    }

    /// <summary>Tap rule evaluation result per NEC 368.17.</summary>
    public record TapRuleResult
    {
        public bool IsCompliant { get; init; }
        public string NecReference { get; init; } = "";
        public double TapAmps { get; init; }
        public double BuswayAmps { get; init; }
        public double TapRatioPercent { get; init; }
        public string Reason { get; init; } = "";
    }

    // ── Busway Selection ─────────────────────────────────────────────────────

    /// <summary>
    /// Selects the minimum standard busway rating for a given load.
    /// Applies 125% continuous-load factor if specified.
    /// </summary>
    /// <param name="loadAmps">Maximum expected load in amps.</param>
    /// <param name="continuousLoad">Whether the load is continuous (≥3 hours).</param>
    public static int SelectRating(double loadAmps, bool continuousLoad = false)
    {
        double requiredAmps = continuousLoad ? loadAmps * 1.25 : loadAmps;
        int rating = StandardRatings.FirstOrDefault(r => r >= requiredAmps);
        return rating > 0 ? rating : StandardRatings[^1];
    }

    // ── Voltage Drop ─────────────────────────────────────────────────────────

    /// <summary>
    /// Typical busway impedance (mΩ/ft) based on rating and material.
    /// These are approximate manufacturer-published values.
    /// </summary>
    public static double GetImpedancePerFoot(int ratingAmps, BuswayMaterial material)
    {
        // Approximate milliohms per foot for common busway ratings
        // Copper busway has lower impedance vs aluminum
        double factor = material == BuswayMaterial.Aluminum ? 1.6 : 1.0;

        double baseImpedance = ratingAmps switch
        {
            <= 400  => 0.040,
            <= 800  => 0.020,
            <= 1200 => 0.013,
            <= 1600 => 0.010,
            <= 2500 => 0.007,
            <= 4000 => 0.004,
            _       => 0.003,
        };

        return baseImpedance * factor;
    }

    /// <summary>
    /// Calculates voltage drop in a busway run.
    /// VD = √3 × I × Z × L (3-phase) or VD = 2 × I × Z × L (single-phase)
    /// where Z is in ohms/ft.
    /// </summary>
    /// <param name="loadAmps">Load current in amps.</param>
    /// <param name="lengthFeet">Busway run length in feet.</param>
    /// <param name="ratingAmps">Busway rating (determines impedance).</param>
    /// <param name="material">Copper or aluminum.</param>
    /// <param name="threePhase">True for 3-phase, false for single-phase.</param>
    public static double CalculateVoltageDrop(
        double loadAmps, double lengthFeet,
        int ratingAmps, BuswayMaterial material = BuswayMaterial.Copper,
        bool threePhase = true)
    {
        double zPerFoot = GetImpedancePerFoot(ratingAmps, material) / 1000.0; // mΩ → Ω
        double multiplier = threePhase ? Math.Sqrt(3) : 2.0;
        return multiplier * loadAmps * zPerFoot * lengthFeet;
    }

    // ── Full Specification ───────────────────────────────────────────────────

    /// <summary>
    /// Produces a complete busway specification with rating selection and voltage drop.
    /// </summary>
    /// <param name="loadAmps">Peak load current.</param>
    /// <param name="systemVoltage">System voltage (e.g. 480, 208).</param>
    /// <param name="lengthFeet">Run length in feet.</param>
    /// <param name="busType">Plug-in or feeder busway.</param>
    /// <param name="material">Conductor material.</param>
    /// <param name="continuousLoad">Whether load is continuous.</param>
    /// <param name="maxVoltageDropPercent">Maximum acceptable VD% (default 3%).</param>
    public static BuswaySpecification Specify(
        double loadAmps, double systemVoltage, double lengthFeet,
        BuswayType busType = BuswayType.Feeder,
        BuswayMaterial material = BuswayMaterial.Copper,
        bool continuousLoad = false,
        double maxVoltageDropPercent = 3.0)
    {
        int rating = SelectRating(loadAmps, continuousLoad);
        double vd = CalculateVoltageDrop(loadAmps, lengthFeet, rating, material);
        double vdPercent = systemVoltage > 0 ? (vd / systemVoltage) * 100.0 : 0;

        return new BuswaySpecification
        {
            Type = busType,
            Material = material,
            RatingAmps = rating,
            SystemVoltage = systemVoltage,
            VoltageDrop = Math.Round(vd, 2),
            VoltageDropPercent = Math.Round(vdPercent, 2),
            VoltageDropAcceptable = vdPercent <= maxVoltageDropPercent,
            LengthFeet = lengthFeet,
        };
    }

    // ── Tap Rules ────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates busway tap compliance per NEC 368.17 and 240.21(B).
    /// </summary>
    /// <param name="buswayAmps">Busway rating in amps.</param>
    /// <param name="tapAmps">Tap device/conductor ampacity.</param>
    /// <param name="tapLengthFeet">Tap conductor length in feet.</param>
    public static TapRuleResult EvaluateTapRule(
        double buswayAmps, double tapAmps, double tapLengthFeet)
    {
        double ratio = buswayAmps > 0 ? (tapAmps / buswayAmps) * 100.0 : 0;

        // NEC 368.17(C): Tap conductors for individual outlets
        // NEC 240.21(B)(1): 10-ft tap rule — tap ≥ 10% of OCPD rating
        // NEC 240.21(B)(2): 25-ft tap rule — tap ≥ 1/3 of OCPD rating
        if (tapLengthFeet <= 10)
        {
            bool compliant = tapAmps >= buswayAmps * 0.10;
            return new TapRuleResult
            {
                IsCompliant = compliant,
                NecReference = "NEC 240.21(B)(1) 10-ft tap rule",
                TapAmps = tapAmps,
                BuswayAmps = buswayAmps,
                TapRatioPercent = Math.Round(ratio, 1),
                Reason = compliant
                    ? "Tap ≥ 10% of busway rating within 10 ft"
                    : $"Tap ({tapAmps}A) < 10% of busway ({buswayAmps}A)",
            };
        }
        else if (tapLengthFeet <= 25)
        {
            bool compliant = tapAmps >= buswayAmps / 3.0;
            return new TapRuleResult
            {
                IsCompliant = compliant,
                NecReference = "NEC 240.21(B)(2) 25-ft tap rule",
                TapAmps = tapAmps,
                BuswayAmps = buswayAmps,
                TapRatioPercent = Math.Round(ratio, 1),
                Reason = compliant
                    ? "Tap ≥ 1/3 of busway rating within 25 ft"
                    : $"Tap ({tapAmps}A) < 1/3 of busway ({buswayAmps}A)",
            };
        }
        else
        {
            // Over 25 ft: generally must be protected at point of supply
            return new TapRuleResult
            {
                IsCompliant = false,
                NecReference = "NEC 240.21(B)",
                TapAmps = tapAmps,
                BuswayAmps = buswayAmps,
                TapRatioPercent = Math.Round(ratio, 1),
                Reason = "Tap exceeds 25 ft — requires OCPD at point of supply",
            };
        }
    }

    // ── Derating for Altitude ────────────────────────────────────────────────

    /// <summary>
    /// Derating factor for busway at high altitude per manufacturer data.
    /// Above 3300 ft (1000m), typical derating ~1% per 330 ft.
    /// </summary>
    public static double GetAltitudeDerating(double altitudeFeet)
    {
        if (altitudeFeet <= 3300)
            return 1.0;

        double excessFeet = altitudeFeet - 3300;
        double deratingPercent = excessFeet / 330.0; // ~1% per 330 ft
        return Math.Max(0.5, 1.0 - deratingPercent / 100.0);
    }
}
