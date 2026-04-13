using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// IEEE 80 substation ground grid design. Calculates touch/step voltage,
/// grid resistance, required conductor sizing, and rod layout.
/// </summary>
public static class GroundGridDesignService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum SoilType
    {
        Wet,            // 30 Ω·m typical
        Moist,          // 100 Ω·m typical
        Dry,            // 1000 Ω·m typical
        Rocky,          // 3000 Ω·m typical
    }

    public enum ConductorMaterial
    {
        CopperSolid,
        CopperStranded,
        CopperClad,
    }

    public record GridInput
    {
        public double LengthM { get; init; }
        public double WidthM { get; init; }
        public double SoilResistivityOhmM { get; init; } = 100;
        public double FaultCurrentAmps { get; init; }
        public double FaultDurationSec { get; init; } = 0.5;
        public double GridDepthM { get; init; } = 0.5;
        public double SurfaceLayerResistivityOhmM { get; init; } = 3000; // Crushed rock
        public double SurfaceLayerThicknessM { get; init; } = 0.1;
        public ConductorMaterial Material { get; init; } = ConductorMaterial.CopperSolid;
    }

    public record GridResistanceResult
    {
        public double AreaM2 { get; init; }
        public double GridResistanceOhms { get; init; }
        public double GroundPotentialRiseV { get; init; }
    }

    public record ConductorSizingResult
    {
        public double MinCrossSectionMm2 { get; init; }
        public string RecommendedSize { get; init; } = "";
        public double FaultCurrentAmps { get; init; }
        public double FaultDurationSec { get; init; }
    }

    public record TouchStepResult
    {
        public double TolerableTouchVoltageV { get; init; }
        public double TolerableStepVoltageV { get; init; }
        public double ActualTouchVoltageV { get; init; }
        public double ActualStepVoltageV { get; init; }
        public bool TouchSafe { get; init; }
        public bool StepSafe { get; init; }
    }

    public record GridDesignResult
    {
        public double AreaM2 { get; init; }
        public double TotalConductorLengthM { get; init; }
        public int ConductorsAlongLength { get; init; }
        public int ConductorsAlongWidth { get; init; }
        public double SpacingM { get; init; }
        public int GroundRods { get; init; }
        public double RodLengthM { get; init; }
        public double GridResistanceOhms { get; init; }
        public bool MeetsRequirements { get; init; }
    }

    // ── Soil Resistivity Defaults ────────────────────────────────────────────

    /// <summary>Returns typical soil resistivity in Ω·m for a soil type.</summary>
    public static double GetTypicalResistivity(SoilType type) => type switch
    {
        SoilType.Wet => 30,
        SoilType.Moist => 100,
        SoilType.Dry => 1000,
        SoilType.Rocky => 3000,
        _ => 100,
    };

    // ── Grid Resistance (IEEE 80 Eq. 57) ────────────────────────────────────

    /// <summary>
    /// Calculates grid resistance using the simplified Sverak formula.
    /// R = ρ / (4 × √(A/π)) + ρ / L_total
    /// </summary>
    public static GridResistanceResult CalculateGridResistance(
        GridInput input,
        double totalConductorLengthM)
    {
        ArgumentNullException.ThrowIfNull(input);
        double area = input.LengthM * input.WidthM;
        if (area <= 0) throw new ArgumentException("Grid area must be positive.");
        if (totalConductorLengthM <= 0) throw new ArgumentException("Conductor length must be positive.");

        double rho = input.SoilResistivityOhmM;
        double r = (rho / (4.0 * Math.Sqrt(area / Math.PI)))
                 + (rho / totalConductorLengthM);

        double gpr = input.FaultCurrentAmps * r;

        return new GridResistanceResult
        {
            AreaM2 = Math.Round(area, 2),
            GridResistanceOhms = Math.Round(r, 4),
            GroundPotentialRiseV = Math.Round(gpr, 1),
        };
    }

    // ── Conductor Sizing (IEEE 80 Eq. 37) ───────────────────────────────────

    /// <summary>
    /// Sizes the ground grid conductor per IEEE 80.
    /// A = I × √(tc) / (α × 1000) — simplified Onderdonk equation for copper.
    /// </summary>
    public static ConductorSizingResult SizeConductor(double faultCurrentAmps, double faultDurationSec)
    {
        if (faultCurrentAmps <= 0) throw new ArgumentException("Fault current must be positive.");
        if (faultDurationSec <= 0) throw new ArgumentException("Fault duration must be positive.");

        // Onderdonk for copper: A(mm²) = I × √(t) / 234 (for ΔT ≈ 250°C, Tm=1083°C)
        double areaMm2 = faultCurrentAmps * Math.Sqrt(faultDurationSec) / 234.0;

        // Map to standard sizes
        string recommended = areaMm2 switch
        {
            <= 16 => "4/0 AWG",
            <= 25 => "250 kcmil",
            <= 35 => "350 kcmil",
            <= 50 => "500 kcmil",
            <= 70 => "750 kcmil",
            _ => "1000 kcmil",
        };

        return new ConductorSizingResult
        {
            MinCrossSectionMm2 = Math.Round(areaMm2, 2),
            RecommendedSize = recommended,
            FaultCurrentAmps = faultCurrentAmps,
            FaultDurationSec = faultDurationSec,
        };
    }

    // ── Touch & Step Voltage (IEEE 80 §8) ───────────────────────────────────

    /// <summary>
    /// Calculates tolerable touch and step voltages per IEEE 80.
    /// Uses 50 kg body weight (conservative) and surface layer derating factor Cs.
    /// </summary>
    public static TouchStepResult EvaluateTouchStep(
        GridInput input,
        double totalConductorLengthM,
        int gridConductorsAcross,
        double gprVoltage)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Surface layer derating factor Cs (IEEE 80 Eq. 27)
        double cs = 1.0 - (0.09 * (1.0 - input.SoilResistivityOhmM / input.SurfaceLayerResistivityOhmM))
                         / (2.0 * input.SurfaceLayerThicknessM + 0.09);

        // Tolerable touch voltage (50 kg): Etouch = (1000 + 1.5 × Cs × ρs) × 0.116 / √t
        double sqrtT = Math.Sqrt(input.FaultDurationSec);
        double tolerableTouch = (1000.0 + 1.5 * cs * input.SurfaceLayerResistivityOhmM) * 0.116 / sqrtT;

        // Tolerable step voltage (50 kg): Estep = (1000 + 6.0 × Cs × ρs) × 0.116 / √t
        double tolerableStep = (1000.0 + 6.0 * cs * input.SurfaceLayerResistivityOhmM) * 0.116 / sqrtT;

        // Actual voltages (simplified mesh/step calculation)
        double area = input.LengthM * input.WidthM;
        double n = Math.Max(gridConductorsAcross, 2);
        double spacing = Math.Max(input.LengthM, input.WidthM) / (n - 1);

        // Mesh voltage (simplified from IEEE 80 Eq. 81)
        double km = (1.0 / (2.0 * Math.PI)) * Math.Log(spacing * spacing / (16.0 * input.GridDepthM * 0.01)
            + Math.Sqrt(spacing * spacing / (16.0 * input.GridDepthM * 0.01)) + 1.0);
        km = Math.Max(km, 0.3); // Floor for numerical stability
        double ki = 0.644 + 0.148 * n;
        double actualTouch = (input.SoilResistivityOhmM * input.FaultCurrentAmps * km * ki) / totalConductorLengthM;

        // Step voltage (simplified)
        double ks = (1.0 / Math.PI) * (1.0 / (2.0 * input.GridDepthM) + 1.0 / (spacing + input.GridDepthM));
        double actualStep = (input.SoilResistivityOhmM * input.FaultCurrentAmps * ks * ki) / totalConductorLengthM;

        return new TouchStepResult
        {
            TolerableTouchVoltageV = Math.Round(tolerableTouch, 1),
            TolerableStepVoltageV = Math.Round(tolerableStep, 1),
            ActualTouchVoltageV = Math.Round(actualTouch, 1),
            ActualStepVoltageV = Math.Round(actualStep, 1),
            TouchSafe = actualTouch <= tolerableTouch,
            StepSafe = actualStep <= tolerableStep,
        };
    }

    // ── Full Grid Design ────────────────────────────────────────────────────

    /// <summary>
    /// Designs a ground grid for a given substation footprint.
    /// Iteratively adjusts spacing to meet touch/step voltage criteria.
    /// </summary>
    public static GridDesignResult DesignGrid(GridInput input, double targetResistanceOhms = 5.0)
    {
        ArgumentNullException.ThrowIfNull(input);
        double area = input.LengthM * input.WidthM;
        if (area <= 0) throw new ArgumentException("Grid area must be positive.");

        // Start with 3m spacing and refine
        double spacing = 6.0;
        bool safe = false;
        int nLength = 0, nWidth = 0;
        double totalLength = 0;
        GridResistanceResult resistance = null!;

        for (int iter = 0; iter < 10 && !safe; iter++)
        {
            spacing = Math.Max(spacing * 0.7, 1.0); // Tighten each iteration
            nLength = Math.Max((int)Math.Ceiling(input.LengthM / spacing) + 1, 2);
            nWidth = Math.Max((int)Math.Ceiling(input.WidthM / spacing) + 1, 2);

            totalLength = nLength * input.WidthM + nWidth * input.LengthM;
            resistance = CalculateGridResistance(input, totalLength);

            var touchStep = EvaluateTouchStep(input, totalLength, Math.Max(nLength, nWidth), resistance.GroundPotentialRiseV);
            safe = touchStep.TouchSafe && touchStep.StepSafe;

            if (resistance.GridResistanceOhms <= targetResistanceOhms && safe) break;
        }

        // Ground rods: one per perimeter crossing, minimum 4
        int rods = Math.Max(2 * (nLength + nWidth - 2), 4);
        double rodLength = input.SoilResistivityOhmM > 500 ? 4.5 : 3.0; // Deeper for high-ρ soil

        return new GridDesignResult
        {
            AreaM2 = Math.Round(area, 2),
            TotalConductorLengthM = Math.Round(totalLength, 1),
            ConductorsAlongLength = nLength,
            ConductorsAlongWidth = nWidth,
            SpacingM = Math.Round(spacing, 2),
            GroundRods = rods,
            RodLengthM = rodLength,
            GridResistanceOhms = Math.Round(resistance.GridResistanceOhms, 4),
            MeetsRequirements = safe && resistance.GridResistanceOhms <= targetResistanceOhms,
        };
    }
}
