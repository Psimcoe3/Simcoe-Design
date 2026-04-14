using System;
using System.Collections.Generic;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Capacitor bank step planning and switching duty estimation.
/// </summary>
public static class CapacitorSwitchingService
{
    public enum SwitchingMethod
    {
        Contactor,
        VacuumContactor,
        VacuumBreaker,
        ZeroCrossSwitch,
    }

    public record CapacitorStep
    {
        public int StepNumber { get; init; }
        public double StepKvar { get; init; }
        public double StepCurrentAmps { get; init; }
    }

    public record SwitchingPlan
    {
        public double TotalKvar { get; init; }
        public int StepCount { get; init; }
        public double NominalVoltage { get; init; }
        public SwitchingMethod Method { get; init; }
        public double EstimatedPeakInrushAmps { get; init; }
        public List<CapacitorStep> Steps { get; init; } = new();
    }

    public static double CalculateCapacitorCurrent(double kvar, double voltage, int phases = 3)
    {
        if (kvar <= 0 || voltage <= 0)
            throw new ArgumentException("kVAR and voltage must be positive.");

        return phases == 1
            ? Math.Round(kvar * 1000.0 / voltage, 2)
            : Math.Round(kvar * 1000.0 / (Math.Sqrt(3) * voltage), 2);
    }

    public static double EstimateInrushCurrent(
        double steadyStateCurrentAmps,
        bool backToBackSwitching = false,
        bool detunedReactor = false)
    {
        if (steadyStateCurrentAmps <= 0)
            throw new ArgumentException("Steady-state current must be positive.");

        double multiplier = backToBackSwitching ? 100 : 30;
        if (detunedReactor)
            multiplier *= 0.4;

        return Math.Round(steadyStateCurrentAmps * multiplier, 1);
    }

    public static SwitchingMethod RecommendSwitchingMethod(
        double totalKvar,
        double voltage,
        bool backToBackSwitching = false,
        bool sensitiveToTransients = false)
    {
        if (totalKvar <= 0 || voltage <= 0)
            throw new ArgumentException("kVAR and voltage must be positive.");

        if (sensitiveToTransients)
            return SwitchingMethod.ZeroCrossSwitch;
        if (voltage > 1000)
            return SwitchingMethod.VacuumBreaker;
        if (backToBackSwitching || totalKvar > 300)
            return SwitchingMethod.VacuumContactor;
        return SwitchingMethod.Contactor;
    }

    public static SwitchingPlan CreateStepPlan(
        double totalKvar,
        double voltage,
        int stepCount = 0,
        bool backToBackSwitching = false,
        bool sensitiveToTransients = false)
    {
        if (totalKvar <= 0 || voltage <= 0)
            throw new ArgumentException("kVAR and voltage must be positive.");

        int effectiveSteps = stepCount > 0
            ? stepCount
            : PowerFactorCorrectionService.RecommendSteps(totalKvar).Steps;
        effectiveSteps = Math.Max(1, effectiveSteps);

        double baseStepKvar = Math.Ceiling(totalKvar / effectiveSteps);
        var steps = new List<CapacitorStep>();
        for (int index = 0; index < effectiveSteps; index++)
        {
            double stepKvar = index == effectiveSteps - 1
                ? Math.Round(totalKvar - baseStepKvar * (effectiveSteps - 1), 1)
                : baseStepKvar;
            if (stepKvar <= 0)
                stepKvar = baseStepKvar;

            steps.Add(new CapacitorStep
            {
                StepNumber = index + 1,
                StepKvar = stepKvar,
                StepCurrentAmps = CalculateCapacitorCurrent(stepKvar, voltage),
            });
        }

        double peakInrush = EstimateInrushCurrent(steps[0].StepCurrentAmps, backToBackSwitching, detunedReactor: sensitiveToTransients);
        return new SwitchingPlan
        {
            TotalKvar = Math.Round(totalKvar, 1),
            StepCount = effectiveSteps,
            NominalVoltage = voltage,
            Method = RecommendSwitchingMethod(totalKvar, voltage, backToBackSwitching, sensitiveToTransients),
            EstimatedPeakInrushAmps = peakInrush,
            Steps = steps,
        };
    }
}