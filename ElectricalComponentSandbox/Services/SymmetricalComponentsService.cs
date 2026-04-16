using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Symmetrical components (Fortescue) analysis for unbalanced fault calculations
/// per IEEE C37 and IEEE 141 (Red Book).
/// </summary>
public static class SymmetricalComponentsService
{
    public enum FaultType
    {
        ThreePhase,           // Balanced 3P fault
        SingleLineToGround,   // SLG — most common
        LineToLine,           // LL
        DoubleLineToGround,   // DLG
    }

    /// <summary>Simple complex number for impedance calculations.</summary>
    public record Complex
    {
        public double R { get; init; }
        public double X { get; init; }
        public double Magnitude => Math.Sqrt(R * R + X * X);
        public double AngleDeg => Math.Atan2(X, R) * 180.0 / Math.PI;

        public static Complex operator +(Complex a, Complex b) =>
            new() { R = a.R + b.R, X = a.X + b.X };

        public static Complex operator *(Complex a, double s) =>
            new() { R = a.R * s, X = a.X * s };

        public static Complex Parallel(Complex a, Complex b)
        {
            // Z_par = (Z1 × Z2) / (Z1 + Z2) using complex arithmetic
            double dR = a.R + b.R;
            double dX = a.X + b.X;
            double dMag2 = dR * dR + dX * dX;
            if (dMag2 < 1e-12) return new Complex { R = 0, X = 0 };
            double nR = a.R * b.R - a.X * b.X;
            double nX = a.R * b.X + a.X * b.R;
            return new Complex { R = (nR * dR + nX * dX) / dMag2, X = (nX * dR - nR * dX) / dMag2 };
        }
    }

    /// <summary>Sequence impedances for a network element.</summary>
    public record SequenceImpedance
    {
        public Complex Z1 { get; init; } = new();  // Positive sequence
        public Complex Z2 { get; init; } = new();  // Negative sequence
        public Complex Z0 { get; init; } = new();  // Zero sequence
    }

    public record FaultResult
    {
        public FaultType Type { get; init; }
        public double FaultCurrentPU { get; init; }
        public double FaultCurrentAmps { get; init; }
        public double ThreePhaseCurrentAmps { get; init; }
        public double MultiplierVsThreePhase { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public record FaultStudyResult
    {
        public double BaseKVA { get; init; }
        public double BaseVoltageKV { get; init; }
        public double BaseCurrentAmps { get; init; }
        public List<FaultResult> Faults { get; init; } = new();
        public FaultType WorstCase { get; init; }
        public double MaxFaultCurrentAmps { get; init; }
    }

    /// <summary>
    /// Base current from kVA and voltage: I_base = kVA / (√3 × kV).
    /// </summary>
    public static double CalculateBaseCurrent(double baseKVA, double baseKV)
    {
        if (baseKV <= 0) return 0;
        return baseKVA / (Math.Sqrt(3) * baseKV);
    }

    /// <summary>
    /// Three-phase fault current (PU): I_3P = 1 / |Z1|.
    /// </summary>
    public static double CalculateThreePhaseFault(SequenceImpedance z)
    {
        double mag = z.Z1.Magnitude;
        return mag > 1e-12 ? 1.0 / mag : 0;
    }

    /// <summary>
    /// Single-line-to-ground fault current (PU): I_SLG = 3 / |Z1 + Z2 + Z0|.
    /// The factor 3 accounts for the sequence current summing to produce phase current.
    /// </summary>
    public static double CalculateSLGFault(SequenceImpedance z)
    {
        var zTotal = z.Z1 + z.Z2 + z.Z0;
        double mag = zTotal.Magnitude;
        return mag > 1e-12 ? 3.0 / mag : 0;
    }

    /// <summary>
    /// Line-to-line fault current (PU): I_LL = √3 / |Z1 + Z2|.
    /// </summary>
    public static double CalculateLLFault(SequenceImpedance z)
    {
        var zTotal = z.Z1 + z.Z2;
        double mag = zTotal.Magnitude;
        return mag > 1e-12 ? Math.Sqrt(3) / mag : 0;
    }

    /// <summary>
    /// Double-line-to-ground fault current (PU):
    /// I_DLG = 3 × |Z2 parallel Z0| / |Z1 + (Z2 parallel Z0)| × (1/|Z1|).
    /// Simplified: I_DLG ≈ I_3P × (3 × |Z2∥Z0|) / |Z1 + Z2∥Z0|.
    /// </summary>
    public static double CalculateDLGFault(SequenceImpedance z)
    {
        var z2Par0 = Complex.Parallel(z.Z2, z.Z0);
        var zTotal = z.Z1 + z2Par0;
        double totalMag = zTotal.Magnitude;
        if (totalMag < 1e-12) return 0;

        // Phase current magnitude for DLG fault
        double i1 = 1.0 / totalMag;
        // I2 = -I1 × Z0 / (Z2 + Z0)
        var z2PlusZ0 = z.Z2 + z.Z0;
        double z2Plus0Mag = z2PlusZ0.Magnitude;
        double i2 = z2Plus0Mag > 1e-12 ? i1 * z.Z0.Magnitude / z2Plus0Mag : 0;
        // I0 = -I1 × Z2 / (Z2 + Z0)
        double i0 = z2Plus0Mag > 1e-12 ? i1 * z.Z2.Magnitude / z2Plus0Mag : 0;

        // Maximum phase current ≈ |I1 + a²I2 + aI0| — approximate as magnitude sum
        // Conservative: use 3 × I0 as ground return, max phase ≈ √(I1² + I2² + I1×I2)
        double maxPhase = Math.Sqrt(i1 * i1 + i2 * i2 + i1 * i2) * Math.Sqrt(3);
        return maxPhase;
    }

    /// <summary>
    /// Calculate fault current in amps for a given fault type.
    /// </summary>
    public static double CalculateFaultCurrent(FaultType type, SequenceImpedance z,
        double baseKVA, double baseKV)
    {
        double baseCurrent = CalculateBaseCurrent(baseKVA, baseKV);
        double faultPU = type switch
        {
            FaultType.ThreePhase => CalculateThreePhaseFault(z),
            FaultType.SingleLineToGround => CalculateSLGFault(z),
            FaultType.LineToLine => CalculateLLFault(z),
            FaultType.DoubleLineToGround => CalculateDLGFault(z),
            _ => 0,
        };

        return Math.Round(faultPU * baseCurrent, 0);
    }

    /// <summary>
    /// Run a complete fault study: all four fault types, identify worst case.
    /// </summary>
    public static FaultStudyResult RunFaultStudy(SequenceImpedance z, double baseKVA, double baseKV)
    {
        double baseCurrent = CalculateBaseCurrent(baseKVA, baseKV);
        double i3P = CalculateThreePhaseFault(z) * baseCurrent;

        var faults = new List<FaultResult>();

        foreach (FaultType ft in Enum.GetValues(typeof(FaultType)))
        {
            double faultPU = ft switch
            {
                FaultType.ThreePhase => CalculateThreePhaseFault(z),
                FaultType.SingleLineToGround => CalculateSLGFault(z),
                FaultType.LineToLine => CalculateLLFault(z),
                FaultType.DoubleLineToGround => CalculateDLGFault(z),
                _ => 0,
            };

            double faultAmps = Math.Round(faultPU * baseCurrent, 0);
            double mult = i3P > 0 ? faultAmps / i3P : 0;

            faults.Add(new FaultResult
            {
                Type = ft,
                FaultCurrentPU = Math.Round(faultPU, 4),
                FaultCurrentAmps = faultAmps,
                ThreePhaseCurrentAmps = Math.Round(i3P, 0),
                MultiplierVsThreePhase = Math.Round(mult, 3),
                Description = GetFaultDescription(ft),
            });
        }

        var worst = faults.OrderByDescending(f => f.FaultCurrentAmps).First();

        return new FaultStudyResult
        {
            BaseKVA = baseKVA,
            BaseVoltageKV = baseKV,
            BaseCurrentAmps = Math.Round(baseCurrent, 0),
            Faults = faults,
            WorstCase = worst.Type,
            MaxFaultCurrentAmps = worst.FaultCurrentAmps,
        };
    }

    /// <summary>
    /// Typical Z2/Z1 and Z0/Z1 ratios for common equipment.
    /// </summary>
    public static SequenceImpedance EstimateSequenceImpedance(Complex z1,
        double z2ToZ1Ratio = 1.0, double z0ToZ1Ratio = 1.0)
    {
        return new SequenceImpedance
        {
            Z1 = z1,
            Z2 = z1 * z2ToZ1Ratio,
            Z0 = z1 * z0ToZ1Ratio,
        };
    }

    private static string GetFaultDescription(FaultType type)
    {
        return type switch
        {
            FaultType.ThreePhase => "Balanced three-phase fault (most severe for motors)",
            FaultType.SingleLineToGround => "Single line-to-ground (most common, 70-80% of faults)",
            FaultType.LineToLine => "Line-to-line fault (≈87% of 3P magnitude)",
            FaultType.DoubleLineToGround => "Double line-to-ground (high ground current)",
            _ => "",
        };
    }
}
