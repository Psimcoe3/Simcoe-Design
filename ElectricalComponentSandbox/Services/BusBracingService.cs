using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Bus bracing and short-circuit mechanical withstand calculations
/// per IEEE C37.20 and ANSI/IEEE 605.
/// </summary>
public static class BusBracingService
{
    public enum BusMaterial
    {
        Copper,
        Aluminum,
    }

    public enum BusShape
    {
        Rectangular,
        Tubular,
    }

    public record BusBarSpec
    {
        public BusMaterial Material { get; init; }
        public BusShape Shape { get; init; }
        public double WidthInches { get; init; }
        public double ThicknessInches { get; init; }
        public double PhaseSeparationInches { get; init; } = 8.0;
        public double SupportSpanInches { get; init; } = 24.0;
    }

    public record FaultCondition
    {
        public double SymmetricalFaultKA { get; init; }
        public double AsymmetryFactor { get; init; } = 2.6;
        public double FaultDurationCycles { get; init; } = 30;
    }

    public record BracingForceResult
    {
        public double PeakForcePerUnitLengthLbsPerFt { get; init; }
        public double TotalForceOnSpanLbs { get; init; }
        public double BendingStressPsi { get; init; }
        public double AllowableStressPsi { get; init; }
        public double StressRatio { get; init; }
        public bool IsAdequate { get; init; }
    }

    public record InsulatorRequirement
    {
        public double MinCantileverStrengthLbs { get; init; }
        public double MaxSupportSpanInches { get; init; }
        public string Recommendation { get; init; } = string.Empty;
    }

    public record BracingAssessment
    {
        public BracingForceResult Forces { get; init; } = new();
        public InsulatorRequirement Insulators { get; init; } = new();
        public double WithstandRatingKA { get; init; }
        public bool MeetsBracingRequirement { get; init; }
        public List<string> Recommendations { get; init; } = new();
    }

    /// <summary>
    /// Yield strength (psi) for bus bar material.
    /// </summary>
    public static double GetYieldStrength(BusMaterial material)
    {
        return material switch
        {
            BusMaterial.Copper => 32000.0,    // Half-hard copper
            BusMaterial.Aluminum => 21000.0,  // 6101-T61 aluminum
            _ => 21000.0,
        };
    }

    /// <summary>
    /// Allowable bending stress = 0.67 × yield strength (safety factor ~1.5).
    /// </summary>
    public static double GetAllowableStress(BusMaterial material)
    {
        return 0.67 * GetYieldStrength(material);
    }

    /// <summary>
    /// Section modulus (in³) for a rectangular bus bar: S = (width × thickness²) / 6.
    /// For edgewise orientation (bending about the strong axis), width is the depth.
    /// </summary>
    public static double CalculateSectionModulus(BusBarSpec bus)
    {
        if (bus.Shape == BusShape.Rectangular)
            return (bus.WidthInches * bus.ThicknessInches * bus.ThicknessInches) / 6.0;

        // Tubular: approximate as S ≈ π/32 × (OD⁴ - ID⁴) / OD
        // Using width as OD, thickness as wall
        double od = bus.WidthInches;
        double id = od - 2 * bus.ThicknessInches;
        if (id <= 0) id = 0;
        return Math.PI / 32.0 * (Math.Pow(od, 4) - Math.Pow(id, 4)) / od;
    }

    /// <summary>
    /// Short-circuit force between parallel bus bars per IEEE 605 / ANSI C37.20.
    /// F = K × (I_peak²) × L / D × 5.4 × 10⁻⁷ (lb/ft, single-phase equivalent).
    /// For 3-phase: worst-case outer phase factor K = 0.866.
    /// </summary>
    public static double CalculatePeakForce(FaultCondition fault, double phaseSeparationInches)
    {
        if (phaseSeparationInches <= 0) return 0;

        // Peak asymmetric current in amps
        double iPeakAmps = fault.SymmetricalFaultKA * 1000.0 * fault.AsymmetryFactor;

        // Force per unit length (lbs/ft) between parallel conductors:
        // F/L = 5.4e-7 × I² / D (inches)
        // Three-phase worst case factor = 0.866
        double forcePerFt = 0.866 * 5.4e-7 * iPeakAmps * iPeakAmps / phaseSeparationInches;

        return Math.Round(forcePerFt, 1);
    }

    /// <summary>
    /// Total force on a bus bar span and resulting bending stress.
    /// Assumes simply-supported beam: M_max = F×L²/8, σ = M/S.
    /// </summary>
    public static BracingForceResult CalculateBracingForce(BusBarSpec bus, FaultCondition fault)
    {
        double forcePerFt = CalculatePeakForce(fault, bus.PhaseSeparationInches);
        double spanFt = bus.SupportSpanInches / 12.0;
        double totalForce = forcePerFt * spanFt;

        // Bending moment for simply supported beam: M = wL²/8
        double momentLbIn = forcePerFt * spanFt * bus.SupportSpanInches / 8.0;

        double sectionModulus = CalculateSectionModulus(bus);
        double stress = sectionModulus > 0 ? momentLbIn / sectionModulus : double.MaxValue;
        double allowable = GetAllowableStress(bus.Material);
        double ratio = allowable > 0 ? stress / allowable : double.MaxValue;

        return new BracingForceResult
        {
            PeakForcePerUnitLengthLbsPerFt = forcePerFt,
            TotalForceOnSpanLbs = Math.Round(totalForce, 1),
            BendingStressPsi = Math.Round(stress, 0),
            AllowableStressPsi = Math.Round(allowable, 0),
            StressRatio = Math.Round(ratio, 3),
            IsAdequate = ratio <= 1.0,
        };
    }

    /// <summary>
    /// Insulator cantilever strength requirement: minimum = 2.5 × total span force (safety factor).
    /// </summary>
    public static InsulatorRequirement CalculateInsulatorRequirement(BracingForceResult forces,
        BusBarSpec bus)
    {
        double safetyFactor = 2.5;
        double minStrength = forces.TotalForceOnSpanLbs * safetyFactor;

        // If current span is overstressed, recommend shorter span
        double maxSpan = bus.SupportSpanInches;
        if (!forces.IsAdequate && forces.StressRatio > 0)
        {
            // Stress ∝ L², so new span = old span / √(stress_ratio)
            maxSpan = bus.SupportSpanInches / Math.Sqrt(forces.StressRatio);
        }

        string rec;
        if (minStrength <= 500)
            rec = "Standard epoxy post insulator";
        else if (minStrength <= 2000)
            rec = "High-strength post insulator or porcelain standoff";
        else
            rec = "Heavy-duty braced insulator assembly or reduced span";

        return new InsulatorRequirement
        {
            MinCantileverStrengthLbs = Math.Round(minStrength, 0),
            MaxSupportSpanInches = Math.Round(maxSpan, 1),
            Recommendation = rec,
        };
    }

    /// <summary>
    /// Full bus bracing assessment: forces, insulators, withstand rating, and recommendations.
    /// </summary>
    public static BracingAssessment Assess(BusBarSpec bus, FaultCondition fault)
    {
        var forces = CalculateBracingForce(bus, fault);
        var insulators = CalculateInsulatorRequirement(forces, bus);

        var recs = new List<string>();

        if (!forces.IsAdequate)
            recs.Add($"Bus stress ratio {forces.StressRatio:F2} exceeds 1.0 — reduce support span to {insulators.MaxSupportSpanInches:F0}\" or increase bus cross-section");

        if (forces.StressRatio > 0.8 && forces.IsAdequate)
            recs.Add("Stress ratio > 0.80 — limited margin; consider closer support spacing");

        if (bus.PhaseSeparationInches < 4.0)
            recs.Add("Phase separation below 4\" — verify creepage and clearance per IEEE C37.20");

        // Withstand rating: the max symmetric fault the current bus can handle (ratio = 1.0)
        // Force ∝ I², so I_max = I_current × 1/√(ratio)
        double withstandKA = forces.StressRatio > 0
            ? fault.SymmetricalFaultKA / Math.Sqrt(forces.StressRatio)
            : fault.SymmetricalFaultKA;

        return new BracingAssessment
        {
            Forces = forces,
            Insulators = insulators,
            WithstandRatingKA = Math.Round(withstandKA, 1),
            MeetsBracingRequirement = forces.IsAdequate,
            Recommendations = recs,
        };
    }
}
