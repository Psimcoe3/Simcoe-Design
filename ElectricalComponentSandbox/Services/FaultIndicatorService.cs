using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Models pickup, latching, and reset behavior for simple distribution fault indicators.
/// </summary>
public static class FaultIndicatorService
{
    public enum IndicatorType
    {
        Overhead,
        Underground,
        Directional,
    }

    public record IndicatorSettings
    {
        public double PickupAmps { get; init; }
        public double MinimumFaultDurationCycles { get; init; }
        public double ResetCurrentAmps { get; init; }
        public double ResetDelayHours { get; init; }
        public bool RequiresVoltageLoss { get; init; }
        public bool DirectionalForwardOnly { get; init; }
    }

    public record IndicatorAssessment
    {
        public bool PickedUp { get; init; }
        public bool Latched { get; init; }
        public bool CanReset { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public static IndicatorSettings GetDefaultSettings(IndicatorType indicatorType)
    {
        return indicatorType switch
        {
            IndicatorType.Underground => new IndicatorSettings
            {
                PickupAmps = 400,
                MinimumFaultDurationCycles = 2,
                ResetCurrentAmps = 15,
                ResetDelayHours = 2,
                RequiresVoltageLoss = true,
            },
            IndicatorType.Directional => new IndicatorSettings
            {
                PickupAmps = 300,
                MinimumFaultDurationCycles = 2,
                ResetCurrentAmps = 20,
                ResetDelayHours = 1,
                RequiresVoltageLoss = false,
                DirectionalForwardOnly = true,
            },
            _ => new IndicatorSettings
            {
                PickupAmps = 200,
                MinimumFaultDurationCycles = 1,
                ResetCurrentAmps = 20,
                ResetDelayHours = 1,
                RequiresVoltageLoss = false,
            },
        };
    }

    public static bool DetectFault(
        IndicatorSettings settings,
        double faultCurrentAmps,
        double durationCycles,
        double voltagePercent = 100,
        bool isForwardFault = true)
    {
        if (faultCurrentAmps < 0 || durationCycles < 0 || voltagePercent < 0)
            throw new ArgumentOutOfRangeException(nameof(faultCurrentAmps), "Fault indicator inputs must be non-negative.");

        bool passesDirectional = !settings.DirectionalForwardOnly || isForwardFault;
        bool passesVoltage = !settings.RequiresVoltageLoss || voltagePercent < 50;

        return faultCurrentAmps >= settings.PickupAmps
            && durationCycles >= settings.MinimumFaultDurationCycles
            && passesDirectional
            && passesVoltage;
    }

    public static bool CanReset(IndicatorSettings settings, double lineCurrentAmps, double hoursSinceFault)
    {
        if (lineCurrentAmps < 0 || hoursSinceFault < 0)
            throw new ArgumentOutOfRangeException(nameof(lineCurrentAmps), "Reset inputs must be non-negative.");

        return lineCurrentAmps <= settings.ResetCurrentAmps
            && hoursSinceFault >= settings.ResetDelayHours;
    }

    public static IndicatorAssessment AssessIndicator(
        IndicatorSettings settings,
        double faultCurrentAmps,
        double durationCycles,
        bool wasLatched,
        double lineCurrentAmps,
        double hoursSinceFault,
        double voltagePercent = 100,
        bool isForwardFault = true)
    {
        bool pickedUp = DetectFault(settings, faultCurrentAmps, durationCycles, voltagePercent, isForwardFault);
        bool canReset = CanReset(settings, lineCurrentAmps, hoursSinceFault);
        bool latched = pickedUp || (wasLatched && !canReset);

        string reason = pickedUp
            ? "Fault current exceeded pickup and duration thresholds"
            : latched
                ? "Indicator remains latched until reset current and delay conditions are met"
                : canReset && wasLatched
                    ? "Indicator reset after current and delay requirements were satisfied"
                    : "No qualifying fault was detected";

        return new IndicatorAssessment
        {
            PickedUp = pickedUp,
            Latched = latched,
            CanReset = canReset,
            Reason = reason,
        };
    }
}