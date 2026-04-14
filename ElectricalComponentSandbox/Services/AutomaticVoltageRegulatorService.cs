using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generator automatic voltage regulator calculations for droop, line-drop compensation,
/// and bounded field-command response.
/// </summary>
public static class AutomaticVoltageRegulatorService
{
    public enum AvrMode
    {
        IsochronousVoltage,
        ReactiveDroop,
        PowerFactorControl,
    }

    public record AvrResponse
    {
        public AvrMode Mode { get; init; }
        public double MeasuredVoltagePu { get; init; }
        public double EffectiveSetpointPu { get; init; }
        public double VoltageErrorPu { get; init; }
        public double FieldCommandPercent { get; init; }
        public bool AtCeiling { get; init; }
        public bool AtFloor { get; init; }
    }

    public static double ApplyReactiveDroop(
        double setpointPu,
        double reactiveLoadPercent,
        double droopPercent = 5)
    {
        if (setpointPu <= 0)
            throw new ArgumentException("Setpoint must be positive.");
        if (droopPercent < 0)
            throw new ArgumentException("Droop percent cannot be negative.");

        double droopAdjustment = reactiveLoadPercent / 100.0 * droopPercent / 100.0;
        return Math.Round(setpointPu - droopAdjustment, 4);
    }

    /// <summary>
    /// Calculates AVR line-drop compensation in volts using the resolved current component.
    /// </summary>
    public static double CalculateLineDropCompensation(
        double loadCurrentAmps,
        double resistanceVoltsPerAmp,
        double reactanceVoltsPerAmp,
        double powerFactor = 0.8)
    {
        if (loadCurrentAmps < 0)
            throw new ArgumentException("Load current cannot be negative.");
        if (resistanceVoltsPerAmp < 0 || reactanceVoltsPerAmp < 0)
            throw new ArgumentException("Compensation constants cannot be negative.");
        if (powerFactor <= 0 || powerFactor > 1)
            throw new ArgumentException("Power factor must be greater than 0 and no more than 1.");

        double reactiveFactor = Math.Sqrt(1 - powerFactor * powerFactor);
        double volts = loadCurrentAmps * (resistanceVoltsPerAmp * powerFactor + reactanceVoltsPerAmp * reactiveFactor);
        return Math.Round(volts, 2);
    }

    public static double CalculateFieldCommand(
        double measuredVoltagePu,
        double setpointPu,
        double proportionalGain = 250,
        double floorPercent = 0,
        double ceilingPercent = 100)
    {
        if (measuredVoltagePu <= 0 || setpointPu <= 0)
            throw new ArgumentException("Measured and setpoint voltage must be positive.");
        if (proportionalGain <= 0)
            throw new ArgumentException("Gain must be positive.");

        double rawCommand = 50 + (setpointPu - measuredVoltagePu) * proportionalGain;
        return Math.Round(Math.Clamp(rawCommand, floorPercent, ceilingPercent), 2);
    }

    public static AvrResponse EvaluateResponse(
        double measuredVoltagePu,
        double setpointPu,
        AvrMode mode = AvrMode.ReactiveDroop,
        double reactiveLoadPercent = 0,
        double droopPercent = 5,
        double proportionalGain = 250,
        double floorPercent = 0,
        double ceilingPercent = 100)
    {
        if (measuredVoltagePu <= 0 || setpointPu <= 0)
            throw new ArgumentException("Measured and setpoint voltage must be positive.");

        double effectiveSetpoint = mode == AvrMode.ReactiveDroop
            ? ApplyReactiveDroop(setpointPu, reactiveLoadPercent, droopPercent)
            : setpointPu;

        double fieldCommand = CalculateFieldCommand(
            measuredVoltagePu,
            effectiveSetpoint,
            proportionalGain,
            floorPercent,
            ceilingPercent);

        return new AvrResponse
        {
            Mode = mode,
            MeasuredVoltagePu = Math.Round(measuredVoltagePu, 4),
            EffectiveSetpointPu = Math.Round(effectiveSetpoint, 4),
            VoltageErrorPu = Math.Round(effectiveSetpoint - measuredVoltagePu, 4),
            FieldCommandPercent = fieldCommand,
            AtCeiling = Math.Abs(fieldCommand - ceilingPercent) < 0.001,
            AtFloor = Math.Abs(fieldCommand - floorPercent) < 0.001,
        };
    }
}