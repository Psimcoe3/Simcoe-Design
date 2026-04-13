using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Power factor correction and capacitor bank sizing service.
///
/// Analyzes a panel or distribution system's aggregate power factor and recommends
/// capacitor bank sizing to achieve a target power factor (typically 0.95 lagging).
///
/// Key formulas:
///   kVAR_required = kW × (tan(θ₁) - tan(θ₂))
///   where θ₁ = acos(PF_existing), θ₂ = acos(PF_target)
///
/// Benefits of power factor correction:
/// - Reduced utility demand charges (many utilities penalize PF < 0.90)
/// - Reduced line current → less I²R losses
/// - Released system capacity (lower apparent power for same real power)
/// - Improved voltage regulation
///
/// Standard capacitor bank sizes per IEEE C37.99 / IEEE 1036.
/// </summary>
public static class PowerFactorCorrectionService
{
    /// <summary>Standard capacitor bank kVAR sizes.</summary>
    public static readonly double[] StandardCapacitorBankSizes =
    {
        5, 10, 15, 20, 25, 30, 40, 50, 60, 75, 100,
        125, 150, 200, 250, 300, 400, 500, 600, 750, 1000,
    };

    /// <summary>
    /// Result of power factor analysis for a panel or system.
    /// </summary>
    public record PowerFactorAnalysis
    {
        public double TotalRealPowerKW { get; init; }
        public double TotalReactivePowerKVAR { get; init; }
        public double TotalApparentPowerKVA { get; init; }
        public double ExistingPowerFactor { get; init; }
        public double TargetPowerFactor { get; init; }
        public double RequiredCorrectionKVAR { get; init; }
        public double RecommendedBankSizeKVAR { get; init; }
        public double CorrectedPowerFactor { get; init; }
        public double CorrectedApparentPowerKVA { get; init; }
        public double CurrentReductionPercent { get; init; }
        public double CapacityReleasedKVA { get; init; }
        public int NumberOfSteps { get; init; }
        public double StepSizeKVAR { get; init; }
        public List<string> Warnings { get; init; } = new();
    }

    /// <summary>
    /// Analyzes a panel schedule's power factor and recommends correction.
    /// </summary>
    public static PowerFactorAnalysis AnalyzePanel(
        PanelSchedule schedule,
        double targetPowerFactor = 0.95)
    {
        double totalRealW = 0;
        double totalApparentVA = 0;

        foreach (var circuit in schedule.Circuits)
        {
            if (circuit.SlotType != CircuitSlotType.Circuit) continue;
            totalRealW += circuit.EffectiveTrueLoadW;
            totalApparentVA += circuit.DemandLoadVA;
        }

        double totalRealKW = totalRealW / 1000.0;
        double totalApparentKVA = totalApparentVA / 1000.0;

        return CalculateCorrection(totalRealKW, totalApparentKVA, targetPowerFactor);
    }

    /// <summary>
    /// Analyzes aggregate system loads for power factor correction.
    /// </summary>
    public static PowerFactorAnalysis AnalyzeSystem(
        double totalRealPowerKW,
        double totalApparentPowerKVA,
        double targetPowerFactor = 0.95)
    {
        return CalculateCorrection(totalRealPowerKW, totalApparentPowerKVA, targetPowerFactor);
    }

    /// <summary>
    /// Calculates required kVAR correction given existing and target power factors.
    /// </summary>
    public static double CalculateRequiredKVAR(
        double realPowerKW,
        double existingPF,
        double targetPF)
    {
        if (realPowerKW <= 0 || existingPF <= 0 || existingPF >= 1.0) return 0;
        if (targetPF <= 0 || targetPF > 1.0) return 0;
        if (existingPF >= targetPF) return 0;

        double theta1 = Math.Acos(Math.Clamp(existingPF, -1.0, 1.0));
        double theta2 = Math.Acos(Math.Clamp(targetPF, -1.0, 1.0));
        return realPowerKW * (Math.Tan(theta1) - Math.Tan(theta2));
    }

    /// <summary>
    /// Returns the next standard capacitor bank size ≥ the required kVAR.
    /// </summary>
    public static double SelectBankSize(double requiredKVAR)
    {
        if (requiredKVAR <= 0) return 0;
        return StandardCapacitorBankSizes.FirstOrDefault(s => s >= requiredKVAR);
    }

    /// <summary>
    /// Recommends number of switched steps for automatic capacitor bank.
    /// Avoids leading power factor by staging correction.
    /// </summary>
    public static (int Steps, double StepSizeKVAR) RecommendSteps(double totalBankKVAR)
    {
        if (totalBankKVAR <= 0) return (0, 0);
        if (totalBankKVAR <= 25) return (1, totalBankKVAR);
        if (totalBankKVAR <= 75) return (3, Math.Ceiling(totalBankKVAR / 3));
        if (totalBankKVAR <= 200) return (4, Math.Ceiling(totalBankKVAR / 4));
        if (totalBankKVAR <= 500) return (5, Math.Ceiling(totalBankKVAR / 5));
        return (6, Math.Ceiling(totalBankKVAR / 6));
    }

    private static PowerFactorAnalysis CalculateCorrection(
        double totalRealKW, double totalApparentKVA, double targetPF)
    {
        var warnings = new List<string>();

        if (totalApparentKVA <= 0 || totalRealKW <= 0)
        {
            return new PowerFactorAnalysis
            {
                TargetPowerFactor = targetPF,
                CorrectedPowerFactor = 1.0,
            };
        }

        // Clamp to avoid domain errors
        double existingPF = Math.Clamp(totalRealKW / totalApparentKVA, 0.01, 1.0);
        double existingReactiveKVAR = Math.Sqrt(totalApparentKVA * totalApparentKVA - totalRealKW * totalRealKW);

        double requiredKVAR = CalculateRequiredKVAR(totalRealKW, existingPF, targetPF);
        double bankSize = SelectBankSize(requiredKVAR);

        // Calculate corrected values
        double correctedReactiveKVAR = Math.Max(0, existingReactiveKVAR - bankSize);
        double correctedApparentKVA = Math.Sqrt(totalRealKW * totalRealKW + correctedReactiveKVAR * correctedReactiveKVAR);
        double correctedPF = correctedApparentKVA > 0 ? totalRealKW / correctedApparentKVA : 1.0;

        // Capacity released
        double capacityReleased = totalApparentKVA - correctedApparentKVA;

        // Current reduction
        double currentReduction = totalApparentKVA > 0
            ? (1 - correctedApparentKVA / totalApparentKVA) * 100
            : 0;

        // Steps
        var (steps, stepSize) = RecommendSteps(bankSize);

        // Warnings
        if (existingPF < 0.70)
            warnings.Add($"Existing power factor {existingPF:F2} is critically low — verify load data");
        if (correctedPF > 1.0)
            warnings.Add("Correction may cause leading power factor — reduce bank size");
        if (bankSize > 0 && bankSize > requiredKVAR * 1.5)
            warnings.Add($"Standard bank {bankSize} kVAR significantly exceeds required {requiredKVAR:F0} kVAR — use stepped bank");
        if (targetPF < 0.90)
            warnings.Add("Target PF below 0.90 may still incur utility penalties");

        return new PowerFactorAnalysis
        {
            TotalRealPowerKW = Math.Round(totalRealKW, 2),
            TotalReactivePowerKVAR = Math.Round(existingReactiveKVAR, 2),
            TotalApparentPowerKVA = Math.Round(totalApparentKVA, 2),
            ExistingPowerFactor = Math.Round(existingPF, 3),
            TargetPowerFactor = targetPF,
            RequiredCorrectionKVAR = Math.Round(requiredKVAR, 1),
            RecommendedBankSizeKVAR = bankSize,
            CorrectedPowerFactor = Math.Round(Math.Clamp(correctedPF, 0, 1.0), 3),
            CorrectedApparentPowerKVA = Math.Round(correctedApparentKVA, 2),
            CurrentReductionPercent = Math.Round(currentReduction, 1),
            CapacityReleasedKVA = Math.Round(capacityReleased, 2),
            NumberOfSteps = steps,
            StepSizeKVAR = stepSize,
            Warnings = warnings,
        };
    }
}
