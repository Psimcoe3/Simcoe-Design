using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generator load-bank testing helpers for NFPA 110 style exercising and acceptance loading.
/// </summary>
public static class LoadBankTestService
{
    public enum TestType
    {
        MonthlyExercise,
        Acceptance,
        Annual,
    }

    public record LoadStep
    {
        public double LoadPercent { get; init; }
        public double LoadKW { get; init; }
        public int DurationMinutes { get; init; }
    }

    public record ExercisePlan
    {
        public double GeneratorKW { get; init; }
        public double BuildingLoadKW { get; init; }
        public double RequiredLoadPercent { get; init; }
        public double RequiredLoadKW { get; init; }
        public double SupplementalLoadBankKW { get; init; }
        public int DurationMinutes { get; init; }
        public bool RequiresLoadBank { get; init; }
    }

    public record TestEvaluation
    {
        public bool Passed { get; init; }
        public double AchievedLoadPercent { get; init; }
        public List<string> Issues { get; init; } = new();
    }

    public static double GetMinimumExerciseLoadPercent(bool wetStackingConcern = false)
    {
        return wetStackingConcern ? 50 : 30;
    }

    /// <summary>
    /// Creates a monthly exercise plan and identifies any supplemental load-bank kW required.
    /// </summary>
    public static ExercisePlan CreateMonthlyExercisePlan(
        double generatorKW,
        double buildingLoadKW,
        int durationMinutes = 30,
        bool wetStackingConcern = false)
    {
        if (generatorKW <= 0 || buildingLoadKW < 0 || durationMinutes <= 0)
            throw new ArgumentException("Generator, load, and duration inputs must be valid.");

        double requiredPercent = GetMinimumExerciseLoadPercent(wetStackingConcern);
        double requiredKW = generatorKW * requiredPercent / 100.0;
        double supplementalKW = Math.Max(0, requiredKW - buildingLoadKW);

        return new ExercisePlan
        {
            GeneratorKW = Math.Round(generatorKW, 1),
            BuildingLoadKW = Math.Round(buildingLoadKW, 1),
            RequiredLoadPercent = requiredPercent,
            RequiredLoadKW = Math.Round(requiredKW, 1),
            SupplementalLoadBankKW = Math.Round(supplementalKW, 1),
            DurationMinutes = durationMinutes,
            RequiresLoadBank = supplementalKW > 0,
        };
    }

    /// <summary>
    /// Builds a stepped acceptance test plan using standard quarter-load increments.
    /// </summary>
    public static List<LoadStep> CreateAcceptanceTestPlan(double generatorKW)
    {
        if (generatorKW <= 0)
            throw new ArgumentException("Generator rating must be positive.");

        double[] percents = { 25, 50, 75, 100 };
        int[] durations = { 30, 30, 60, 60 };

        return percents
            .Select((percent, index) => new LoadStep
            {
                LoadPercent = percent,
                LoadKW = Math.Round(generatorKW * percent / 100.0, 1),
                DurationMinutes = durations[index],
            })
            .ToList();
    }

    /// <summary>
    /// Evaluates whether a test met minimum loading and transient performance criteria.
    /// </summary>
    public static TestEvaluation EvaluateTestResults(
        double generatorKW,
        double achievedLoadKW,
        double voltageDipPercent,
        double frequencyDipPercent,
        double minimumLoadPercent = 30,
        double maxVoltageDipPercent = 15,
        double maxFrequencyDipPercent = 10)
    {
        if (generatorKW <= 0 || achievedLoadKW < 0)
            throw new ArgumentException("Generator rating must be positive and achieved load cannot be negative.");

        double achievedLoadPercent = achievedLoadKW / generatorKW * 100.0;
        var issues = new List<string>();

        if (achievedLoadPercent < minimumLoadPercent)
            issues.Add("Achieved load is below the minimum exercise threshold");
        if (voltageDipPercent > maxVoltageDipPercent)
            issues.Add("Voltage dip exceeds acceptable transient limit");
        if (frequencyDipPercent > maxFrequencyDipPercent)
            issues.Add("Frequency dip exceeds acceptable transient limit");

        return new TestEvaluation
        {
            Passed = issues.Count == 0,
            AchievedLoadPercent = Math.Round(achievedLoadPercent, 1),
            Issues = issues,
        };
    }

    public static double EstimateFuelUsed(
        double generatorKW,
        double loadPercent,
        double durationHours,
        double gallonsPerKwh = 0.071)
    {
        if (generatorKW <= 0 || durationHours <= 0)
            throw new ArgumentException("Generator rating and duration must be positive.");
        if (loadPercent <= 0 || loadPercent > 100)
            throw new ArgumentException("Load percent must be greater than 0 and no more than 100.");
        if (gallonsPerKwh <= 0)
            throw new ArgumentException("Fuel rate must be positive.");

        double outputKW = generatorKW * loadPercent / 100.0;
        return Math.Round(outputKW * durationHours * gallonsPerKwh, 2);
    }
}