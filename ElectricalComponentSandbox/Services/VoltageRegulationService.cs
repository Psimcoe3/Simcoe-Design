using System;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Voltage regulation analysis per ANSI C84.1 and IEEE C57.
/// Covers voltage drop assessment, tap changer selection,
/// regulator sizing, and voltage profile analysis.
/// </summary>
public static class VoltageRegulationService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum RegulationType
    {
        NoLoadTapChanger,   // NLTC — de-energized tap change (transformer)
        OnLoadTapChanger,   // OLTC — under-load tap change
        StepVoltageRegulator, // SVR — line voltage regulator
        StaticVarCompensator, // SVC — reactive compensation
    }

    public enum VoltageRange
    {
        RangeA, // ANSI C84.1 Range A — normal operating (±5%)
        RangeB, // ANSI C84.1 Range B — emergency/infrequent (±8.33%/−8.33%)
    }

    public record VoltageProfile
    {
        public double NominalVoltage { get; init; }
        public double MeasuredVoltage { get; init; }
        public double DeviationPercent { get; init; }
        public VoltageRange Range { get; init; }
        public bool WithinRangeA { get; init; }
        public bool WithinRangeB { get; init; }
    }

    public record TapChangerResult
    {
        public RegulationType Type { get; init; }
        public int TapPosition { get; init; }
        public double TapStepPercent { get; init; }
        public double RegulationPercent { get; init; }
        public double OutputVoltage { get; init; }
    }

    public record RegulatorSizingResult
    {
        public double RequiredKVA { get; init; }
        public double SelectedKVA { get; init; }
        public double RegulationRangePercent { get; init; }
        public double LoadAmps { get; init; }
        public int NumberOfSteps { get; init; }
    }

    public record VoltageStudyResult
    {
        public double SourceVoltage { get; init; }
        public double LoadVoltage { get; init; }
        public double TotalDropPercent { get; init; }
        public bool NeedsRegulation { get; init; }
        public RegulationType RecommendedType { get; init; }
    }

    // ── ANSI C84.1 Voltage Assessment ────────────────────────────────────────

    /// <summary>
    /// Evaluates voltage against ANSI C84.1 Range A and Range B limits.
    /// </summary>
    public static VoltageProfile EvaluateVoltage(double nominalVoltage, double measuredVoltage)
    {
        if (nominalVoltage <= 0)
            throw new ArgumentException("Nominal voltage must be positive.");

        double deviation = (measuredVoltage - nominalVoltage) / nominalVoltage * 100;

        // ANSI C84.1 Range A: +5% to −5% (service voltage)
        bool rangeA = deviation >= -5.0 && deviation <= 5.0;

        // ANSI C84.1 Range B: +5.83% to −8.33% (service voltage)
        bool rangeB = deviation >= -8.33 && deviation <= 5.83;

        return new VoltageProfile
        {
            NominalVoltage = nominalVoltage,
            MeasuredVoltage = Math.Round(measuredVoltage, 2),
            DeviationPercent = Math.Round(deviation, 2),
            Range = rangeA ? VoltageRange.RangeA : rangeB ? VoltageRange.RangeB : VoltageRange.RangeB,
            WithinRangeA = rangeA,
            WithinRangeB = rangeB,
        };
    }

    // ── Tap Changer Selection ────────────────────────────────────────────────

    /// <summary>
    /// Calculates required tap position for an NLTC or OLTC.
    /// Standard transformers have 2×2.5% NLTC taps (5 positions) or ±10% OLTC.
    /// </summary>
    public static TapChangerResult SelectTapPosition(
        double nominalVoltage,
        double desiredVoltage,
        RegulationType type = RegulationType.NoLoadTapChanger,
        double tapStepPercent = 2.5,
        int maxTaps = 2)
    {
        if (nominalVoltage <= 0 || desiredVoltage <= 0)
            throw new ArgumentException("Voltages must be positive.");

        double requiredRegulation = (desiredVoltage - nominalVoltage) / nominalVoltage * 100;

        // OLTC has wider range (typically ±10% in 32 steps of 0.625%)
        if (type == RegulationType.OnLoadTapChanger)
        {
            tapStepPercent = 0.625;
            maxTaps = 16; // ±16 positions
        }

        // Find nearest tap position
        int tapPosition = (int)Math.Round(requiredRegulation / tapStepPercent);
        tapPosition = Math.Clamp(tapPosition, -maxTaps, maxTaps);

        double actualRegulation = tapPosition * tapStepPercent;
        double outputVoltage = nominalVoltage * (1 + actualRegulation / 100);

        return new TapChangerResult
        {
            Type = type,
            TapPosition = tapPosition,
            TapStepPercent = tapStepPercent,
            RegulationPercent = Math.Round(actualRegulation, 3),
            OutputVoltage = Math.Round(outputVoltage, 2),
        };
    }

    // ── Step Voltage Regulator Sizing ────────────────────────────────────────

    /// <summary>
    /// Sizes a step voltage regulator (SVR) for a feeder application.
    /// SVR rating (kVA) = system kVA × regulation range / 100.
    /// Standard regulation range is ±10%.
    /// </summary>
    public static RegulatorSizingResult SizeRegulator(
        double loadKVA,
        double systemVoltageKV,
        double regulationRangePercent = 10,
        int phases = 3)
    {
        if (loadKVA <= 0 || systemVoltageKV <= 0)
            throw new ArgumentException("Load and voltage must be positive.");

        // SVR kVA = load kVA × regulation% / 100
        double requiredKVA = loadKVA * regulationRangePercent / 100;

        // Standard SVR sizes
        double[] standardKVA = { 25, 38.3, 50, 57.5, 76.7, 100, 150, 167, 250, 333, 500, 667, 833 };
        double selectedKVA = standardKVA.FirstOrDefault(s => s >= requiredKVA);
        if (selectedKVA == 0) selectedKVA = standardKVA[^1];

        // Load amps
        double loadAmps = phases == 1
            ? loadKVA * 1000 / (systemVoltageKV * 1000)
            : loadKVA * 1000 / (1.732 * systemVoltageKV * 1000);

        // Number of 5/8% steps for ±10% range = 32
        int steps = (int)(2 * regulationRangePercent / 0.625);

        return new RegulatorSizingResult
        {
            RequiredKVA = Math.Round(requiredKVA, 2),
            SelectedKVA = selectedKVA,
            RegulationRangePercent = regulationRangePercent,
            LoadAmps = Math.Round(loadAmps, 1),
            NumberOfSteps = steps,
        };
    }

    // ── Voltage Study ────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a simplified voltage study to determine if regulation is needed.
    /// </summary>
    public static VoltageStudyResult PerformVoltageStudy(
        double sourceVoltage,
        double dropPercent,
        double nominalVoltage)
    {
        if (sourceVoltage <= 0 || nominalVoltage <= 0)
            throw new ArgumentException("Voltages must be positive.");

        double loadVoltage = sourceVoltage * (1 - dropPercent / 100);
        double totalDeviation = (loadVoltage - nominalVoltage) / nominalVoltage * 100;

        bool needsRegulation = Math.Abs(totalDeviation) > 5.0; // Outside Range A

        RegulationType recommended;
        if (!needsRegulation) recommended = RegulationType.NoLoadTapChanger;
        else if (Math.Abs(totalDeviation) <= 10) recommended = RegulationType.OnLoadTapChanger;
        else recommended = RegulationType.StepVoltageRegulator;

        return new VoltageStudyResult
        {
            SourceVoltage = Math.Round(sourceVoltage, 2),
            LoadVoltage = Math.Round(loadVoltage, 2),
            TotalDropPercent = Math.Round(dropPercent, 2),
            NeedsRegulation = needsRegulation,
            RecommendedType = recommended,
        };
    }
}
