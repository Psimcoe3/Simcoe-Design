using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Medium voltage cable sizing per NEC 310/IEEE 835.
/// Covers ampacity, insulation levels, shield grounding,
/// BIL ratings, and short-circuit withstand.
/// </summary>
public static class MediumVoltageCableService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum InsulationLevel
    {
        Level100,  // 100% — grounded systems, fault cleared ≤1 min
        Level133,  // 133% — ungrounded or high-resistance grounded, cleared ≤1 hr
    }

    public enum InsulationType
    {
        EPR,   // Ethylene Propylene Rubber (90°C)
        XLPE,  // Cross-Linked Polyethylene (90°C)
        TR,    // Tree-Retardant XLPE (90°C)
    }

    public enum VoltageClass
    {
        V5kV,    // 5 kV (4.16 kV systems)
        V8kV,    // 8 kV (6.9 kV systems)
        V15kV,   // 15 kV (13.8 kV systems)
        V25kV,   // 25 kV (23 kV systems)
        V35kV,   // 35 kV (34.5 kV systems)
    }

    public enum ConductorMaterial
    {
        Copper,
        Aluminum,
    }

    public record MvCableSpec
    {
        public VoltageClass VoltageClass { get; init; } = VoltageClass.V15kV;
        public InsulationLevel Level { get; init; } = InsulationLevel.Level100;
        public InsulationType Insulation { get; init; } = InsulationType.XLPE;
        public ConductorMaterial Material { get; init; } = ConductorMaterial.Copper;
        public double SystemVoltageKV { get; init; } = 13.8;
        public int Phases { get; init; } = 3;
    }

    public record AmpacityResult
    {
        public string CableSize { get; init; } = "";
        public double AmpacityAmps { get; init; }
        public double LoadAmps { get; init; }
        public bool IsAdequate { get; init; }
        public double UtilizationPercent { get; init; }
    }

    public record ShortCircuitWithstandResult
    {
        public string CableSize { get; init; } = "";
        public double WithstandAmps { get; init; }
        public double FaultCurrentAmps { get; init; }
        public double FaultDurationSec { get; init; }
        public bool IsAdequate { get; init; }
    }

    public record InsulationSpec
    {
        public VoltageClass VoltageClass { get; init; }
        public InsulationLevel Level { get; init; }
        public double InsulationThicknessMils { get; init; }
        public double BasicInsulationLevelKV { get; init; }
        public double AcWithstandKV { get; init; }
    }

    public record CableSizingResult
    {
        public string RecommendedSize { get; init; } = "";
        public double Ampacity { get; init; }
        public double ShortCircuitWithstand { get; init; }
        public InsulationSpec Insulation { get; init; } = null!;
        public bool MeetsAllCriteria { get; init; }
    }

    // ── MV Ampacity Table (copper, 90°C XLPE, in duct bank) ──────────────────

    private static readonly (string Size, double CuAmps, double AlAmps)[] AmpacityTable =
    {
        ("1/0", 175, 135),
        ("2/0", 200, 155),
        ("3/0", 230, 180),
        ("4/0", 260, 205),
        ("250", 285, 225),
        ("350", 340, 265),
        ("500", 405, 320),
        ("750", 495, 395),
        ("1000", 560, 445),
    };

    // ── Short-Circuit Withstand (Onderdonk, copper 90→250°C) ──────────────────

    // I²t constant for copper at 90°C initial: ~0.0297 (kA²·s per cmil²×10⁻⁶)
    private static readonly (string Size, double AreaCmil)[] ConductorAreas =
    {
        ("1/0", 105600),
        ("2/0", 133100),
        ("3/0", 167800),
        ("4/0", 211600),
        ("250", 250000),
        ("350", 350000),
        ("500", 500000),
        ("750", 750000),
        ("1000", 1000000),
    };

    // ── Insulation/BIL Data ──────────────────────────────────────────────────

    /// <summary>Returns insulation thickness and BIL for given voltage class/level.</summary>
    public static InsulationSpec GetInsulationSpec(VoltageClass voltageClass, InsulationLevel level)
    {
        double thickness = (voltageClass, level) switch
        {
            (VoltageClass.V5kV, InsulationLevel.Level100) => 90,
            (VoltageClass.V5kV, InsulationLevel.Level133) => 115,
            (VoltageClass.V8kV, InsulationLevel.Level100) => 115,
            (VoltageClass.V8kV, InsulationLevel.Level133) => 140,
            (VoltageClass.V15kV, InsulationLevel.Level100) => 175,
            (VoltageClass.V15kV, InsulationLevel.Level133) => 220,
            (VoltageClass.V25kV, InsulationLevel.Level100) => 260,
            (VoltageClass.V25kV, InsulationLevel.Level133) => 320,
            (VoltageClass.V35kV, InsulationLevel.Level100) => 345,
            (VoltageClass.V35kV, InsulationLevel.Level133) => 420,
            _ => 175,
        };

        double bil = voltageClass switch
        {
            VoltageClass.V5kV => 75,
            VoltageClass.V8kV => 95,
            VoltageClass.V15kV => 110,
            VoltageClass.V25kV => 150,
            VoltageClass.V35kV => 200,
            _ => 110,
        };

        double acWithstand = voltageClass switch
        {
            VoltageClass.V5kV => 19,
            VoltageClass.V8kV => 26,
            VoltageClass.V15kV => 36,
            VoltageClass.V25kV => 50,
            VoltageClass.V35kV => 70,
            _ => 36,
        };

        return new InsulationSpec
        {
            VoltageClass = voltageClass,
            Level = level,
            InsulationThicknessMils = thickness,
            BasicInsulationLevelKV = bil,
            AcWithstandKV = acWithstand,
        };
    }

    // ── Ampacity Lookup ──────────────────────────────────────────────────────

    /// <summary>
    /// Selects a cable size for a given load current.
    /// </summary>
    public static AmpacityResult SelectByAmpacity(double loadAmps, MvCableSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (loadAmps <= 0) throw new ArgumentException("Load current must be positive.");

        foreach (var (size, cuAmps, alAmps) in AmpacityTable)
        {
            double ampacity = spec.Material == ConductorMaterial.Copper ? cuAmps : alAmps;
            if (ampacity >= loadAmps)
            {
                return new AmpacityResult
                {
                    CableSize = size,
                    AmpacityAmps = ampacity,
                    LoadAmps = Math.Round(loadAmps, 2),
                    IsAdequate = true,
                    UtilizationPercent = Math.Round(loadAmps / ampacity * 100, 1),
                };
            }
        }

        // If none are big enough, return largest with IsAdequate = false
        var largest = AmpacityTable[^1];
        double largestAmps = spec.Material == ConductorMaterial.Copper ? largest.CuAmps : largest.AlAmps;
        return new AmpacityResult
        {
            CableSize = largest.Size,
            AmpacityAmps = largestAmps,
            LoadAmps = Math.Round(loadAmps, 2),
            IsAdequate = false,
            UtilizationPercent = Math.Round(loadAmps / largestAmps * 100, 1),
        };
    }

    // ── Short-Circuit Withstand ──────────────────────────────────────────────

    /// <summary>
    /// Checks if a cable can withstand the available fault current for a given duration.
    /// Isc = area / (constant × √t) per ICEA P-32-382.
    /// </summary>
    public static ShortCircuitWithstandResult CheckShortCircuitWithstand(
        string cableSize,
        double faultCurrentAmps,
        double faultDurationSec,
        ConductorMaterial material = ConductorMaterial.Copper)
    {
        if (string.IsNullOrEmpty(cableSize)) throw new ArgumentException("Cable size is required.");
        if (faultCurrentAmps <= 0) throw new ArgumentException("Fault current must be positive.");
        if (faultDurationSec <= 0) throw new ArgumentException("Fault duration must be positive.");

        var entry = ConductorAreas.FirstOrDefault(c => c.Size == cableSize);
        if (entry.Size == null) throw new ArgumentException($"Unknown cable size: {cableSize}");

        // Copper: I = 0.0297 × A / √t [A in cmil, I in amps]
        // Aluminum: multiply constant by 0.725
        double constant = material == ConductorMaterial.Copper ? 0.0297 : 0.0215;
        double withstand = constant * entry.AreaCmil / Math.Sqrt(faultDurationSec);

        return new ShortCircuitWithstandResult
        {
            CableSize = cableSize,
            WithstandAmps = Math.Round(withstand, 0),
            FaultCurrentAmps = faultCurrentAmps,
            FaultDurationSec = faultDurationSec,
            IsAdequate = withstand >= faultCurrentAmps,
        };
    }

    // ── Complete Cable Sizing ────────────────────────────────────────────────

    /// <summary>
    /// Sizes MV cable considering ampacity, short-circuit withstand, and insulation.
    /// </summary>
    public static CableSizingResult SizeCable(
        double loadAmps,
        double faultCurrentAmps,
        double faultDurationSec,
        MvCableSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var ampResult = SelectByAmpacity(loadAmps, spec);
        var scResult = CheckShortCircuitWithstand(
            ampResult.CableSize, faultCurrentAmps, faultDurationSec, spec.Material);
        var insulation = GetInsulationSpec(spec.VoltageClass, spec.Level);

        // If SC withstand fails, upsize
        string finalSize = ampResult.CableSize;
        bool scOk = scResult.IsAdequate;
        if (!scOk)
        {
            foreach (var (size, cuAmps, alAmps) in AmpacityTable)
            {
                var check = CheckShortCircuitWithstand(size, faultCurrentAmps, faultDurationSec, spec.Material);
                if (check.IsAdequate)
                {
                    finalSize = size;
                    scOk = true;
                    break;
                }
            }
        }

        double finalAmpacity = GetAmpacity(finalSize, spec.Material);
        double finalWithstand = CheckShortCircuitWithstand(
            finalSize, faultCurrentAmps, faultDurationSec, spec.Material).WithstandAmps;

        return new CableSizingResult
        {
            RecommendedSize = finalSize,
            Ampacity = finalAmpacity,
            ShortCircuitWithstand = finalWithstand,
            Insulation = insulation,
            MeetsAllCriteria = ampResult.IsAdequate && scOk,
        };
    }

    private static double GetAmpacity(string size, ConductorMaterial material)
    {
        var entry = AmpacityTable.FirstOrDefault(a => a.Size == size);
        return material == ConductorMaterial.Copper ? entry.CuAmps : entry.AlAmps;
    }
}
