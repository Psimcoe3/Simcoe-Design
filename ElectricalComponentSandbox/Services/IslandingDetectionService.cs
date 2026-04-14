using System;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Anti-islanding assessment using window, ROCOF, vector-shift, and reconnect permissive checks.
/// </summary>
public static class IslandingDetectionService
{
    public record DetectionThresholds
    {
        public double MinFrequencyHz { get; init; }
        public double MaxFrequencyHz { get; init; }
        public double MinVoltagePu { get; init; }
        public double MaxVoltagePu { get; init; }
        public double MaxRocofHzPerSec { get; init; }
        public double MaxVectorShiftDeg { get; init; }
    }

    public record IslandingAssessment
    {
        public double FrequencyHz { get; init; }
        public double VoltagePu { get; init; }
        public double RocofHzPerSec { get; init; }
        public double VectorShiftDeg { get; init; }
        public bool FrequencyTrip { get; init; }
        public bool VoltageTrip { get; init; }
        public bool RocofTrip { get; init; }
        public bool VectorShiftTrip { get; init; }
        public bool TripRequired { get; init; }
        public string? Reason { get; init; }
    }

    public static DetectionThresholds GetDefaultThresholds(double nominalFrequencyHz = 60)
    {
        if (nominalFrequencyHz <= 0)
            throw new ArgumentException("Nominal frequency must be positive.");

        return nominalFrequencyHz switch
        {
            <= 50.1 => new DetectionThresholds
            {
                MinFrequencyHz = 47.5,
                MaxFrequencyHz = 51.5,
                MinVoltagePu = 0.88,
                MaxVoltagePu = 1.10,
                MaxRocofHzPerSec = 0.5,
                MaxVectorShiftDeg = 10,
            },
            _ => new DetectionThresholds
            {
                MinFrequencyHz = 57.0,
                MaxFrequencyHz = 61.8,
                MinVoltagePu = 0.88,
                MaxVoltagePu = 1.10,
                MaxRocofHzPerSec = 0.5,
                MaxVectorShiftDeg = 10,
            },
        };
    }

    public static double CalculateRocof(double previousFrequencyHz, double currentFrequencyHz, double deltaTimeSec)
    {
        if (previousFrequencyHz <= 0 || currentFrequencyHz <= 0)
            throw new ArgumentException("Frequencies must be positive.");
        if (deltaTimeSec <= 0)
            throw new ArgumentException("Delta time must be positive.");

        return Math.Round(Math.Abs(currentFrequencyHz - previousFrequencyHz) / deltaTimeSec, 3);
    }

    public static IslandingAssessment AssessIslanding(
        double frequencyHz,
        double voltagePu,
        double rocofHzPerSec,
        double vectorShiftDeg,
        DetectionThresholds? thresholds = null)
    {
        var limits = thresholds ?? GetDefaultThresholds();

        bool frequencyTrip = frequencyHz < limits.MinFrequencyHz || frequencyHz > limits.MaxFrequencyHz;
        bool voltageTrip = voltagePu < limits.MinVoltagePu || voltagePu > limits.MaxVoltagePu;
        bool rocofTrip = Math.Abs(rocofHzPerSec) > limits.MaxRocofHzPerSec;
        bool vectorShiftTrip = Math.Abs(vectorShiftDeg) > limits.MaxVectorShiftDeg;

        string? reason = null;
        if (frequencyTrip) reason = "Frequency outside anti-islanding window";
        else if (voltageTrip) reason = "Voltage outside anti-islanding window";
        else if (rocofTrip) reason = "ROCOF exceeds anti-islanding limit";
        else if (vectorShiftTrip) reason = "Vector shift exceeds anti-islanding limit";

        return new IslandingAssessment
        {
            FrequencyHz = Math.Round(frequencyHz, 3),
            VoltagePu = Math.Round(voltagePu, 3),
            RocofHzPerSec = Math.Round(Math.Abs(rocofHzPerSec), 3),
            VectorShiftDeg = Math.Round(Math.Abs(vectorShiftDeg), 2),
            FrequencyTrip = frequencyTrip,
            VoltageTrip = voltageTrip,
            RocofTrip = rocofTrip,
            VectorShiftTrip = vectorShiftTrip,
            TripRequired = frequencyTrip || voltageTrip || rocofTrip || vectorShiftTrip,
            Reason = reason,
        };
    }

    public static bool CanReconnect(
        double frequencyHz,
        double voltagePu,
        double stableTimeSec,
        DetectionThresholds? thresholds = null,
        double requiredStableTimeSec = 300)
    {
        if (stableTimeSec < 0 || requiredStableTimeSec <= 0)
            throw new ArgumentException("Stable time inputs must be valid.");

        var limits = thresholds ?? GetDefaultThresholds();
        bool insideWindow = frequencyHz >= limits.MinFrequencyHz && frequencyHz <= limits.MaxFrequencyHz
            && voltagePu >= limits.MinVoltagePu && voltagePu <= limits.MaxVoltagePu;

        return insideWindow && stableTimeSec >= requiredStableTimeSec;
    }
}