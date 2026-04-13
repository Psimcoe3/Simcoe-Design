using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Motor starting analysis: voltage dip calculation, inrush current estimation,
/// and starting method comparison per IEEE 3002.7 / NEC 430.
/// </summary>
public static class MotorStartingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum StartingMethod
    {
        AcrossTheLine,       // DOL — full voltage
        ReducedVoltageAutotransformer,  // 65%/80% tap
        WyeDelta,            // Y-Δ — 33% starting torque
        SoftStarter,         // Electronic ramp
        VariableFrequencyDrive,  // VFD — near unity inrush
    }

    public enum MotorType
    {
        SquirrelCage,        // NEMA B typical
        WoundRotor,
        SynchronousMotor,
    }

    /// <summary>Motor parameters for starting analysis.</summary>
    public record MotorParameters
    {
        public double RatedHP { get; init; }
        public double RatedVoltage { get; init; }
        public double FullLoadAmps { get; init; }
        public double LockedRotorAmps { get; init; }
        public double Efficiency { get; init; } = 0.90;
        public double PowerFactor { get; init; } = 0.85;
        public MotorType Type { get; init; } = MotorType.SquirrelCage;
    }

    /// <summary>Voltage dip analysis result.</summary>
    public record VoltageDipResult
    {
        public double InrushAmps { get; init; }
        public double InrushMultiplier { get; init; }
        public double VoltageDipPercent { get; init; }
        public double VoltageDuringStarting { get; init; }
        public bool IsAcceptable { get; init; }
        public double MaxAllowedDipPercent { get; init; }
        public StartingMethod Method { get; init; }
    }

    /// <summary>Starting method comparison entry.</summary>
    public record StartingMethodComparison
    {
        public StartingMethod Method { get; init; }
        public double InrushMultiplier { get; init; }
        public double InrushAmps { get; init; }
        public double StartingTorquePercent { get; init; }
        public double VoltageDipPercent { get; init; }
        public bool MeetsVoltageDipLimit { get; init; }
    }

    // ── Inrush Current ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the typical locked-rotor-to-FLA multiplier for a motor.
    /// NEMA Design B: 6-8× FLA typical.
    /// </summary>
    public static double GetTypicalLrMultiplier(MotorType type, double ratedHP)
    {
        return type switch
        {
            MotorType.SquirrelCage => ratedHP <= 5 ? 7.0 : ratedHP <= 50 ? 6.5 : 6.0,
            MotorType.WoundRotor => 3.5,  // Much lower due to external resistance
            MotorType.SynchronousMotor => 5.5,
            _ => 6.5,
        };
    }

    /// <summary>
    /// Returns the inrush current reduction factor for a starting method.
    /// DOL = 1.0 (full), WyeDelta = 0.33, etc.
    /// </summary>
    public static double GetStartingMethodFactor(StartingMethod method)
    {
        return method switch
        {
            StartingMethod.AcrossTheLine => 1.0,
            StartingMethod.ReducedVoltageAutotransformer => 0.65, // 65% tap typical
            StartingMethod.WyeDelta => 0.333,
            StartingMethod.SoftStarter => 0.40,  // Typical current limit 400% FLA
            StartingMethod.VariableFrequencyDrive => 0.10,  // Near unity, ~100-150% FLA
            _ => 1.0,
        };
    }

    /// <summary>
    /// Starting torque as percent of full-load torque for each method.
    /// </summary>
    public static double GetStartingTorquePercent(StartingMethod method)
    {
        return method switch
        {
            StartingMethod.AcrossTheLine => 100,
            StartingMethod.ReducedVoltageAutotransformer => 42,  // (0.65)² ≈ 42%
            StartingMethod.WyeDelta => 33,  // 1/3 of DOL
            StartingMethod.SoftStarter => 50,  // Adjustable
            StartingMethod.VariableFrequencyDrive => 150,  // Full torque at low speed
            _ => 100,
        };
    }

    // ── Voltage Dip Calculation ──────────────────────────────────────────────

    /// <summary>
    /// Calculates voltage dip during motor starting.
    /// VD% = (Istart × Zsource) / Vsource × 100
    /// Simplified: VD% ≈ Istart / (Istart + Isc) × 100
    /// where Isc = available short circuit current at motor terminals.
    /// </summary>
    /// <param name="motor">Motor parameters.</param>
    /// <param name="availableFaultCurrentAmps">Available fault current at motor bus (amps).</param>
    /// <param name="method">Starting method.</param>
    /// <param name="maxDipPercent">Maximum acceptable voltage dip (default 15% per IEEE 3002.7).</param>
    public static VoltageDipResult CalculateVoltageDip(
        MotorParameters motor,
        double availableFaultCurrentAmps,
        StartingMethod method = StartingMethod.AcrossTheLine,
        double maxDipPercent = 15.0)
    {
        double lrAmps = motor.LockedRotorAmps > 0
            ? motor.LockedRotorAmps
            : motor.FullLoadAmps * GetTypicalLrMultiplier(motor.Type, motor.RatedHP);

        double startFactor = GetStartingMethodFactor(method);
        double inrushAmps = lrAmps * startFactor;
        double inrushMultiplier = motor.FullLoadAmps > 0 ? inrushAmps / motor.FullLoadAmps : 0;

        // Simplified voltage dip: Istart / (Istart + Isc) × 100
        double vdPercent = availableFaultCurrentAmps > 0
            ? (inrushAmps / (inrushAmps + availableFaultCurrentAmps)) * 100.0
            : 100.0;

        double vDuring = motor.RatedVoltage * (1.0 - vdPercent / 100.0);

        return new VoltageDipResult
        {
            InrushAmps = Math.Round(inrushAmps, 1),
            InrushMultiplier = Math.Round(inrushMultiplier, 2),
            VoltageDipPercent = Math.Round(vdPercent, 2),
            VoltageDuringStarting = Math.Round(vDuring, 1),
            IsAcceptable = vdPercent <= maxDipPercent,
            MaxAllowedDipPercent = maxDipPercent,
            Method = method,
        };
    }

    // ── Method Comparison ────────────────────────────────────────────────────

    /// <summary>
    /// Compares all starting methods for a given motor and system.
    /// Returns results sorted by voltage dip (best to worst).
    /// </summary>
    public static List<StartingMethodComparison> CompareStartingMethods(
        MotorParameters motor,
        double availableFaultCurrentAmps,
        double maxDipPercent = 15.0)
    {
        var methods = (StartingMethod[])Enum.GetValues(typeof(StartingMethod));
        var results = new List<StartingMethodComparison>();

        foreach (var method in methods)
        {
            var dip = CalculateVoltageDip(motor, availableFaultCurrentAmps, method, maxDipPercent);
            results.Add(new StartingMethodComparison
            {
                Method = method,
                InrushMultiplier = dip.InrushMultiplier,
                InrushAmps = dip.InrushAmps,
                StartingTorquePercent = GetStartingTorquePercent(method),
                VoltageDipPercent = dip.VoltageDipPercent,
                MeetsVoltageDipLimit = dip.IsAcceptable,
            });
        }

        return results.OrderBy(r => r.VoltageDipPercent).ToList();
    }

    /// <summary>
    /// Recommends the least-restrictive starting method that meets the voltage dip limit.
    /// Prefers DOL if acceptable; otherwise escalates to reduced-voltage methods.
    /// </summary>
    public static StartingMethodComparison? RecommendStartingMethod(
        MotorParameters motor,
        double availableFaultCurrentAmps,
        double maxDipPercent = 15.0)
    {
        // Order by preference: DOL first (simplest), then by cost/complexity
        var preferenceOrder = new[]
        {
            StartingMethod.AcrossTheLine,
            StartingMethod.WyeDelta,
            StartingMethod.ReducedVoltageAutotransformer,
            StartingMethod.SoftStarter,
            StartingMethod.VariableFrequencyDrive,
        };

        foreach (var method in preferenceOrder)
        {
            var dip = CalculateVoltageDip(motor, availableFaultCurrentAmps, method, maxDipPercent);
            if (dip.IsAcceptable)
            {
                return new StartingMethodComparison
                {
                    Method = method,
                    InrushMultiplier = dip.InrushMultiplier,
                    InrushAmps = dip.InrushAmps,
                    StartingTorquePercent = GetStartingTorquePercent(method),
                    VoltageDipPercent = dip.VoltageDipPercent,
                    MeetsVoltageDipLimit = true,
                };
            }
        }

        return null; // No method meets the limit
    }
}
