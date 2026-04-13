using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Revenue metering, current transformer (CT) sizing, and PT sizing
/// per IEEE C57.13, ANSI C12, and utility metering requirements.
/// </summary>
public static class MeteringService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum MeterType
    {
        SelfContained,   // Direct metering ≤200A (typically)
        TransformerRated, // CT/PT metering >200A
    }

    public enum MeterClass
    {
        Class200,  // 200A self-contained
        Class320,  // 320A self-contained
        Class20,   // 20A CT-rated meter
    }

    public enum CtAccuracyClass
    {
        /// <summary>Revenue metering (0.3% accuracy).</summary>
        C03_Metering,
        /// <summary>Relay/protection (varies C100-C800).</summary>
        C_Relaying,
    }

    /// <summary>Standard CT ratios per IEEE C57.13.</summary>
    public static readonly int[] StandardCtPrimaries = new[]
    {
        50, 75, 100, 150, 200, 250, 300, 400, 500, 600, 800,
        1000, 1200, 1500, 2000, 2500, 3000, 4000, 5000, 6000,
    };

    /// <summary>CT specification result.</summary>
    public record CtSpecification
    {
        public int PrimaryAmps { get; init; }
        public int SecondaryAmps { get; init; } = 5;
        public double Ratio { get; init; }
        public double BurdenVA { get; init; }
        public double MaxBurdenVA { get; init; }
        public CtAccuracyClass AccuracyClass { get; init; }
        public bool IsAdequate { get; init; }
        public double UtilizationPercent { get; init; }
    }

    /// <summary>PT (voltage transformer) specification.</summary>
    public record PtSpecification
    {
        public double PrimaryVoltage { get; init; }
        public double SecondaryVoltage { get; init; } = 120;
        public double Ratio { get; init; }
        public double BurdenVA { get; init; }
    }

    /// <summary>Complete metering specification.</summary>
    public record MeteringSpecification
    {
        public MeterType Type { get; init; }
        public MeterClass Class { get; init; }
        public double ServiceAmps { get; init; }
        public double SystemVoltage { get; init; }
        public bool ThreePhase { get; init; }
        public int CtQuantity { get; init; }
        public CtSpecification? CtSpec { get; init; }
        public PtSpecification? PtSpec { get; init; }
        public bool RequiresPt { get; init; }
        public string Notes { get; init; } = "";
    }

    // ── CT Sizing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects standard CT primary rating for the given load amps.
    /// CT should be sized so load operates at 50-80% of CT rating for
    /// optimal accuracy per IEEE C57.13.
    /// </summary>
    public static int SelectCtPrimary(double loadAmps)
    {
        // Target: load at ~60-80% of CT rating for best accuracy
        double target = loadAmps / 0.8;

        foreach (int primary in StandardCtPrimaries)
        {
            if (primary >= target)
                return primary;
        }
        return StandardCtPrimaries[^1];
    }

    /// <summary>
    /// Full CT specification including burden check.
    /// </summary>
    /// <param name="loadAmps">Expected maximum load current.</param>
    /// <param name="burdenVA">Total connected burden (meter + leads).</param>
    /// <param name="accuracy">Accuracy class required.</param>
    /// <param name="maxBurdenVA">CT burden rating (default: B0.2 = 12.5VA at 5A secondary).</param>
    public static CtSpecification SizeCt(
        double loadAmps,
        double burdenVA = 5.0,
        CtAccuracyClass accuracy = CtAccuracyClass.C03_Metering,
        double maxBurdenVA = 12.5)
    {
        int primary = SelectCtPrimary(loadAmps);
        double ratio = (double)primary / 5;
        double utilization = loadAmps / primary * 100;

        return new CtSpecification
        {
            PrimaryAmps = primary,
            SecondaryAmps = 5,
            Ratio = ratio,
            BurdenVA = burdenVA,
            MaxBurdenVA = maxBurdenVA,
            AccuracyClass = accuracy,
            IsAdequate = burdenVA <= maxBurdenVA && utilization >= 10,
            UtilizationPercent = Math.Round(utilization, 1),
        };
    }

    // ── PT Sizing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes a potential transformer for systems requiring voltage transformation
    /// for metering (typically >600V systems).
    /// </summary>
    public static PtSpecification SizePt(double systemVoltageLL, double burdenVA = 75)
    {
        return new PtSpecification
        {
            PrimaryVoltage = systemVoltageLL,
            SecondaryVoltage = 120,
            Ratio = Math.Round(systemVoltageLL / 120, 2),
            BurdenVA = burdenVA,
        };
    }

    // ── Meter Type Selection ─────────────────────────────────────────────────

    /// <summary>
    /// Determines meter type and class based on service size per utility standards.
    /// Self-contained meters: ≤200A (Class 200) or ≤320A (Class 320).
    /// Transformer-rated: >320A (Class 20 with CTs).
    /// </summary>
    public static MeteringSpecification SpecifyMetering(
        double serviceAmps, double systemVoltageLL,
        bool threePhase = true, double ctBurdenVA = 5.0)
    {
        bool selfContained = serviceAmps <= 320;
        MeterType type = selfContained ? MeterType.SelfContained : MeterType.TransformerRated;
        MeterClass mClass = serviceAmps <= 200 ? MeterClass.Class200
                          : serviceAmps <= 320 ? MeterClass.Class320
                          : MeterClass.Class20;

        CtSpecification? ctSpec = null;
        int ctQty = 0;
        if (!selfContained)
        {
            ctSpec = SizeCt(serviceAmps, ctBurdenVA);
            ctQty = threePhase ? 3 : 2;
        }

        // PTs required for systems >600V
        bool requiresPt = systemVoltageLL > 600;
        PtSpecification? ptSpec = requiresPt ? SizePt(systemVoltageLL) : null;

        string notes = selfContained
            ? $"Self-contained {mClass} meter. Direct connection, no CTs required."
            : $"CT-rated meter (Class 20) with {ctQty}× {ctSpec!.PrimaryAmps}:{ctSpec.SecondaryAmps} CTs.";

        if (requiresPt)
            notes += $" PTs required: {ptSpec!.PrimaryVoltage}:{ptSpec.SecondaryVoltage}V.";

        return new MeteringSpecification
        {
            Type = type,
            Class = mClass,
            ServiceAmps = serviceAmps,
            SystemVoltage = systemVoltageLL,
            ThreePhase = threePhase,
            CtQuantity = ctQty,
            CtSpec = ctSpec,
            PtSpec = ptSpec,
            RequiresPt = requiresPt,
            Notes = notes,
        };
    }

    /// <summary>
    /// Calculates total metering burden (VA) for CT lead wire run.
    /// Burden = I²×R for secondary loop: 5A² × (2 × length_ft × resistance_per_ft).
    /// #10 AWG Cu ≈ 1.02 Ω/1000ft.
    /// </summary>
    public static double CalculateLeadBurden(
        double leadLengthFeetOneWay, double wireResistancePerKFt = 1.02, int secondaryAmps = 5)
    {
        double totalResistance = 2 * leadLengthFeetOneWay * wireResistancePerKFt / 1000;
        return Math.Round(secondaryAmps * secondaryAmps * totalResistance, 2);
    }
}
