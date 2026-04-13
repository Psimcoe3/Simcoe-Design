using System;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Variable Frequency Drive selection per NEC 430 / IEEE 519.
/// Covers drive sizing, harmonic mitigation, cable derating,
/// and braking resistor sizing.
/// </summary>
public static class VfdSelectionService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum VfdType
    {
        SixPulse,       // Standard 6-pulse — ~30% ITHD
        TwelvePulse,    // 12-pulse with phase-shift transformer — ~12% ITHD
        EighteenPulse,  // 18-pulse — ~5% ITHD
        ActiveFrontEnd, // AFE — <5% ITHD, regenerative capable
    }

    public enum MotorDuty
    {
        VariableTorque,   // Fans, pumps — cubic torque law
        ConstantTorque,   // Conveyors, compressors — full torque at all speeds
        ConstantPower,    // Machine tools — constant HP above base speed
    }

    public record MotorSpec
    {
        public double RatedHP { get; init; }
        public double RatedVoltage { get; init; } = 480;
        public double RatedFLA { get; init; }
        public double ServiceFactor { get; init; } = 1.15;
        public MotorDuty Duty { get; init; } = MotorDuty.VariableTorque;
        public int Poles { get; init; } = 4; // → 1800 RPM at 60Hz
    }

    public record VfdSizingResult
    {
        public double RequiredHP { get; init; }
        public double SelectedHP { get; init; }
        public double OutputAmps { get; init; }
        public double InputAmps { get; init; }
        public VfdType Type { get; init; }
        public double EstimatedTHDiPercent { get; init; }
        public double Efficiency { get; init; }
    }

    public record HarmonicResult
    {
        public VfdType DriveType { get; init; }
        public double EstimatedITHDPercent { get; init; }
        public double EstimatedVTHDPercent { get; init; }
        public bool MeetsIeee519 { get; init; }
        public string Recommendation { get; init; } = "";
    }

    public record CableDerating
    {
        public double StandardAmps { get; init; }
        public double DeratedAmps { get; init; }
        public double DeratingFactor { get; init; }
        public double MaxCableLengthFeet { get; init; }
    }

    public record BrakingResistorResult
    {
        public double PeakBrakingKW { get; init; }
        public double ContinuousBrakingKW { get; init; }
        public double ResistorOhms { get; init; }
        public double ResistorWatts { get; init; }
    }

    // ── Standard VFD Frame Sizes (HP at 480V) ────────────────────────────────

    private static readonly double[] StandardHP =
        { 0.5, 0.75, 1, 1.5, 2, 3, 5, 7.5, 10, 15, 20, 25, 30, 40, 50, 60, 75, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 1000 };

    // ── VFD Sizing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Selects a VFD for a motor application. 
    /// Constant torque requires oversizing by ~1 frame.
    /// </summary>
    public static VfdSizingResult SizeVfd(MotorSpec motor, VfdType type = VfdType.SixPulse)
    {
        ArgumentNullException.ThrowIfNull(motor);
        if (motor.RatedHP <= 0)
            throw new ArgumentException("Motor HP must be positive.");

        // Constant torque applications need 1 size up (higher current at low speed)
        double requiredHP = motor.Duty == MotorDuty.ConstantTorque
            ? motor.RatedHP * 1.15
            : motor.RatedHP;

        // Select standard frame
        double selectedHP = StandardHP.FirstOrDefault(h => h >= requiredHP);
        if (selectedHP == 0) selectedHP = StandardHP[^1];

        // Approximate FLA from HP: I = HP × 746 / (√3 × V × eff × PF)
        // Typical motor: eff=0.92, PF=0.86
        double outputAmps = motor.RatedFLA > 0
            ? motor.RatedFLA
            : selectedHP * 746 / (1.732 * motor.RatedVoltage * 0.92 * 0.86);

        // VFD efficiency
        double efficiency = type switch
        {
            VfdType.ActiveFrontEnd => 0.96,
            VfdType.EighteenPulse => 0.97,
            VfdType.TwelvePulse => 0.97,
            _ => 0.97,
        };

        double inputAmps = outputAmps / efficiency;

        double thdi = GetEstimatedTHDi(type);

        return new VfdSizingResult
        {
            RequiredHP = Math.Round(requiredHP, 2),
            SelectedHP = selectedHP,
            OutputAmps = Math.Round(outputAmps, 1),
            InputAmps = Math.Round(inputAmps, 1),
            Type = type,
            EstimatedTHDiPercent = thdi,
            Efficiency = efficiency,
        };
    }

    // ── Harmonic Assessment ──────────────────────────────────────────────────

    /// <summary>
    /// Estimates harmonic impact and checks IEEE 519 compliance.
    /// </summary>
    public static HarmonicResult AssessHarmonics(
        VfdType type,
        double vfdKVA,
        double transformerKVA)
    {
        if (vfdKVA <= 0 || transformerKVA <= 0)
            throw new ArgumentException("KVA values must be positive.");

        double thdi = GetEstimatedTHDi(type);
        double loadRatio = vfdKVA / transformerKVA;

        // VTHD ≈ ITHD × load_ratio × transformer impedance (~5.75%)
        double vthd = thdi * loadRatio * 0.0575 * 100;

        // IEEE 519 limits: ITHD ≤ 5% at PCC for Isc/IL 20-50 range
        bool meetsLimit = thdi <= 8.0 && vthd <= 5.0;

        string recommendation = type switch
        {
            VfdType.SixPulse when !meetsLimit => "Consider 12-pulse or 18-pulse drive, or add line reactor/harmonic filter.",
            VfdType.TwelvePulse when !meetsLimit => "Consider 18-pulse or active front end drive.",
            _ when meetsLimit => "Current configuration meets IEEE 519 limits.",
            _ => "Add passive or active harmonic filter.",
        };

        return new HarmonicResult
        {
            DriveType = type,
            EstimatedITHDPercent = Math.Round(thdi, 1),
            EstimatedVTHDPercent = Math.Round(vthd, 2),
            MeetsIeee519 = meetsLimit,
            Recommendation = recommendation,
        };
    }

    // ── Output Cable Derating ────────────────────────────────────────────────

    /// <summary>
    /// Derates output cable for VFD PWM waveform effects (additional heating).
    /// Returns maximum recommended cable length to prevent reflected wave damage.
    /// </summary>
    public static CableDerating DerateCable(
        double motorFLA,
        double cableAmps,
        double switchingFreqKHz = 4.0)
    {
        if (motorFLA <= 0 || cableAmps <= 0)
            throw new ArgumentException("Current values must be positive.");

        // VFD output cables derate ~5% due to harmonic heating
        double deratingFactor = 0.95;
        double derated = cableAmps * deratingFactor;

        // Max cable length to avoid reflected wave issues (rule of thumb)
        // Higher switching frequency → shorter max length
        double maxLengthFt = switchingFreqKHz switch
        {
            <= 2.0 => 1000,
            <= 4.0 => 500,
            <= 8.0 => 300,
            <= 12.0 => 200,
            _ => 100,
        };

        return new CableDerating
        {
            StandardAmps = Math.Round(cableAmps, 1),
            DeratedAmps = Math.Round(derated, 1),
            DeratingFactor = deratingFactor,
            MaxCableLengthFeet = maxLengthFt,
        };
    }

    // ── Braking Resistor ─────────────────────────────────────────────────────

    /// <summary>
    /// Sizes a dynamic braking resistor for regenerative loads.
    /// </summary>
    public static BrakingResistorResult SizeBrakingResistor(
        double motorHP,
        double dcBusVoltage,
        double brakingDutyCyclePercent = 10)
    {
        if (motorHP <= 0 || dcBusVoltage <= 0)
            throw new ArgumentException("Motor HP and DC bus voltage must be positive.");

        // Peak braking power ≈ motor rated power
        double peakKW = motorHP * 0.746;

        // Continuous = peak × duty cycle
        double continuousKW = peakKW * (brakingDutyCyclePercent / 100.0);

        // R = V² / P (minimum resistance for peak brake power)
        double resistorOhms = (dcBusVoltage * dcBusVoltage) / (peakKW * 1000);

        // Resistor watt rating = continuous power with 1.5× safety factor
        double resistorWatts = continuousKW * 1000 * 1.5;

        return new BrakingResistorResult
        {
            PeakBrakingKW = Math.Round(peakKW, 2),
            ContinuousBrakingKW = Math.Round(continuousKW, 2),
            ResistorOhms = Math.Round(resistorOhms, 1),
            ResistorWatts = Math.Round(resistorWatts, 0),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double GetEstimatedTHDi(VfdType type) => type switch
    {
        VfdType.SixPulse => 30.0,
        VfdType.TwelvePulse => 12.0,
        VfdType.EighteenPulse => 5.0,
        VfdType.ActiveFrontEnd => 3.0,
        _ => 30.0,
    };
}
